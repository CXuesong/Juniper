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
        public const int PAPER_BACKREFERENCE_THRESHOLD = 100000;

        private async Task<IEnumerable<KgNode[]>> FindHop12PathsAsync(KgNode node1, KgNode node2)
        {
            Debug.Assert(node1 != null);
            Debug.Assert(node2 != null);
            await Task.WhenAll(ExploreAsync(node1), ExploreAsync(node2));
            var paper1 = node1 as PaperNode;
            var paper2 = node2 as PaperNode;
            if (paper1 != null && paper2 != null)
            {
                // 照顾一下 Id1 - Idr - Id2 的情况
                // 需要决定探索方向。
                await ExplorePaperBackReferencesAsync(node2.Id);
                if (GetStatus(node2.Id).PaperBackReferenceExplorationStatus == ExplorationStatus.DoNotExplore)
                {
                    // Id2 论文有点火。
                    // 只好从 Id1 向 Id2 探索。注意，我们仅从 Id1 探索论文。
                    await Task.WhenAll(graph.AdjacentOutVertices(node1.Id)
                        .Select(id => nodes[id])
                        .OfType<PaperNode>()
                        .Select(ExploreAsync));
                }
                // 否则，Id2 被引用次数比较少，可以从 Id2 反向推 Idr 。
                // 此工作已经由 ExplorePaperBackReferencesAsync 做完了。
            }
            var paths = new List<KgNode[]>();
            var out1 = graph.AdjacentOutVertices(node1.Id);
            var in2 = graph.AdjacentInVertices(node2.Id);
            // 1-hop
            if (out1.Contains(node2.Id)) paths.Add(new[] {node1, node2});
            // 2-hop
            var commonNodes = out1.Intersect(in2);
            paths.AddRange(commonNodes.Select(cn => new[] {node1, nodes[cn], node2}));
            return paths;
        }
    }
}
