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
        private async Task<IEnumerable<KgNode[]>> FindHop12PathsAsync(KgNode node1, KgNode node2)
        {
            Debug.Assert(node1 != null);
            Debug.Assert(node2 != null);
            await Task.WhenAll(ExploreAsync(node1.Id), ExploreAsync(node2.Id));
            var paths = new List<KgNode[]>();
            var out1 = graph.AdjacentOutVertices(node1.Id);
            var in2 = graph.AdjacentOutVertices(node2.Id);
            // 1-hop
            if (out1.Contains(node2.Id)) paths.Add(new[] {node1, node2});
            // 2-Hop
            var commonNodes = out1.Intersect(in2);
            paths.AddRange(commonNodes.Select(cn => new[] {node1, nodes[cn], node2}));
            return paths;
        }
    }
}
