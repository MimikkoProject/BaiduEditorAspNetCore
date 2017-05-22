using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiduEditorHandler.Utilities
{

    internal static class IFormFileExtensions
    {

        public static string GetFileName(this IFormFile file)
        {
            var fileName = ContentDispositionHeaderValue
                .Parse(file.ContentDisposition)
                .FileName
                .Trim('"');// FileName returns "fileName.ext"(with double quotes) in beta 3
            return fileName;
        }

    }

}