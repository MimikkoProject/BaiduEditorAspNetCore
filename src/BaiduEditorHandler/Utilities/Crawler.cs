using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.PlatformAbstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Net.Http;

namespace BaiduEditorHandler.Utilities
{

    public class Crawler
    {

        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IConfigurationRoot _configuration;

        public string SourceUrl { get; set; }

        public string ServerUrl { get; set; }

        public string State { get; set; }

        public Crawler(IHostingEnvironment hostingEnvironment, IConfigurationRoot configuration, string sourceUrl)
        {
            this._hostingEnvironment = hostingEnvironment;
            this._configuration = configuration;
            this.SourceUrl = sourceUrl;
        }

        public async Task<Crawler> FetchAsync()
        {
            using (var hc = new HttpClient())
            {
                using (var response = await hc.GetAsync(this.SourceUrl))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        this.State = "Url returns " + response.StatusCode + ", " + response.ReasonPhrase;
                        return this;
                    }
                    if (response.Content.Headers.ContentType.MediaType.IndexOf("image") == -1)
                    {
                        this.State = "Url is not an image";
                        return this;
                    }
                    var rootPath = this._hostingEnvironment.WebRootPath;
                    this.ServerUrl = PathFormatter.Format(Path.GetFileName(this.SourceUrl), this._configuration["catcherPathFormat"]);
                    var savePath = Path.Combine(rootPath, ServerUrl);
                    if (!Directory.Exists(Path.GetDirectoryName(savePath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                    }
                    try
                    {
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            using (var fs = new FileStream(savePath, FileMode.OpenOrCreate))
                            {
                                stream.CopyTo(fs);
                                fs.Flush();
                                this.State = "SUCCESS";
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        this.State = "Fetch failed：" + e.Message;
                    }
                    return this;
                }
            }
        }

    }

}