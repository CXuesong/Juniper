﻿using System;
using System.Collections.Generic;
using System.Linq;
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
        private KgNodeCollection exploredNodes = new KgNodeCollection();

        // 保存节点的可变状态。
        // 注意 graph 和 exploredNodes 集合可以变化，但集合中的每个项目
        // 例如以 long 表示的节点编号，和每个 KgNode 实例的内容是不可变的。
        // 因此，使用 status 映射处理这些可变状态。
        private Dictionary<long, NodeStatus> status = new Dictionary<long, NodeStatus>();

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
        /// </returns>
        public async Task<ICollection<KgNode[]>> FindPathsAsync(long id1, long id2)
        {
            throw new NotImplementedException();
        }
    }
}
