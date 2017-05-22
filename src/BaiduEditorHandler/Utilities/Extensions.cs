using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BaiduEditorHandler.Utilities
{

    public static class Extensions
    {

        public static string ToStateMessage(this UploadState state)
        {
            switch (state)
            {
                case UploadState.Success:
                    return "SUCCESS";
                case UploadState.FileAccessError:
                    return "File access error, please check if you have enough rights.";
                case UploadState.SizeLimitExceed:
                    return "The file size exceeds the server limits.";
                case UploadState.TypeNotAllow:
                    return "The file format is not allowed on this server.";
                case UploadState.NetworkError:
                    return "Network error.";
            }
            return "Unknown error.";
        }

        public static string ToStateMessage(this ResultState state)
        {
            switch (state)
            {
                case ResultState.Success:
                    return "SUCCESS";
                case ResultState.InvalidParam:
                    return "Invalid parameter.";
                case ResultState.PathNotFound:
                    return "Path not found.";
                case ResultState.AuthorizError:
                    return "Insufficient privilege for file system. ";
                case ResultState.IOError:
                    return "File system read error.";
            }
            return "Unknown error.";
        }

        public static bool CheckFileType(this UploadOptions options, string filename)
        {
            var fileExtension = Path.GetExtension(filename).ToLower();
            return options.AllowExtensions.Select(x => x.ToLower()).Contains(fileExtension);
        }

        public static bool CheckFileSize(this UploadOptions options, long size)
        {
            return size < options.SizeLimit;
        }

    }

}