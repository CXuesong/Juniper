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

        private static int ToInt32(this string expression)
        {
            return Convert.ToInt32(expression);
        }

        static Configurations()
        {
            var config = ConfigurationManager.AppSettings;
            BaseAddresses = config["BaseAddresses"]?.Split((char[]) null, StringSplitOptions.RemoveEmptyEntries)
                            ?? new[] {"http://localhost:9000/"};
            ASClientPagingSize = config["ASClient.PagingSize"]?.ToInt32()
                                 ?? 1000;
        }
    }
}
