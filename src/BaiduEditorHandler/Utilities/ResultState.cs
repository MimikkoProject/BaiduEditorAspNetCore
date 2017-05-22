using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiduEditorHandler.Utilities
{

    public enum ResultState
    {
        Success,
        InvalidParam,
        AuthorizError,
        IOError,
        PathNotFound
    }

}