using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiduEditorHandler.Utilities
{

    public enum UploadState
        : int
    {
        Success = 0,
        SizeLimitExceed = -1,
        TypeNotAllow = -2,
        FileAccessError = -3,
        NetworkError = -4,
        Unknown = 1,
    }

}