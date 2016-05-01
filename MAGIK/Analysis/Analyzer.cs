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
        private DirectedGraph<long> graph = new DirectedGraph<long>();

        // 保存已经发现的节点。
        private ConcurrentDictionary<long, KgNode> nodes = new ConcurrentDictionary<long, KgNode>();

        // 保存节点的可变状态。
        // 注意 graph 和 exploredNodes 集合可以变化，但集合中的每个项目
        // 例如以 long 表示的节点编号，和每个 KgNode 实例的内容是不可变的。
        // 因此，使用 status 映射处理这些可变状态。
        private ConcurrentDictionary<long, NodeStatus> status = new ConcurrentDictionary<long, NodeStatus>();

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
            public bool MarkAsExploring(object domainKey)
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

        /// <summary>
        /// 获取指定 Id 的节点状态。如果指定的 Id 目前不存在状态，则会新建一个。
        /// </summary>
        private NodeStatus GetStatus(long id)
        {
            Debug.Assert(graph.Vertices.Contains(id));
            return status.GetOrAdd(id, i => new NodeStatus());
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
            var nn = nodes.GetOrAdd(node.Id, node);
            if (nn != node)
            {
                // 此节点已经被发现
                // 断言节点类型。
                if (nn.GetType() != node.GetType())
                    Logging.Warn(this, "试图注册的节点{0}与已注册的节点{1}具有不同的类型。", node, nn);
                return false;
            }
            else
            {
                graph.Add(node.Id);
                return true;
            }
        }

        /// <summary>
        /// 注册一条边。
        /// 注意，如果是单向边，则必定是 node1 --&gt; node2 。
        /// </summary>
        private void RegisterEdge(long id1, long id2, bool biDirectional)
        {
            graph.Add(id1, id2);
            if (biDirectional) graph.Add(id2, id1);
        }

        /// <summary>
        /// 如果指定的节点尚未探索，则探索此节点。
        /// 如果其他线程正在探索此节点，则等待此节点探索完毕。
        /// </summary>
        private async Task LocalExploreAsync(KgNode node)
        {
            Debug.Assert(node != null);
            var s = GetStatus(node.Id);
            if (!await s.MarkAsExploringOrUntilExplored(NodeStatus.LocalExploration))
                return;
            Logging.Enter(this, node);
            var newlyDiscoveredNodes = 0;
            var adj = await node.GetAdjacentNodesAsync();
            // an: Adjacent Node
            foreach (var an in adj)
            {
                if (RegisterNode(an)) newlyDiscoveredNodes++;
                RegisterEdge(node.Id, an.Id, !(an is PaperNode));
            }
            s.MarkAsExplored(NodeStatus.LocalExploration);
            Logging.Exit(this, $"{newlyDiscoveredNodes} new nodes");
        }

        /// <summary>
        /// 同时探索多个节点。适用于文章节点。
        /// </summary>
        private async Task LocalExploreAsync(IEnumerable<PaperNode> nodes)
        {
            Debug.Assert(nodes != null);
            var nodesCollection = nodes as ICollection<PaperNode> ?? nodes.ToArray();
            if (nodesCollection.Count == 0) return;
            Logging.Enter(this, $"[{nodesCollection.Count} nodes]");
            var newlyDiscoveredNodes = 0;
            try
            {
                var nodesToExplore = nodesCollection
                    .Where(n => GetStatus(n.Id).MarkAsExploring(NodeStatus.LocalExploration))
                    .ToArray();
                if (nodesToExplore.Length == 0) return;
                // 区分“已经下载详细信息”的实体和“需要下载详细信息”的实体。
                var nodesToFetch = nodesToExplore.Where(n => n.IsStub);
                var nodesFetched = nodesToExplore.Where(n => !n.IsStub);
                var fetchTasks = nodesToFetch.Select(n => n.Id)
                    .Partition(SEB.MaxChainedIdCount)
                    .Select(async ids =>
                    {
                        // 假定 Partition 返回的是 IList / ICollection
                        var idc = (ICollection<long>) ids;
                        var er = await GlobalServices.ASClient.EvaluateAsync(
                            SEB.EntityIdIn(idc),
                            SEB.MaxChainedIdCount);
                        if (er.Entities.Count < idc.Count)
                            Logging.Warn(this, "批量查询实体 Id 时，返回结果数量不足。期望：{0}，实际：{1}。", idc.Count, er.Entities.Count);
                        return er.Entities.Select(et => new PaperNode(et));
                    }).ToArray();   // 先让网络通信启动起来。
                Func<PaperNode, Task> explore = async paperNode =>
                {
                    //Debug.Assert(this.nodes.ContainsKey(paperNode.Id));
                    var adj = await paperNode.GetAdjacentNodesAsync();
                    // an: Adjacent Node
                    foreach (var an in adj)
                    {
                        if (RegisterNode(an)) newlyDiscoveredNodes++;
                        RegisterEdge(paperNode.Id, an.Id, !(an is PaperNode));
                    }
                    // 标记为“已经探索过”。
                    GetStatus(paperNode.Id).MarkAsExplored(NodeStatus.LocalExploration);
                };
                //然后处理这些已经在本地的节点。
                await Task.WhenAll(nodesFetched.Select(explore));
                //最后处理刚刚下载下来的节点。
                await Task.WhenAll((await Task.WhenAll(fetchTasks))
                    .SelectMany(papers => papers)
                    .Select(explore));
            }
            finally
            {
                Logging.Exit(this, $"{newlyDiscoveredNodes} new nodes");
            }
        }

        /// <summary>
        /// 异步探索作者的所有论文，顺便探索他/她在发表这些论文时所位于的机构。
        /// 如果其他线程正在探索此节点，则等待此节点探索完毕。
        /// </summary>
        private async Task ExploreAuthorPapersAsync(AuthorNode author)
        {
            // 探索 author 的所有论文。此处的探索还可以顺便确定 author 的所有组织。
            if (!await GetStatus(author.Id).MarkAsExploringOrUntilExplored(NodeStatus.AuthorPapersExploration))
                return;
            foreach (var paper in await author.GetPapersAsync())
            {
                // AA.AuId1 <-> Id
                RegisterNode(paper);
                // 此处还可以注册 paper 的所有作者。
                // 这样做的好处是，万一 author1 和 author2 同时写了一篇论文。
                // 在这里就可以发现了。
                await LocalExploreAsync(paper);
                // 为作者 AA.AuId1 注册所有可能的机构。
                // 这里比较麻烦，因为一个作者可以属于多个机构，所以
                // 不能使用 LocalExploreAsync （会认为已经探索过。）
                foreach (var pa in paper.Authors)
                {
                    // AA.AuId <-> AA.AfId
                    // 其中必定包括
                    // AA.AuId1 <-> AA.AfId3
                    RegisterNode(pa);
                    if (pa.Affiliation != null)
                    {
                        RegisterNode(pa.Affiliation);
                        RegisterEdge(pa.Id, pa.Affiliation.Id, true);
                    }
                }
            }
            GetStatus(author.Id).MarkAsExplored(NodeStatus.AuthorPapersExploration);
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
            var er = await GlobalServices.ASClient.EvaluateAsync(
                SEB.EntityOrAuthorIdEquals(id), 2, 0);
            if (er.Entities.Count == 0) return null;
            foreach (var et in er.Entities)
            {
                if (et.Authors == null)
                {
                    // 很有可能意味着这是一个作者节点，因为论文都是有作者的。
                    // ISSUE 如果试图将作者Id（AA.AuId）作为实体Id（Id）进行查询的话，
                    //          是可以查询出结果的。只是检索的结果除了 logprob 和 id
                    //          以外，其他属性都是空的。
                    // 所以需要跳过这一轮。
                    continue;
                }
                var au = et.Authors.FirstOrDefault(a => a.Id == id);
                if (au != null) return new AuthorNode(au);
                if (et.Id == id) return new PaperNode(et);
            }
            Logging.Warn(this, $"查找Id/AuId {id} 时接收到了不正确的信息。");
            return null;
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
            Logging.Enter(this, $"{id1} -> {id2}");
            var sw = Stopwatch.StartNew();
            // 先找到实体/作者再说。
            var nodes = await Task.WhenAll(GetEntityOrAuthorNodeAsync(id1),
                GetEntityOrAuthorNodeAsync(id2));
            var node1 = nodes[0];
            var node2 = nodes[1];
            if (node1 == null) throw new ArgumentException($"在 MAG 中找不到指定的 Id：{id1}", nameof(id1));
            if (node1 == null) throw new ArgumentException($"在 MAG 中找不到指定的 Id：{id2}", nameof(id2));
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
            Logging.Success(this, "在 {0} - {1} 之间找到了 {2} 条路径。用时： {3} 。", node1, node2, result.Length, sw.Elapsed);
            Logging.Trace(this, "缓存图： {0} 个节点， {1} 条边。", graph.VerticesCount, graph.EdgesCount);
            Logging.Exit(this);
            return result;
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
