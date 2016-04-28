using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    public partial class Analyzer
    {
        // 约定：使用 Graph 保存 Id 之间的连接关系。
        // 不要使用 DirectedGraph<KgNode> 。否则在试图根据 Id 查找节点信息时可能会遇到麻烦。
        private DirectedGraph<long> graph = new DirectedGraph<long>();

        // 保存已经发现的节点。
        private Dictionary<long, KgNode> nodes = new Dictionary<long, KgNode>();

        // 保存节点的可变状态。
        // 注意 graph 和 exploredNodes 集合可以变化，但集合中的每个项目
        // 例如以 long 表示的节点编号，和每个 KgNode 实例的内容是不可变的。
        // 因此，使用 status 映射处理这些可变状态。
        private Dictionary<long, NodeStatus> _Status = new Dictionary<long, NodeStatus>();

        /// <summary>
        /// 节点的状态标志。
        /// </summary>
        private enum NodeStatusFlag : byte
        {
            /// <summary>
            /// 节点未被探索。
            /// </summary>
            Unexplored = 0,
            /// <summary>
            /// 这是探索过程的中间状态。仅在 Explore 函数调用完成前，部分节点会位于此状态。
            /// </summary>
            Exploring,
            Explored
        }

        private class NodeStatus
        {
            // 一个节点被探索，当且仅当在向服务器提交查询，并能够获得此节点的完全信息之后。
            // 在 DEBUG 模式下，完全信息包括节点的名称和此节点的邻节点（保存在 Graph 中，
            //      但很可能不在 exploredNodes 中，因为这些邻节点还没有被探索）。
            // 在 RELEASE 模式下，完全信息仅包括节点的邻节点。
            public NodeStatusFlag Flag { get; set; }
        }

        /// <summary>
        /// 获取指定 Id 的节点状态。如果指定的 Id 目前不存在状态，则会新建一个。
        /// </summary>
        private NodeStatus GetStatus(long id)
        {
            NodeStatus s;
            if (!_Status.TryGetValue(id, out s))
            {
                s = new NodeStatus();
                _Status.Add(id, s);
            }
            return s;
        }

        /// <summary>
        /// 尝试注册一个处于“未探索”状态的节点。
        /// </summary>
        /// <returns>
        /// 如果此节点已经被注册，则返回 <c>false</c> 。
        /// </returns>
        private bool RegisterNode(KgNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            KgNode nn;
            if (nodes.TryGetValue(node.Id, out nn))
            {
                // 此节点已经被发现
                // 断言节点类型。
                if (nn.GetType() != node.GetType())
                    Logging.Warn(this, "试图注册的节点{0}与已注册的节点{1}具有不同的类型。", node, nn);
                return false;
            }
            else
            {
                nodes.Add(node.Id, node);
                graph.Add(node.Id);
                return true;
            }
        }

        /// <summary>
        /// 如果指定的节点尚未探索，则探索此节点。
        /// </summary>
        private Task ExploreAsync(KgNode node)
        {
            Debug.Assert(node != null);
            return ExploreAsync(node.Id);
        }

        /// <summary>
        /// 如果指定的节点尚未探索，则探索此节点。
        /// </summary>
        private async Task ExploreAsync(long id)
        {
            var s = GetStatus(id);
            lock (s)
            {
                if (s.Flag != NodeStatusFlag.Unexplored) return;
                s.Flag = NodeStatusFlag.Exploring;
            }
            var node = nodes[id];
            Logging.Enter(this, node);
            var adj = await node.GetAdjacentOutNodesAsync();
            // an: Adjacent Node
            foreach (var an in adj)
            {
                RegisterNode(an);
                graph.Add(id, an.Id);
            }
            s.Flag = NodeStatusFlag.Explored;
            Logging.Exit(this);
        }

        /// <summary>
        /// 根据指定的 Id ，获取论文或者作者节点。
        /// </summary>
        /// <returns>如果找不到符合要求的节点，则返回<c>null</c>。</returns>
        private async Task<KgNode> GetEntityOrAuthorNodeAsync(long id)
        {
            // 先尝试在节点缓存中查找。
            KgNode existing;
            if (nodes.TryGetValue(id, out existing))
                return existing;
            // 去网上找找。
            var er = await GlobalServices.ASClient.EvaluateAsync(SearchExpressionBuilder.EntityOrAuthorIdEquals(id), 2, 0);
            if (er.Entities.Length == 0) return null;
            var et = er.Entities[0];
            if (et.Id == id) return new PaperNode(et);
            var au = et.Authors.FirstOrDefault(a => a.Id == id);
            if (au == null)
            {
                Logging.Warn(this, $"查找Id/AuId {id} 时接收到了不正确的信息。");
                return null;
            }
            return new AuthorNode(au);
        }

        /// <summary>
        /// 根据给定的一对标识符所对应的对象，异步检索二者之间可能的路径。
        /// </summary>
        /// <param name="id1">源点对象标识符。此标识符可以是论文（Id）或作者（AA.AuId）。</param>
        /// <param name="id2">漏点对象标识符。此标识符可以是论文（Id）或作者（AA.AuId）。</param>
        /// <returns>
        /// 一个 Task 。其运行结果是一个集合（ICollection），集合中的每一个项目是一个数组，代表一条路径。
        /// 数组按照 id1 → id2 的顺序返回路径途经的所有节点。
        /// 合法的节点类型包括
        /// 论文（Id）, 研究领域（F.Fid）, 期刊（J.JId）, 会议（C.CId）, 作者（AA.AuId）, 组织（AA.AfId）。
        /// </returns>
        public async Task<ICollection<KgNode[]>> FindPathsAsync(long id1, long id2)
        {
            // 先找到实体/作者再说。
            var nodes = await Task.WhenAll(GetEntityOrAuthorNodeAsync(id1),
                GetEntityOrAuthorNodeAsync(id2));
            var node1 = nodes[0];
            var node2 = nodes[1];
            if (node1 == null) throw Utility.BuildIdNotFoundException(id1);
            if (node2 == null) throw Utility.BuildIdNotFoundException(id2);
            // 在图中注册节点。
            RegisterNode(node1);
            RegisterNode(node2);
            // 开始搜索。
            var hops = await Task.WhenAll(FindHop12PathsAsync(node1, node2), 
                FindHop3PathsAsync(node1, node2));
            // Possible multiple enumeration of IEnumerable
            // I don't care
            // 因为 FindHop12PathsAsync 和 FindHop3PathsAsync 返回的其实都是 List 。
            return hops.SelectMany(hop => hop).Distinct().ToArray();
        }

        /// <summary>
        /// 向 Trace 输出统计信息。
        /// </summary>
        public void TraceStatistics()
        {
            Trace.WriteLine($"缓存图：{graph.VerticesCount}个节点，{graph.EdgesCount}条边。");
        }

        /// <summary>
        /// 向 Trace 输出图的文本表示。注意，对于大规模的网络，此操作可能会非常慢。
        /// </summary>
        public void TraceGraph()
        {
            Trace.WriteLine(graph.Dump());
        }
    }
}
