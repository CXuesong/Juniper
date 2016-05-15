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
        private async Task<IReadOnlyCollection<KgNode[]>> FindHop3PathsAsync(KgNode node1, KgNode node2)
        {
            Debug.Assert(node1 != null);
            Debug.Assert(node2 != null);
            Logger.Magik.Enter(this, $"{node1} -> {node2}");
            // Notation
            // Node1 -- Node3 -- Node4 -- Node2
            var paths = new List<KgNode[]>();
            var author1 = node1 as AuthorNode;
            if (author1 != null)
            { 
                // Author 还需要补刀。
                await FetchAuthorsPapersAsync(new[] {author1});
            }
            // 获取 Node1 出发所有可能的 Node3
            var nodes3 = graph.AdjacentOutVertices(node1.Id)
                .Select(id => nodes[id])
                .ToArray();
            // 探索 Node4
            await ExploreInterceptionNodesAsync(nodes3, node2);
            // 计算路径。
            // 从 Id1 出发，寻找所有可能的 Id3 。
            var id4PredecessorsDict = new Dictionary<long, List<long>>();
            foreach (var id3 in graph.AdjacentOutVertices(node1.Id))
            {
                foreach (var id4 in graph.AdjacentOutVertices(id3))
                    id4PredecessorsDict.GetOrAddNew(id4).Add(id3);
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
            Logger.Magik.Trace(this, "在 {0} - {1} 之间找到了 {2} 条 3-hop 路径。", node1.Id, node2.Id, paths.Count);
            Logger.Magik.Exit(this);
            return paths;
        }
    }

}
