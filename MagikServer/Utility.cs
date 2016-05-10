using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
