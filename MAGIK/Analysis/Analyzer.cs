//  Analyzer    公共接口、数据类型和存储

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

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    public partial class Analyzer : AnalyzerBase
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


        public Analyzer(AcademicSearchClient asClient)
            : base(asClient)
        {

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
        /// <remarks>
        /// 此函数应该是线程安全的……吧？
        /// </remarks>
        public override async Task<IReadOnlyCollection<KgNode[]>> FindPathsAsync(long id1, long id2)
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
            var hops = await Task.WhenAll(
                FindHop12PathsAsync(node1, node2),
                FindHop3PathsAsync(node1, node2));
            var result = MultiCollectionView.Create(hops);
            Debug.Assert(result.IsDistinct(ArrayEqualityComparer<KgNode>.Default));
            Logger.Magik.Success(this, "在 {0} - {1} 之间找到了 {2} 条路径。用时： {3} 。", node1, node2, result.Count, sw.Elapsed);
            TimerLogger.TraceTimer("Analyzer", sw);
            Logger.Magik.Exit(this);
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
        }

        /// <summary>
        /// 向 Trace 输出图的文本表示。注意，对于大规模的网络，此操作可能会非常慢。
        /// </summary>
        public void TraceGraph()
        {
            Trace.WriteLine(graph.Dump());
        }


        /// <summary>
        /// 获取调用统计信息。
        /// 统计论文节点的首字母及其累积频率。
        /// </summary>
        public string DumpAlphabet()
        {
            var alphabet = nodes.Values.AsParallel().OfType<PaperNodeBase>()
                .Where(n => !string.IsNullOrEmpty(n.Name))
                .GroupBy(n => char.ToUpperInvariant(n.Name[0]))
                .OrderBy(g => g.Key)
                .Select(g => new {Alphabet = g.Key, Count = g.Count()})
                .ToArray();
            var result = "";
            var total = alphabet.Sum(a => a.Count);
            var current = 0;
            foreach (var a in alphabet)
            {
                result += a.Alphabet + "\t" + (double) current/total + "\n";
                current += a.Count;
            }
            return result;
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
            public static readonly ExplorationDomainKey LocalExploration =
                new TokenExplorationDomainKey("LocalExploration");

            /// <summary>
            /// 作者所写的所有论文的探索情况。
            /// </summary>
            public static readonly ExplorationDomainKey AuthorPapersExploration =
                new TokenExplorationDomainKey("AuthorPapersExploration");

            // 一个节点被探索，当且仅当在向服务器提交查询，并能够获得此节点的完全信息之后。
            // 在 DEBUG 模式下，完全信息包括节点的名称和此节点的邻节点（保存在 Graph 中，
            //      但很可能不在 exploredNodes 中，因为这些邻节点还没有被探索）。
            // 在 RELEASE 模式下，完全信息仅包括节点的邻节点。
            private readonly Dictionary<ExplorationDomainKey, ExplorationStatus> explorationStatusDict 
                = new Dictionary<ExplorationDomainKey, ExplorationStatus>();
            // 约定：在任务结束后，TaskCompletionSource 应当返回 true 。
            private readonly Dictionary<ExplorationDomainKey, TaskCompletionSource<bool>> explorationTaskCompletionSourceDict
                = new Dictionary<ExplorationDomainKey, TaskCompletionSource<bool>>();
            private ReaderWriterLockSlim syncLock = new ReaderWriterLockSlim();
#if DEBUG
            private readonly long _NodeId;

            public override string ToString()
            {
                return _NodeId + ";" + string.Join(";", explorationStatusDict.Select(p => p.Key + "," + p.Value));
            }
#endif

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
            public bool TryMarkAsExploring(ExplorationDomainKey domainKey)
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
                    //Debug.WriteLine("Exploring-{0}: {1}", domainKey, this);
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
            public Task<bool> MarkAsExploringOrUntilExplored(ExplorationDomainKey domainKey)
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
                                var tcs = explorationTaskCompletionSourceDict.GetOrAddNew(domainKey);
                                return tcs.Task.ContinueWith(r => false);
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
            /// 则等待直到节点处于 <see cref="ExplorationStatus.Explored" /> 状态。
            /// </summary>
            /// <returns>
            /// 返回一个任务。如果当前节点处于 <see cref="ExplorationStatus.Unexplored"/>
            /// 状态，则直接返回 false 。
            /// 否则会在（其他线程）探索结束后返回 <c>true</c>。
            /// </returns>
            public Task<bool> UntilExploredAsync(ExplorationDomainKey domainKey)
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
                            case ExplorationStatus.Exploring:
                                // Wait for exploration
                                var tcs = explorationTaskCompletionSourceDict.GetOrAddNew(domainKey);
                                return tcs.Task.ContinueWith(r => true);
                            case ExplorationStatus.Explored:
                                return Task.FromResult(true);
                            default:
                                return Task.FromResult(false);
                        }
                    }
                    Debug.Assert(s == ExplorationStatus.Unexplored);
                    return Task.FromResult(false);
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
            public void MarkAsExplored(ExplorationDomainKey domainKey)
            {
                if (domainKey == null) throw new ArgumentNullException(nameof(domainKey));
                syncLock.EnterWriteLock();
                try
                {
                    Debug.Assert(explorationStatusDict[domainKey] == ExplorationStatus.Exploring);
                    explorationStatusDict[domainKey] = ExplorationStatus.Explored;
                    var tcs = explorationTaskCompletionSourceDict.TryGetValue(domainKey);
                    if (tcs != null)
                    {
                        // 为什么是 true ？ 
                        // 参阅 explorationTaskCompletionSourceDict 声明处的约定。
                        tcs.SetResult(true);
                        explorationTaskCompletionSourceDict.Remove(domainKey);
                    }
                }
                finally
                {
                    syncLock.ExitWriteLock();
                }
            }

            public ExplorationStatus GetExplorationStatus(ExplorationDomainKey domainKey)
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

            public NodeStatus(long nodeId)
            {
#if DEBUG
                _NodeId = nodeId;
#endif
            }
        }

        /// <summary>
        /// 用于在 <see cref="NodeStatus"/> 中标注当前节点已经探索过的内容。
        /// 这是一个键类型，因而应当是不可变的。
        /// </summary>
        private class ExplorationDomainKey : IEquatable<ExplorationDomainKey>
        {
            /// <summary>
            /// 指示当前对象是否等于同一类型的另一个对象。
            /// </summary>
            /// <returns>
            /// 如果当前对象等于 <paramref name="other"/> 参数，则为 true；否则为 false。
            /// </returns>
            /// <param name="other">与此对象进行比较的对象。</param>
            public bool Equals(ExplorationDomainKey other)
            {
                if (this == other) return true;
                if (other == null) return false;
                if (GetType() != other.GetType()) return false;
                return EqualsCore(other);
            }

            /// <summary>
            /// 确定指定的对象是否等于当前对象。
            /// </summary>
            /// <returns>
            /// 如果指定的对象等于当前对象，则为 true，否则为 false。
            /// </returns>
            /// <param name="obj">要与当前对象进行比较的对象。</param>
            public override bool Equals(object obj)
            {
                var other = obj as ExplorationDomainKey;
                if (other == null) return false;
                return Equals(other);
            }

            /// <summary>
            /// 作为默认哈希函数。
            /// </summary>
            /// <returns>
            /// 当前对象的哈希代码。
            /// </returns>
            public sealed override int GetHashCode()
            {
                return GetType().GetHashCode() ^ GetHashCodeCore();
            }

            /// <summary>
            /// 在派生类中重写时，判断当前对象和另一对象是否相等。
            /// </summary>
            /// <remarks>
            /// 调用方已经保证 <paramref name="other"/> 与当前对象类型相同。
            /// </remarks>
            protected virtual bool EqualsCore(ExplorationDomainKey other)
            {
                Debug.Assert(GetType() == other.GetType());
                return true;
            }

            protected virtual int GetHashCodeCore()
            {
                return 0;
            }
        }

        /// <summary>
        /// 使用一个名称来区分探索领域。适用于”局部探索“、
        /// ”作者所有论文探索“的标注。
        /// </summary>
        private class TokenExplorationDomainKey : ExplorationDomainKey
        {
            /// <summary>
            /// 探索领域的名称。
            /// </summary>
            public string Name { get; }

            public TokenExplorationDomainKey(string name)
            {
                if (name == null) throw new ArgumentNullException(nameof(name));
                Name = name;
            }

            protected override bool EqualsCore(ExplorationDomainKey other)
            {
                return base.EqualsCore(other) && Name == ((TokenExplorationDomainKey) other).Name;
            }

            protected override int GetHashCodeCore()
            {
                return Name.GetHashCode();
            }

            /// <summary>
            /// 返回表示当前对象的字符串。
            /// </summary>
            /// <returns>
            /// 表示当前对象的字符串。
            /// </returns>
            public override string ToString()
            {
                return Name;
            }
        }

        /// <summary>
        /// 表示此节点和另一个节点之间的所有中间节点的探索情况。
        /// 尤其是指使用 <see cref="ExploreInterceptionNodesInternalAsync"/> 进行探索的情况。
        /// 注意，探索的方向为 被标记的节点 指向 <see cref="AnotherNodeId"/> 对应的节点。
        /// </summary>
        private class InterceptionExplorationDomainKey : ExplorationDomainKey
        {
            /// <summary>
            /// 被探索的另一个节点的 Id 。
            /// </summary>
            public long AnotherNodeId { get; }

            public InterceptionExplorationDomainKey(long anotherNodeId)
            {
                AnotherNodeId = anotherNodeId;
            }

            protected override bool EqualsCore(ExplorationDomainKey other)
            {
                return base.EqualsCore(other) &&
                       AnotherNodeId == ((InterceptionExplorationDomainKey) other).AnotherNodeId;
            }

            protected override int GetHashCodeCore()
            {
                return AnotherNodeId.GetHashCode();
            }

            /// <summary>
            /// 返回表示当前对象的字符串。
            /// </summary>
            /// <returns>
            /// 表示当前对象的字符串。
            /// </returns>
            public override string ToString()
            {
                return $"Interception({AnotherNodeId})";
            }
        }
    }
}
