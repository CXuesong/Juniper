using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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

        private class NodeStatus
        {
            // 一个节点被探索，当且仅当在向服务器提交查询，并能够获得此节点的完全信息之后。
            // 在 DEBUG 模式下，完全信息包括节点的名称和此节点的邻节点（保存在 Graph 中，
            //      但很可能不在 exploredNodes 中，因为这些邻节点还没有被探索）。
            // 在 RELEASE 模式下，完全信息仅包括节点的邻节点。
            public bool Explored { get; set; }
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
            throw new NotImplementedException();
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
        /// 如果指定的节点尚未探索，则探索此节点。
        /// </summary>
        private async Task Explore(long id)
        {
            var s = GetStatus(id);
            if (s.Explored) return;
            var node = nodes[id];
            Logging.Enter(this, node);
            var adj = await node.GetAdjacentOutNodesAsync();
            foreach (var an in adj)
            {
                KgNode nn;
                if (nodes.TryGetValue(an.Id, out nn))
                {
                    // 此节点已经被发现
                    // 断言节点类型。
                    if (nn.GetType() != an.GetType())
                        Logging.Warn(this, "新发现的节点{0}与已注册的节点{1}具有不同的类型。", an, nn);
                }
                else
                {
                    nodes.Add(an.Id, an);
                }
                graph.Add(id, an.Id);
            }
            s.Explored = true;
            Logging.Exit(this);
        }
    }
}
