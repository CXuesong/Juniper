//  Analyzer    3-hop

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
            Logger.Magik.Enter(this, $"{node1} -> {node2}");
            // Notation
            // Node1 -- Node3 -- Node4 -- Node2
            var paths = new List<KgNode[]>();
            var paper1 = node1 as PaperNode;
            var author1 = node1 as AuthorNode;
            // 探索 node1
            await LocalExploreAsync(node1);
            if (paper1 != null)
            {
                // 手动探索 node1 之后的所有节点。
                var nodes3 = graph.AdjacentOutVertices(node1.Id)
                    .Select(id => nodes[id])
                    .ToArray();
                await ExploreInterceptionNodesAsync(nodes3, node2);
            }
            else
            {
                // 在 FindPathsAsync 中应该已经可以保证 node1 是论文或作者 。
                Debug.Assert(author1 != null);
                await ExploreAuthorsPapersAsync(new[] {author1});
            }
            // 从 Id1 出发，探索所有可能的 Id3 。
            var id4PredecessorsDict = new Dictionary<long, List<long>>();
            foreach (var id3 in graph.AdjacentOutVertices(node1.Id))
            {
                foreach (var id4 in graph.AdjacentOutVertices(id3))
                    id4PredecessorsDict.GetOrAdd(id4).Add(id3);
            }
            foreach (var id4 in graph.AdjacentInVertices(node2.Id))
            {
                var id3Nodes = id4PredecessorsDict.TryGetValue(id4);
                if (id3Nodes != null)
                {
                    // 有路径！
                    paths.AddRange(id3Nodes.Select(id3 =>
                        new[] {node1, nodes[id3], nodes[id4], node2}));
                }
            }
            Logger.Magik.Success(this, "在 {0} - {1} 之间找到了 {2} 条 3-hop 路径。", node1.Id, node2.Id, paths.Count);
            Logger.Magik.Exit(this);
            return paths;
        }
    }

}
