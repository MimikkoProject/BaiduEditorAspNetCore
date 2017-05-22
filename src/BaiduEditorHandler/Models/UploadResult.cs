using BaiduEditorHandler.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiduEditorHandler.Models
{

    public class UploadResult
    {

        public UploadState State { get; set; }

        public string Url { get; set; }

        public string OriginFileName { get; set; }

        public string ErrorMessage { get; set; }

    }

}