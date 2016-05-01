using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    partial class Analyzer
    {
        /// <summary>
        /// 如果一篇文章的被引用次数超过此值，则认为文章被引用次数太多，需要调整搜索策略。
        /// </summary>
        public const int PAPER_BACKREFERENCE_THRESHOLD = 10000;

        private async Task<IEnumerable<KgNode[]>> FindHop12PathsAsync(KgNode node1, KgNode node2)
        {
            Debug.Assert(node1 != null);
            Debug.Assert(node2 != null);
            Logging.Enter(this, $"{node1} -> {node2}");
            await ExploreInterceptionNodesAsync(node1, node2);
            var paths = new List<KgNode[]>();
            var out1 = graph.AdjacentOutVertices(node1.Id);
            var in2 = graph.AdjacentInVertices(node2.Id);
            // 1-hop
            if (out1.Contains(node2.Id)) paths.Add(new[] {node1, node2});
            // 2-hop
            var commonNodes = out1.Intersect(in2);
            paths.AddRange(commonNodes.Select(cn => new[] {node1, nodes[cn], node2}));
            Logging.Success(this, "1-Hop & 2-Hop Paths: {0} .", paths.Count);
            Logging.Exit(this);
            return paths;
        }
    }
}
