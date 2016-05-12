using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Runtime;
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
        public static TimeSpan AnalyzerCacheTimeout { get; }

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
        /// 向控制台输出当前的设置。
        /// </summary>
        public static void PrintConfigurations()
        {
            if (Environment.Is64BitProcess) Console.WriteLine("64位进程。");
            if (GCSettings.IsServerGC) Console.WriteLine("服务器GC已启用。");
            foreach (var p in typeof(Configurations).GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                Console.WriteLine("{0,40} = {1}", p.Name, p.GetValue(null));
            }
        }

        static Configurations()
        {
            var config = ConfigurationManager.AppSettings;
            BaseAddresses = config["BaseAddresses"]?.Split((char[]) null, StringSplitOptions.RemoveEmptyEntries)
                            ?? new[] {"http://localhost:9000/"};
            ASClientPagingSize = config["ASClient.PagingSize"]?.ToInt32()
                                 ?? 1000;
            AnalyzerCacheTimeout = config["Analyzer.CacheTimeout"]?.ToTimeSpan()
                                   ?? TimeSpan.Zero;
        }
    }
}
