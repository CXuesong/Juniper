//  Analyzer    基础

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;
using Microsoft.Contests.Bop.Participants.Magik.Academic.Contract;
using SEB = Microsoft.Contests.Bop.Participants.Magik.Academic.SearchExpressionBuilder;

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    partial class Analyzer
    {

        /// <summary>
        /// 获取指定 Id 的节点状态。如果指定的 Id 目前不存在状态，则会新建一个。
        /// </summary>
        private NodeStatus GetStatus(long id)
        {
            Debug.Assert(graph.Vertices.Contains(id));
            return status.GetOrAdd(id, i => new NodeStatus(id));
        }

        /// <summary>
        /// 尝试注册一个处于“未探索”状态的节点。
        /// </summary>
        /// <returns>
        /// 如果此节点已经被注册，且与 node 不同，则返回 <c>false</c> 。
        /// </returns>
        private bool RegisterNode(KgNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            var factoryCalled = false;
            var nn = nodes.GetOrAdd(node.Id, id =>
            {
                factoryCalled = true;
                return node;
            });
            if (factoryCalled)
            {
                Debug.Assert(nn == node);
                graph.Add(node.Id);
                return true;
            }
            else
            {
                // 此节点已经被发现
                // 断言节点类型。
                if (nn.GetType() != node.GetType())
                    Logger.Magik.Warn(this, "试图注册的节点{0}与已注册的节点{1}具有不同的类型。", node, nn);
                return false;
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
        /// 如果指定的节点尚未探索，则探索此节点。并会保证注册包括 node 在内的所有节点。
        /// 如果其他线程正在探索此节点，则等待此节点探索完毕。
        /// </summary>
        private async Task LocalExploreAsync(KgNode node)
        {
            Debug.Assert(node != null);
            var s = GetStatus(node.Id);
            if (!await s.MarkAsExploringOrUntilExplored(NodeStatus.LocalExploration))
                return;
            var newlyDiscoveredNodes = 0;
            //if (RegisterNode(node)) newlyDiscoveredNodes++;
            var adj = await node.GetAdjacentNodesAsync(SearchClient);
            // an: Adjacent Node
            foreach (var an in adj)
            {
                if (RegisterNode(an)) newlyDiscoveredNodes++;
                RegisterEdge(node.Id, an.Id, !(an is PaperNode));
            }
            s.MarkAsExplored(NodeStatus.LocalExploration);
            //Logger.Magik.Trace(this, EventId.OperationSucceeded,
            //    "LocalExplore {0} -> {1} new nodes.", node, newlyDiscoveredNodes);
        }

        /// <summary>
        /// 同时探索多个节点。适用于文章节点。注意，此函数仅在查询方面进行了优化，
        /// 其操作与多次调用 <see cref="LocalExploreAsync(KgNode)"/> 是等价的。
        /// 如果其他线程正在探索此节点，则等待此节点探索完毕。
        /// </summary>
        private async Task LocalExploreAsync(IReadOnlyCollection<PaperNode> paperNodes)
        {
            Debug.Assert(paperNodes != null);
            var newlyDiscoveredNodes = 0;
            // 注意，有些节点可能仍处于 正在探索 的状态。
            // 需要在返回前等待这些正在探索的节点。
            var nodesToExplore = paperNodes
                .Where(n => GetStatus(n.Id).TryMarkAsExploring(NodeStatus.LocalExploration))
                .ToArray();
            if (nodesToExplore.Length == 0) goto WAIT_FOR_EXPLORATIONS;
            // 区分“已经下载详细信息”的实体和“需要下载详细信息”的实体。
            var nodesToFetch = nodesToExplore.Where(n => n.IsStub);
            var nodesFetched = nodesToExplore.Where(n => !n.IsStub);
            var fetchTasks = nodesToFetch.Select(n => n.Id)
                .Partition(SEB.MaxChainedIdCount)
                .Select(async ids =>
                {
                    // 假定 Partition 返回的是 IList / ICollection
                    var idc = (ICollection<long>) ids;
                    var er = await SearchClient.EvaluateAsync(
                        SEB.EntityIdIn(idc),
                        SEB.MaxChainedIdCount);
                    if (er.Entities.Count < idc.Count)
                        Logger.Magik.Warn(this, "批量查询实体 Id 时，返回结果数量不足。期望：{0}，实际：{1}。", idc.Count, er.Entities.Count);
                    return er.Entities.Select(et => new PaperNode(et));
                }).ToArray(); // 先让网络通信启动起来。
            // 随后，先把 paperNodes 注册一遍。
            newlyDiscoveredNodes += nodesToExplore.Count(RegisterNode);
            // 定义探索过程。
            Func<PaperNode, Task> explore = async paperNode =>
            {
                var adj = await paperNode.GetAdjacentNodesAsync(SearchClient);
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
            WAIT_FOR_EXPLORATIONS:
            //确保返回前，所有 Exploring 的节点已经由此线程或其他线程处理完毕。
            await Task.WhenAll(paperNodes.Select(n =>
                GetStatus(n.Id).UntilExploredAsync(NodeStatus.LocalExploration)));
        }

        /// <summary>
        /// 异步探索作者的所有论文，顺便探索他/她在发表这些论文时所位于的机构。
        /// 如果其他线程正在探索此节点，则等待此节点探索完毕。
        /// </summary>
        private async Task ExploreAuthorsPapersAsync(IReadOnlyCollection<AuthorNode> authorNodes)
        {
            // 有些类似于
            //      Task LocalExploreAsync(IEnumerable<PaperNode> paperNodes);
            // 探索 author 的所有论文。此处的探索还可以顺便确定 author 的所有组织。
            var nodesToExplore = authorNodes
                .Where(n => GetStatus(n.Id).TryMarkAsExploring(NodeStatus.AuthorPapersExploration))
                .ToArray();
            if (nodesToExplore.Length == 0) goto WAIT_FOR_EXPLORATIONS;
            var papers = 0;
            // 随后，先把 authorNodes 注册一遍。
            var fetchTasks = nodesToExplore.Select(n => n.Id)
                .Partition(SEB.MaxChainedAuIdCount)
                .Select(async ids =>
                {
                    // 一次探索若干作者。这意味着不同作者的文章是混在一起的。
                    // 假定 Partition 返回的是 IList / ICollection
                    var idc = (ICollection<long>) ids;
                    var er = await SearchClient.EvaluateAsync(
                        SEB.AuthorIdIn(idc),
                        Assumptions.AuthorMaxPapers * idc.Count);
                    // 实际情况应当是， er.Entities.Count >> idc.Count
                    if (er.Entities.Count < idc.Count)
                        Logger.Magik.Warn(this, "批量查询实体 Id 时，返回结果数量不足。期望：>>{0}，实际：{1}。", idc.Count, er.Entities.Count);
                    Interlocked.Add(ref papers, er.Entities.Count);
                    await Task.WhenAll(er.Entities.Select(async et =>
                    {
                        var paper = new PaperNode(et);
                        // 此处还可以注册 paper 与其所有作者之间的关系。
                        // 这样做的好处是，万一 author1 和 author2 同时写了一篇论文。
                        // 在这里就可以发现了。
                        RegisterNode(paper);
                        var localExploreTask = LocalExploreAsync(paper);
                        // 为作者 AA.AuId1 注册所有可能的机构。
                        // 这里比较麻烦，因为一个作者可以属于多个机构，所以
                        // 不能使用 LocalExploreAsync （会认为已经探索过。）
                        // 而需要手动建立节点关系。
                        foreach (var au in et.Authors)
                        {
                            if (au.AffiliationId != null)
                            {
                                var aff = new AffiliationNode(au);
                                RegisterNode(aff);
                                RegisterNode(new AuthorNode(au));
                                RegisterEdge(au.Id, aff.Id, true);
                            }
                        }
                        await localExploreTask;
                    }));
                });
            await Task.WhenAll(fetchTasks);
            // 标记为“已经探索过”。
            foreach (var an in nodesToExplore)
                GetStatus(an.Id).MarkAsExplored(NodeStatus.AuthorPapersExploration);
            WAIT_FOR_EXPLORATIONS:
            //确保返回前，所有 Exploring 的节点已经由此线程或其他线程处理完毕。
            await Task.WhenAll(authorNodes.Select(n =>
                GetStatus(n.Id).UntilExploredAsync(NodeStatus.AuthorPapersExploration)));
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
            var er = await SearchClient.EvaluateAsync(SEB.EntityOrAuthorIdEquals(id), 2, 0);
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
            Logger.Magik.Warn(this, $"查找Id/AuId {id} 时接收到了不正确的信息。");
            return null;
        }

        /// <summary>
        /// 根据给定的两个节点，探索能够与这两个节点相连的中间结点集合。
        /// </summary>
        private Task ExploreInterceptionNodesAsync(IEnumerable<KgNode> nodes1, KgNode node2)
        {
            var tasks = nodes1
                .ToLookup(n => n.GetType())
                .Select(g =>
                {
                    if (g.Key == typeof (PaperNode) || g.Key == typeof (AuthorNode))
                        // 论文 -和- 作者 可以批量处理。
                        return ExploreInterceptionNodesInternalAsync(g.ToArray(), node2);
                    // 其它节点只能一个一个来。
                    return Task.WhenAll(g.Select(node => ExploreInterceptionNodesAsync(node, node2)));
                });
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// 根据给定的两个节点，探索能够与这两个节点相连的中间结点集合。
        /// </summary>
        private Task ExploreInterceptionNodesAsync(IReadOnlyList<PaperNode> nodes1, KgNode node2)
        {
            return ExploreInterceptionNodesInternalAsync(nodes1, node2);
        }

        /// <summary>
        /// 根据给定的两个节点，探索能够与这两个节点相连的中间结点集合。
        /// </summary>
        private Task ExploreInterceptionNodesAsync(IReadOnlyList<AuthorNode> nodes1, KgNode node2)
        {
            // 多个作者只能一个一个搜。反正一篇文章应该没几个作者……吧？
            return Task.WhenAll(nodes1.Select(au => ExploreInterceptionNodesInternalAsync(new[] { au }, node2)));
        }

        /// <summary>
        /// 根据给定的两个节点，探索能够与这两个节点相连的中间结点集合。
        /// </summary>
        private Task ExploreInterceptionNodesAsync(KgNode node1, KgNode node2)
        {
            return ExploreInterceptionNodesInternalAsync(new[] { node1 }, node2);
        }

        /// <summary>
        /// 根据给定的两个节点，探索能够与这两个节点相连的中间结点集合。
        /// 注意，在代码中请使用 ExploreInterceptionNodesAsync 的重载。
        /// 不要直接调用此函数。
        /// </summary>
        private async Task ExploreInterceptionNodesInternalAsync(IReadOnlyList<KgNode> nodes1, KgNode node2)
        {
            if (nodes1 == null) throw new ArgumentNullException(nameof(nodes1));
            if (node2 == null) throw new ArgumentNullException(nameof(node2));
            if (nodes1.Count == 0)
                return;     // Nothing to explore.
            KgNode node1 = null;
            var explore12DomainKey = new InterceptionExplorationDomainKey(node2.Id);
            var node2Status = GetStatus(node2.Id);
            var nodes1ToExplore = nodes1
                .Where(n => GetStatus(n.Id).TryMarkAsExploring(explore12DomainKey))
                .ToArray();
            if (nodes1ToExplore.Length == 0) goto WAIT_FOR_EXPLORATIONS;
            IReadOnlyCollection<PaperNode> papers1 = null;
            IReadOnlyCollection<AuthorNode> authors1 = null;
            // 在进行上下文相关探索之前，先对两个节点进行局部探索。
            if (nodes1ToExplore.Length == 1)
            {
                node1 = nodes1ToExplore[0];
                if (node1 is PaperNode)
                    papers1 = new[] { (PaperNode)node1 };
                else if (node1 is AuthorNode)
                    authors1 = new[] {(AuthorNode) node1};
                await Task.WhenAll(LocalExploreAsync(node1), LocalExploreAsync(node2));
            }
            else
            {
                node1 = null;
                Task nodes1Task;
                if (nodes1ToExplore[0] is PaperNode)
                {
                    papers1 = nodes1ToExplore.Cast<PaperNode>().ToArray();
                    nodes1Task = LocalExploreAsync(papers1);
                }
                else if (nodes1ToExplore[0] is AuthorNode)
                {
                    authors1 = nodes1ToExplore.Cast<AuthorNode>().ToArray();
                    // 先按照局部探索把作者过一遍吧。
                    // 感觉似乎并无卵，但一切为了约定。
                    nodes1Task = Task.WhenAll(authors1.Select(LocalExploreAsync));
                }
                else
                {
                    throw new ArgumentException("集合包含元素的类型过于复杂。请尝试使用单元素集合多次调用。", nameof(node1));
                }
                await Task.WhenAll(nodes1Task, LocalExploreAsync(node2));
            }
            var paper2 = node2 as PaperNode;
            // searchConstraint : 建议尽量简短，因为 FromPapers1 的约束可能
            // 会是 100 个条件的并。
            Func<string, Task> ExploreFromPapers1References = searchConstraint =>
            {
                // 注意，我们是来做全局搜索的。像是 Id -> AuId -> Id
                // 这种探索在局部探索阶段应该已经解决了。
                Debug.Assert(papers1 != null);
                var tasks = papers1.SelectMany(p1 => graph
                    .AdjacentOutVertices(p1.Id))
                    .Distinct()     // 注意，参考文献(或是作者——尽管在这里不需要)很可能会重复。
                    .Where(id3 => nodes[id3] is PaperNode)
                    .Partition(SEB.MaxChainedIdCount)
                    .Select(async id3s =>
                    {
                        //TODO 在探索作者所有的文章时，这些文章的参考文献其实已经被探索过了。
                        //在这里跳过这些节点即可。
                        var er = await SearchClient.EvaluateAsync(SEB.And(
                            SEB.EntityIdIn(id3s),
                            searchConstraint),
                            SEB.MaxChainedIdCount);
                        await Task.WhenAll(er.Entities.Select(async et =>
                        {
                            var pn = new PaperNode(et);
                            RegisterNode(pn);
                            //Id1 -> Id3 已经在之前的局部探索处理过了。
                            //但 Id3 节点往外还有很多尚未探索的关系。
                            await LocalExploreAsync(pn);
                        }));
                    });
                return Task.WhenAll(tasks);
            };
            if (paper2 != null)
            {
                // 带有 作者/会议/期刊 等属性限制，搜索引用中含有 paper2 的论文 Id。
                // attributeConstraint 可以长一些。
                Func<string, Task> ExploreToPaper2WithAttributes =
                    async attributeConstraint =>
                    {
                        // 一般来说， Paper2 肯定就是题目中的终结点，
                        // 因此我们是知道其具体信息的。
                        Debug.Assert(!paper2.IsStub);
                        var maxPapers = paper2.IsStub
                            ? Assumptions.PaperMaxCitations
                            : paper2.CitationCount;
                        var er = await SearchClient.EvaluateAsync(SEB.And(
                            attributeConstraint,
                            SEB.ReferenceIdContains(paper2.Id)),
                            maxPapers);
                        foreach (var et in er.Entities)
                        {
                            var node = new PaperNode(et);
                            RegisterNode(node);
                            // 假异步。
                            await LocalExploreAsync(node);
                        }
                    };
                if (papers1 != null)
                {
                    // Id1 -> Id3 -> Id2
                    await ExploreFromPapers1References(SEB.ReferenceIdContains(paper2.Id));
                }
                else if (authors1 != null)
                {
                    // AA.AuId <-> Id -> Id
                    await Task.WhenAll(authors1
                        .Select(n => n.Id)
                        .Partition(SEB.MaxChainedAuIdCount)
                        .Select(id1s => ExploreToPaper2WithAttributes(SEB.AuthorIdIn(id1s))));
                }
                else if (node1 is FieldOfStudyNode)
                {
                    // F.FId <-> Id -> Id
                    await
                        ExploreToPaper2WithAttributes(SEB.FieldOfStudyIdEquals(node1.Id));
                }
                else if (node1 is ConferenceNode)
                {
                    // F.FId <-> Id -> Id
                    await ExploreToPaper2WithAttributes(SEB.ConferenceIdEquals(node1.Id));
                }
                else if (node1 is JournalNode)
                {
                    // F.FId <-> Id -> Id
                    await ExploreToPaper2WithAttributes(SEB.JournalIdEquals(node1.Id));
                }
                else if (node1 is AffiliationNode)
                {
                    // AA.AfId <-> AA.AuId <-> Id
                    await ExploreToPaper2WithAttributes(SEB.JournalIdEquals(node1.Id));
                }
            }
            else if (node2 is AuthorNode)
            {
                if (papers1 != null)
                {
                    // Id1 -> Id3 -> AA.AuId2
                    await ExploreFromPapers1References(SEB.AuthorIdContains(node2.Id));
                }
                else if (authors1 != null)
                {
                    // AA.AuId1 <-> Id3 <-> AA.AuId2
                    //var author1 = (AuthorNode)node1;
                    // 探索 AA.AuId1 的所有论文。此处的探索还可以顺便确定 AuId1 的所有组织。
                    // 注意到每个作者都会写很多论文
                    // 不论如何，现在尝试从 Id1 向 Id2 探索。
                    // 我们需要列出 Id1 的所有文献，以获得其曾经位于的所有组织。
                    await ExploreAuthorsPapersAsync(authors1);
                    // AA.AuId1 <-> AA.AfId3 <-> AA.AuId2
                    var author2in = graph.AdjacentInVertices(node2.Id);
                    foreach (var afid in authors1
                        .SelectMany(au1 => graph.AdjacentOutVertices(au1.Id)
                        .Where(id2 =>nodes[id2] is AffiliationNode)))
                    {
                        // 如果已知的 Author2 已经存在对此机构的联系，那么就不用上网去确认了。
                        if (!author2in.Contains(afid))
                        {
                            if (await SearchClient.EvaluationHasResultAsync(
                                SEB.AuthorIdWithAffiliationIdContains(node2.Id, afid)))
                            {
                                // AA.AfId3 <-> AA.AuId2
                                RegisterEdge(node2.Id, afid, true);
                            }
                        }
                    }
                }
            }
            // 标记探索完毕
            foreach (var n in nodes1ToExplore)
                GetStatus(n.Id).MarkAsExplored(explore12DomainKey);
            // 等待其他线程（如果有）
            WAIT_FOR_EXPLORATIONS:
            await Task.WhenAll(nodes1.Select(n => GetStatus(n.Id).UntilExploredAsync(explore12DomainKey)));
        }
    }
}
