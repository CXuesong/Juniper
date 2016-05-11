using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer
{
    internal static class Utility
    {
        public const string JsonMediaType = "application/json";

        private static Analyzer cachedAnalyzer;
        private static DateTime cachedAnalyzerCreationTime = DateTime.MinValue;

        /// <summary>
        /// 根据 <see cref="Configurations.AnalyzerCacheAllowed"/> ，决定
        /// 是创建新的 Analyzer ，还是返回一个已有的 Analyzer 。
        /// </summary>
        public static Analyzer GetAnalyzer()
        {
            // TODO 检查线程安全性。
            var analyzer = cachedAnalyzer;
            if (cachedAnalyzer == null 
                || !Configurations.AnalyzerCacheAllowed
                || DateTime.Now - cachedAnalyzerCreationTime > TimeSpan.FromMinutes(30))
            {
                analyzer = new Analyzer(GlobalServices.CreateASClient());
                analyzer.SearchClient.PagingSize = Configurations.ASClientPagingSize;
                cachedAnalyzer = analyzer;
                cachedAnalyzerCreationTime = DateTime.Now;
            }
            return analyzer;
        }

        public static void PurgeAnalyzer()
        {
            cachedAnalyzer = null;
        }

        private static string _ApplicationTitle;
        private static string _ProductName;
        private static Version _ProductVersion;

        public static string ApplicationTitle
        {
            get
            {
                if (_ApplicationTitle == null)
                {
                    var titleAttribute = typeof(Utility).Assembly.GetCustomAttribute<AssemblyTitleAttribute>();
                    _ApplicationTitle = titleAttribute != null ? titleAttribute.Title : "";
                }
                return _ApplicationTitle;
            }
        }

        public static string ProductName
        {
            get
            {
                if (_ProductName == null)
                {
                    var productAttribute = typeof(Utility).Assembly.GetCustomAttribute<AssemblyProductAttribute>();
                    _ProductName = productAttribute != null ? productAttribute.Product : "";
                }
                return _ProductName;
            }
        }

        public static Version ProductVersion
        {
            get
            {
                if (_ProductVersion == null) _ProductVersion = typeof(Utility).Assembly.GetName().Version;
                return _ProductVersion;
            }
        }
    }
}
