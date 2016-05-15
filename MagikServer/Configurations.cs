using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
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

        public static bool ASClientUseUltimateKey { get; }

        /// <summary>
        /// 学术搜索客户端的分页记录数量。
        /// </summary>
        public static int ASClientPagingSize { get; }

        /// <summary>
        /// 学术搜索客户端的最大允许并行分页数量。
        /// </summary>
        public static int ASClientConcurrentPagingCount { get; }

        private static int ToInt32(this string expression)
        {
            return Convert.ToInt32(expression);
        }

        private static bool ToBoolean(this string expression)
        {
            return Convert.ToBoolean(expression);
        }

        private static TimeSpan ToTimeSpan(this string expression)
        {
            return TimeSpan.Parse(expression);
        }

        /// <summary>
        /// 输出当前的设置。
        /// </summary>
        public static string DumpConfigurations()
        {
            var sb = new StringBuilder();
            if (Environment.Is64BitProcess) sb.AppendLine("64位进程。");
            if (GCSettings.IsServerGC) sb.AppendLine("服务器GC已启用。");
            sb.AppendLine("当前路径： " + Directory.GetCurrentDirectory());
            foreach (var p in typeof(Configurations).GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                var value = p.GetValue(null);
                var s = value.ToString();
                var enumerable = value as IEnumerable;
                if (enumerable != null) s = string.Join(",", enumerable.Cast<object>());
                sb.AppendFormat("{0,40} = {1}\n", p.Name, s);
            }
            return sb.ToString();
        }

        static Configurations()
        {
            var config = ConfigurationManager.AppSettings;
            BaseAddresses = config["BaseAddresses"]?.Split((char[]) null, StringSplitOptions.RemoveEmptyEntries)
                            ?? new[] {"http://localhost:9000/"};
            ASClientPagingSize = config["ASClient.PagingSize"]?.ToInt32()
                                 ?? 1000;
            ASClientConcurrentPagingCount = config["ASClient.ConcurrentPagingCount"]?.ToInt32()
                                               ?? 10;
            ASClientUseUltimateKey = config["ASClient.UseUltimateKey"]?.ToBoolean()
                                     ?? true;
        }
    }
}
