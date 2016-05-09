//  Analyzer    1/2-hop

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
        private async Task<IReadOnlyCollection<KgNode[]>> FindHop12PathsAsync(KgNode node1, KgNode node2)
        {
            Debug.Assert(node1 != null);
            Debug.Assert(node2 != null);
            Logger.Magik.Enter(this, $"{node1} -> {node2}");
            await ExploreInterceptionNodesAsync(node1, node2);
            var paths = new List<KgNode[]>();
            var out1 = graph.AdjacentOutVertices(node1.Id);
            var in2 = graph.AdjacentInVertices(node2.Id);
            // 1-hop
            if (out1.Contains(node2.Id)) paths.Add(new[] {node1, node2});
            // 2-hop
            var commonNodes = out1.Intersect(in2);
            paths.AddRange(commonNodes.Select(cn => new[] {node1, nodes[cn], node2}));
            Logger.Magik.Success(this, "在 {0} - {1} 之间找到了 {2} 条 1/2-hop 路径。", node1.Id, node2.Id, paths.Count);
            Logger.Magik.Exit(this);
            return paths;
        }
    }
}
