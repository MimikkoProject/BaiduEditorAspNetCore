using BaiduEditorHandler.Models;
using BaiduEditorHandler.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace BaiduEditorHandler.Midwares
{

    public class BaiduEditorUploadHandlerMiddleware
    {

        private readonly RequestDelegate _next;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IConfigurationRoot _configuration;
        private readonly ILogger _logger;

        public BaiduEditorUploadHandlerMiddleware(RequestDelegate next, IHostingEnvironment hostingEnvironment, ILoggerFactory loggerFactory, IServiceProvider serviceProvider, IHostingEnvironment env)
        {
            this._next = next;
            this._hostingEnvironment = hostingEnvironment;
            this._logger = loggerFactory.CreateLogger<BaiduEditorUploadHandlerMiddleware>();

            var builder = new ConfigurationBuilder()
                .SetBasePath(hostingEnvironment.ContentRootPath)
                .AddJsonFile("ueditorconfig.json")
                .AddJsonFile($"ueditorconfig.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            this._configuration = builder.Build();
        }

        #region Utilities
        protected async Task WriteJsonAsync(HttpContext httpContext, object response)
        {
            httpContext.Response.Clear();
            httpContext.Response.StatusCode = 200;
            var json = JsonConvert.SerializeObject(response);
            this._logger.LogInformation("Writing json data: " + json.ToString());
            if (!httpContext.Request.Query.ContainsKey("callback"))
            {
                httpContext.Response.ContentType = "text/plain";
                await httpContext.Response.WriteAsync(json);
            }
            else
            {
                var jsonpCallback = httpContext.Request.Query["callback"].ToString();
                httpContext.Response.ContentType = "application/javascript";
                await httpContext.Response.WriteAsync(string.Format("{0}({1});", jsonpCallback, json));
            }
        }
        #endregion

        #region NotSupportedHandler
        protected async Task NotSupportedHandler(HttpContext httpContext)
        {
            await this.WriteJsonAsync(httpContext, new
            {
                state = "Parameter 'action' is null or unsupported."
            });
        }
        #endregion

        #region CrawlerHandler
        protected async Task CrawlerHandler(HttpContext httpContext)
        {
            if (!httpContext.Request.Form.ContainsKey("source[]"))
            {
                await this.WriteJsonAsync(httpContext, new
                {
                    state = "Invalid parameter: the source to fetch is not set."
                });
                return;
            }
            else
            {
                var sources = httpContext.Request.Form["source[]"];
                await this.WriteJsonAsync(httpContext, new
                {
                    state = "SUCCESS",
                    list = sources.Select(async x =>
                    {
                        var y = await new Crawler(this._hostingEnvironment, this._configuration, x).FetchAsync();
                        return new
                        {
                            state = y.State,
                            source = y.SourceUrl,
                            url = y.ServerUrl
                        };
                    }).ToList()
                });
            }
        }
        #endregion

        #region ConfigHandler
        protected async Task ConfigHandler(HttpContext httpContext)
        {
            var configFilePath = Path.Combine(this._hostingEnvironment.ContentRootPath, "ueditorconfig.json");
            var jObj = JObject.Parse(File.ReadAllText(configFilePath));
            await this.WriteJsonAsync(httpContext, jObj);
        }
        #endregion

        #region UploadHandler
        protected async Task WriteUploadHandlerResult(HttpContext httpContext, UploadResult result)
        {
            await this.WriteJsonAsync(httpContext, new
            {
                state = result.State.ToStateMessage(),
                url = result.Url,
                title = result.OriginFileName,
                original = result.OriginFileName,
                error = result.ErrorMessage
            });
        }

        protected async Task UploadHandler(HttpContext httpContext, UploadOptions options)
        {
            byte[] uploadFileBytes = null;
            string uploadFileName = null;

            var result = new UploadResult() { State = UploadState.Unknown };

            if (options.Base64)
            {
                uploadFileName = options.Base64Filename;
                uploadFileBytes = Convert.FromBase64String(httpContext.Request.Form[options.UploadFieldName]);
            }
            else
            {
                var file = httpContext.Request.Form.Files[options.UploadFieldName];
                uploadFileName = file.GetFileName();

                if (!options.CheckFileType(uploadFileName))
                {
                    result.State = UploadState.TypeNotAllow;
                    await this.WriteUploadHandlerResult(httpContext, result);
                    return;
                }
                if (!options.CheckFileSize(file.Length))
                {
                    result.State = UploadState.SizeLimitExceed;
                    await this.WriteUploadHandlerResult(httpContext, result);
                    return;
                }

                try
                {
                    using (var ns = file.OpenReadStream())
                    {
                        using (var ms = new MemoryStream())
                        {
                            ns.CopyTo(ms);
                            ms.Flush();
                            uploadFileBytes = ms.ToArray();
                        }
                    }
                }
                catch (Exception)
                {
                    result.State = UploadState.NetworkError;
                    await this.WriteUploadHandlerResult(httpContext, result);
                }
            }

            result.OriginFileName = uploadFileName;

            var savePath = PathFormatter.Format(uploadFileName, options.PathFormat);
            var localPath = Path.Combine(this._hostingEnvironment.WebRootPath, savePath);
            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(localPath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                }
                this._logger.LogInformation(string.Format("Writing uploaded file {0} to local path {1}.", uploadFileName, localPath));
                File.WriteAllBytes(localPath, uploadFileBytes);
                result.Url = savePath;
                result.State = UploadState.Success;
            }
            catch (Exception e)
            {
                result.State = UploadState.FileAccessError;
                result.ErrorMessage = e.Message;
            }
            finally
            {
                await this.WriteUploadHandlerResult(httpContext, result);
            }
        }
        #endregion

        #region ListFileHandler
        protected async Task WriteListFileHandlerResult(HttpContext httpContext, ResultState state, string[] fileList, int start, int size, int total)
        {
            await this.WriteJsonAsync(httpContext, new
            {
                state = state.ToStateMessage(),
                list = fileList == null ? null : fileList.Select(x => new { url = x }),
                start = start,
                size = size,
                total = total
            });
        }

        protected async Task ListFileHandler(HttpContext httpContext, string listPath, string[] allowedFiles)
        {
            var start = 0;
            var size = 0;
            var total = 0;
            var state = ResultState.Success;
            var searchExtensions = allowedFiles.Select(x => x.ToLower()).ToArray();
            string[] fileList = null;
            try
            {
                start = string.IsNullOrEmpty(httpContext.Request.Query["start"]) ? 0 : Convert.ToInt32(httpContext.Request.Query["start"]);
                size = string.IsNullOrEmpty(httpContext.Request.Query["size"]) ?
                    SystemUtils.Try(() => int.Parse(this._configuration["imageManagerListSize"])) :
                    Convert.ToInt32(httpContext.Request.Query["size"]);
            }
            catch (FormatException)
            {
                state = ResultState.InvalidParam;
                await this.WriteListFileHandlerResult(httpContext, state, fileList, start, size, total);
                return;
            }
            var buildingList = new List<string>();
            try
            {
                var localPath = Path.Combine(this._hostingEnvironment.WebRootPath, listPath);
                buildingList.AddRange(Directory.GetFiles(localPath, "*", SearchOption.AllDirectories)
                    .Where(x => searchExtensions.Contains(Path.GetExtension(x).ToLower()))
                    .Select(x => listPath + x.Substring(localPath.Length).Replace("\\", "/")));
                total = buildingList.Count;
                fileList = buildingList.OrderBy(x => x).Skip(start).Take(size).ToArray();
            }
            catch (UnauthorizedAccessException)
            {
                state = ResultState.AuthorizError;
            }
            catch (DirectoryNotFoundException)
            {
                state = ResultState.PathNotFound;
            }
            catch (IOException)
            {
                state = ResultState.IOError;
            }
            finally
            {
                await this.WriteListFileHandlerResult(httpContext, state, fileList, start, size, total);
            }
        }
        #endregion

        public async Task Invoke(HttpContext httpContext)
        {
            try
            {
                if (!httpContext.Request.Query.ContainsKey("action"))
                {
                    this._logger.LogInformation(string.Format("Handling request: {0} without an 'action' form field.", httpContext.Request.Path));
                }
                else
                {
                    httpContext.Response.OnStarting(async () =>
                    {
                        var action = httpContext.Request.Query["action"].ToString();
                        this._logger.LogInformation(string.Format("Handling request: {0} with action {1}.", httpContext.Request.Path, action));
                        switch (action)
                        {
                            case "config":
                                await this.ConfigHandler(httpContext);
                                break;
                            case "uploadimage":
                                await this.UploadHandler(httpContext, new UploadOptions()
                                {
                                    AllowExtensions = this._configuration.Get<string[]>("imageAllowFiles"),
                                    PathFormat = this._configuration["imagePathFormat"],
                                    SizeLimit = this._configuration.Get<int>("imageMaxSize"),
                                    UploadFieldName = this._configuration["imageFieldName"]
                                });
                                break;
                            case "uploadscrawl":
                                await this.UploadHandler(httpContext, new UploadOptions()
                                {
                                    AllowExtensions = new string[] { ".png" },
                                    PathFormat = this._configuration["scrawlPathFormat"],
                                    SizeLimit = this._configuration.Get<int>("scrawlMaxSize"),
                                    UploadFieldName = this._configuration["scrawlFieldName"],
                                    Base64 = true,
                                    Base64Filename = "scrawl.png"
                                });
                                break;
                            case "uploadvideo":
                                await this.UploadHandler(httpContext, new UploadOptions()
                                {
                                    AllowExtensions = this._configuration.Get<string[]>("videoAllowFiles"),
                                    PathFormat = this._configuration["videoPathFormat"],
                                    SizeLimit = this._configuration.Get<int>("videoMaxSize"),
                                    UploadFieldName = this._configuration["videoFieldName"]
                                });
                                break;
                            case "uploadfile":
                                await this.UploadHandler(httpContext, new UploadOptions()
                                {
                                    AllowExtensions = this._configuration.Get<string[]>("fileAllowFiles"),
                                    PathFormat = this._configuration["filePathFormat"],
                                    SizeLimit = this._configuration.Get<int>("fileMaxSize"),
                                    UploadFieldName = this._configuration["fileFieldName"]
                                });
                                break;
                            case "listimage":
                                await ListFileHandler(httpContext, this._configuration["imageManagerListPath"], this._configuration.Get<string[]>("imageManagerAllowFiles"));
                                break;
                            case "listfile":
                                await ListFileHandler(httpContext, this._configuration["fileManagerListPath"], this._configuration.Get<string[]>("fileManagerAllowFiles"));
                                break;
                            case "catchimage":
                                await this.CrawlerHandler(httpContext);
                                break;
                            default:
                                await this.NotSupportedHandler(httpContext);
                                break;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex.Message, ex);
            }
            await _next(httpContext);
        }

    }

    public static class BaiduEditorUploadHandlerMiddlewareExtensions
    {

        public static IApplicationBuilder UseBaiduEditorUploadHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<BaiduEditorUploadHandlerMiddleware>();
        }

    }

}