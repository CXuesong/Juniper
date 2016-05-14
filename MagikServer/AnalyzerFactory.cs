using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer
{
    /// <summary>
    /// 用于提供粗粒度的缓存功能。
    /// </summary>
    public static class AnalyzerFactory
    {
        //private static ConcurrentQueue<Analyzer> _Analyzers;

        // 参考
        //  AnalyzerTestMethod5616: 78,093
        //  AnalyzerTestMethod62110: 1,165,014
        // 我们认为一个节点大约占用 1KB 的内存。
        /// <summary>
        /// 我们认为缓存节点数量超过此数量级的 Analyzer 规模很大，因此不适合缓存。
        /// </summary>
        public const int LargeAnalyzerNodesThreshold = 110000;

        /// <summary>
        /// 在此实现中，直接构造一个 <see cref="Analyzer"/> 并返回。
        /// </summary>
        public static Analyzer GetAnalyzer()
        {
            var client = GlobalServices.CreateASClient();
            var analyzer = new Analyzer(client);
            return analyzer;
        }

        /// <summary>
        /// 在此实现中，不做任何事情。
        /// </summary>
        public static void PutAnalyzer(Analyzer analyzer)
        {
            //if (analyzer.CachedNodesCount < LargeAnalyzerNodesThreshold)
        }

        /// <summary>
        /// 重置缓存。
        /// </summary>
        public static void PurgeCache()
        {

        }
    }
}
