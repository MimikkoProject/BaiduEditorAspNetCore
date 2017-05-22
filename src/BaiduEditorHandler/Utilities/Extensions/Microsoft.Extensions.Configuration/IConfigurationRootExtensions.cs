using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Configuration
{

    public static class IConfigurationRootExtensions
    {

        public static T Get<T>(this IConfigurationRoot config, string key)
        {
            if (typeof(T) == typeof(int))
            {
                var value = config[key];
                return (T)(object)SystemUtils.Try(() => int.Parse(value));
            }
            else if (typeof(T) == typeof(string[]))
            {
                return (T)(object)config.GetChildren().Select(x => x.Value).ToArray();
            }
            else
            {
                throw new NotSupportedException();
            }
        }

    }

}