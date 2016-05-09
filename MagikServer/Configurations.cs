using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer
{
    /// <summary>
    /// 用于缓存全局设置。
    /// </summary>
    internal static class Configurations
    {
        /// <summary>
        /// 服务器的基地址。
        /// </summary>
        public static string[] BaseAddresses { get; }

        /// <summary>
        /// 学术搜索客户端的分页记录数量。
        /// </summary>
        public static int ASClientPagingSize { get; }

        /// <summary>
        /// 是否允许在请求之间缓存网络图。
        /// </summary>
        public static bool AnalyzerCacheAllowed { get; }

        private static int ToInt32(this string expression)
        {
            return Convert.ToInt32(expression);
        }

        private static bool ToBoolean(this string expression)
        {
            return Convert.ToBoolean(expression);
        }

        static Configurations()
        {
            var config = ConfigurationManager.AppSettings;
            BaseAddresses = config["BaseAddresses"]?.Split((char[]) null, StringSplitOptions.RemoveEmptyEntries)
                            ?? new[] {"http://localhost:9000/"};
            ASClientPagingSize = config["ASClient.PagingSize"]?.ToInt32()
                                 ?? 1000;
            AnalyzerCacheAllowed = config["Analyzer.CacheAllowed"]?.ToBoolean()
                                   ?? false;
        }
    }
}
