﻿//  Analyzer    公共接口、数据类型和存储

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;
using Microsoft.Contests.Bop.Participants.Magik.Academic.Contract;
using SEB = Microsoft.Contests.Bop.Participants.Magik.Academic.SearchExpressionBuilder;

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    public partial class Analyzer
    {
        // 约定：使用 Graph 保存 Id 之间的连接关系。
        // 不要使用 DirectedGraph<KgNode> 。否则在试图根据 Id 查找节点信息时可能会遇到麻烦。
        private readonly DirectedGraph<long> graph = new DirectedGraph<long>();

        // 保存已经发现的节点。
        private readonly ConcurrentDictionary<long, KgNode> nodes = new ConcurrentDictionary<long, KgNode>();

        // 保存节点的可变状态。
        // 注意 graph 和 exploredNodes 集合可以变化，但集合中的每个项目
        // 例如以 long 表示的节点编号，和每个 KgNode 实例的内容是不可变的。
        // 因此，使用 status 映射处理这些可变状态。
        private readonly ConcurrentDictionary<long, NodeStatus> status = new ConcurrentDictionary<long, NodeStatus>();

        private readonly AcademicSearchClient asClient;

        public Analyzer(AcademicSearchClient asClient)
        {
            if (asClient == null) throw new ArgumentNullException(nameof(asClient));
            this.asClient = asClient;
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
        public async Task<KgNode[][]> FindPathsAsync(long id1, long id2)
        {
            Logger.Magik.Enter(this, $"{id1} -> {id2}");
            var sw = Stopwatch.StartNew();
            // 先找到实体/作者再说。
            var nodes = await Task.WhenAll(GetEntityOrAuthorNodeAsync(id1),
                GetEntityOrAuthorNodeAsync(id2));
            var node1 = nodes[0];
            var node2 = nodes[1];
            if (node1 == null) throw new ArgumentException($"在 MAG 中找不到指定的 Id/AuId：{id1}。", nameof(id1));
            if (node2 == null) throw new ArgumentException($"在 MAG 中找不到指定的 Id/AuId：{id2}。", nameof(id2));
            // 在图中注册节点。
            RegisterNode(node1);
            RegisterNode(node2);
            // 开始搜索。
            var hops = await Task.WhenAll(FindHop12PathsAsync(node1, node2),
                FindHop3PathsAsync(node1, node2));
            // Possible multiple enumeration of IEnumerable
            // I don't care
            // 因为 FindHop12PathsAsync 和 FindHop3PathsAsync 返回的其实都是 List 。
            var result = hops.SelectMany(hop => hop)
                .Distinct(new ArrayEqualityComparer<KgNode>(KgNodeEqualityComparer.Default))
                .ToArray();
            Logger.Magik.Success(this, "在 {0} - {1} 之间找到了 {2} 条路径。用时： {3} 。", node1, node2, result.Length, sw.Elapsed);
            Logger.Magik.Exit(this);
            //var xxx = await FindHop3PathsAsync(node1, node2);
            //var x = graph.Contains(1982462162, 2139873966);
            //var x1 = graph.Contains(2139873966, 2122841972);
            //var x2 = graph.Contains(2122841972, 676500258);
            return result;
        }

        /// <summary>
        /// 获取调用统计信息。
        /// </summary>
        public string DumpStatistics()
        {
            return $"缓存图：{graph.VerticesCount}个节点，{graph.EdgesCount}条边。";
        }


        /// <summary>
        /// 向日志输出调用统计信息。
        /// </summary>
        public void LogStatistics()
        {
            Logger.Magik.Info(this, DumpStatistics());
            Logger.Magik.Info(asClient, asClient.DumpStatistics());
            Logger.Magik.Info(this, "缓存图： {0} 个节点， {1} 条边。", graph.VerticesCount, graph.EdgesCount);
        }

        /// <summary>
        /// 向 Trace 输出图的文本表示。注意，对于大规模的网络，此操作可能会非常慢。
        /// </summary>
        public void TraceGraph()
        {
            Trace.WriteLine(graph.Dump());
        }

        /// <summary>
        /// 节点的状态标志。
        /// </summary>
        private enum ExplorationStatus : byte
        {
            /// <summary>
            /// 节点未被探索。
            /// </summary>
            Unexplored = 0,
            /// <summary>
            /// 这是探索过程的中间状态。仅在 Explore 函数调用完成前，部分节点会位于此状态。
            /// </summary>
            Exploring = 1,
            /// <summary>
            /// 局部相关连接已经探索完毕。
            /// </summary>
            Explored = 2,
        }

        private class NodeStatus
        {
            /// <summary>
            /// 节点的基础本地信息的探索情况。
            /// </summary>
            public static readonly object LocalExploration = new NamedObject("LocalExploration");
            /// <summary>
            /// 作者所写的所有论文的探索情况。
            /// </summary>
            public static readonly object AuthorPapersExploration = new NamedObject("AuthorPapersExploration");

            // 一个节点被探索，当且仅当在向服务器提交查询，并能够获得此节点的完全信息之后。
            // 在 DEBUG 模式下，完全信息包括节点的名称和此节点的邻节点（保存在 Graph 中，
            //      但很可能不在 exploredNodes 中，因为这些邻节点还没有被探索）。
            // 在 RELEASE 模式下，完全信息仅包括节点的邻节点。
            private Dictionary<object, ExplorationStatus> explorationStatusDict 
                = new Dictionary<object, ExplorationStatus>();
            private Dictionary<object, TaskCompletionSource<bool>> explorationTaskCompletionSourceDict
                = new Dictionary<object, TaskCompletionSource<bool>>();
            private ReaderWriterLockSlim syncLock = new ReaderWriterLockSlim();
            
            /// <summary>
            /// 如果当前节点处于 <see cref="ExplorationStatus.Unexplored"/> 状态，
            /// 则尝试将节点置于 <see cref="ExplorationStatus.Exploring" /> 状态。
            /// </summary>
            /// <param name="domainKey">
            /// 对此节点注册一个标志。用于表示此节点的某些关联
            /// （如作者的所有论文，或是一篇文章的所有引用）
            /// 已经被探索。
            /// </param>
            /// <returns>
            /// 如果成功将节点置于 <see cref="ExplorationStatus.Exploring" /> 状态，
            /// 则返回 true 。指示当前线程应当开始探索对应的节点。
            /// </returns>
            public bool TryMarkAsExploring(object domainKey)
            {
                if (domainKey == null) throw new ArgumentNullException(nameof(domainKey));
                syncLock.EnterWriteLock();
                try
                {
                    ExplorationStatus s;
                    if (explorationStatusDict.TryGetValue(domainKey, out s))
                    {
                        if (s != ExplorationStatus.Unexplored)
                            return false;
                    }
                    explorationStatusDict[domainKey] = ExplorationStatus.Exploring;
                    return true;
                }
                finally
                {
                    syncLock.ExitWriteLock();
                }
            }

            /// <summary>
            /// 将当前节点标注为 <see cref="ExplorationStatus.Exploring" /> 状态。
            /// 如果当前节点已经处于 <see cref="ExplorationStatus.Exploring" /> 状态，
            /// 则会等待直到节点处于 <see cref="ExplorationStatus.Explored" /> 状态。
            /// </summary>
            /// <param name="domainKey"></param>
            /// <returns>
            /// 如果成功将节点置于 <see cref="ExplorationStatus.Exploring" /> 状态，
            /// 则任务会返回 true 。指示当前线程应当开始探索对应的节点。否则，如果当前节点
            /// 已经被探索，或正在探索，则任务会在探索结束后返回 false 。
            /// </returns>
            public Task<bool> MarkAsExploringOrUntilExplored(object domainKey)
            {
                if (domainKey == null) throw new ArgumentNullException(nameof(domainKey));
                syncLock.EnterWriteLock();
                try
                {
                    ExplorationStatus s;
                    if (explorationStatusDict.TryGetValue(domainKey, out s))
                    {
                        switch (s)
                        {
                            case ExplorationStatus.Unexplored:
                                break;
                            case ExplorationStatus.Explored:
                                return Task.FromResult(false);
                            case ExplorationStatus.Exploring:
                                // Wait for exploration
                                var tcs = explorationTaskCompletionSourceDict.GetOrAdd(domainKey);
                                return tcs.Task;
                        }
                    }
                    explorationStatusDict[domainKey] = ExplorationStatus.Exploring;
                    return Task.FromResult(true);
                }
                finally 
                {
                    syncLock.ExitWriteLock();
                }
            }

            /// <summary>
            /// 如果当前节点处于 <see cref="ExplorationStatus.Exploring"/> 状态，
            /// 则尝试将节点置于 <see cref="ExplorationStatus.Explored" /> 状态。
            /// </summary>
            public void MarkAsExplored(object domainKey)
            {
                if (domainKey == null) throw new ArgumentNullException(nameof(domainKey));
                syncLock.EnterWriteLock();
                try
                {
#if DEBUG
                    //Debug.Assert(explorationStatusDict[domainKey] == ExplorationStatus.Exploring);
#endif
                    explorationStatusDict[domainKey] = ExplorationStatus.Explored;
                    var tcs = explorationTaskCompletionSourceDict.TryGetValue(domainKey);
                    if (tcs != null)
                    {
                        // 为什么是 false ？ 参阅 MarkAsExploringOrUntilExplored 的返回值。
                        tcs.SetResult(false);
                        explorationTaskCompletionSourceDict.Remove(domainKey);
                    }
                }
                finally
                {
                    syncLock.ExitWriteLock();
                }
            }

            public ExplorationStatus GetExplorationStatus(object domainKey)
            {
                if (domainKey == null) throw new ArgumentNullException(nameof(domainKey));
                syncLock.EnterReadLock();
                try
                {
                    ExplorationStatus s;
                    if (explorationStatusDict.TryGetValue(domainKey, out s))
                    {
                        return s;
                    }
                    return ExplorationStatus.Unexplored;
                }
                finally
                {
                    syncLock.ExitReadLock();
                }
            }

            ~NodeStatus()
            {
                syncLock?.Dispose();
            }
        }
    }
}
