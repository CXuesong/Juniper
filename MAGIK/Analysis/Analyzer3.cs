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
        private async Task<IEnumerable<KgNode[]>> FindHop3PathsAsync(KgNode node1, KgNode node2)
        {
            Debug.Assert(node1 != null);
            Debug.Assert(node2 != null);
            var paths = new List<KgNode[]>();
            return paths;
            /*
            await Task.WhenAll(ExploreAsync(node1), ExploreAsync(node2));

            // Id - Id - Id - Id
            // 先不考虑这种情况。

            // Id1 - Id3 - AA.AuId - Id2
            // 从 Id1 出发，探索所有可能的 Id3 。
            await Task.WhenAll(graph.AdjacentOutVertices(node1.Id)
                .Select(id => nodes[id])
                .OfType<PaperNode>()
                .Select(ExploreAsync));

            var id3Nodes = graph.AdjacentOutVertices(node1.Id);
            var id4PredecessorsDict = new Dictionary<long, List<long>>();
            foreach (var id3 in id3Nodes)
            {
                foreach (var id4 in id3Nodes)
                {
                    id4PredecessorsDict.GetOrCreate(id4).Add(id3);
                }
            }

            foreach (var id4 in graph.AdjacentInVertices(node2.Id))
            {
                
            }

            return paths;
            */
        }
    }

}
