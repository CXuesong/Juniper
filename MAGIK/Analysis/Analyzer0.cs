//  Analyzer    基础

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;
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
            var adj = await node.GetAdjacentNodesAsync(asClient);
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
                        var idc = (ICollection<long>)ids;
                        var er = await asClient.EvaluateAsync(
                            SEB.EntityIdIn(idc),
                            SEB.MaxChainedIdCount);
                        if (er.Entities.Count < idc.Count)
                            Logging.Warn(this, "批量查询实体 Id 时，返回结果数量不足。期望：{0}，实际：{1}。", idc.Count, er.Entities.Count);
                        return er.Entities.Select(et => new PaperNode(et));
                    }).ToArray();   // 先让网络通信启动起来。
                Func<PaperNode, Task> explore = async paperNode =>
                {
                    //Debug.Assert(this.nodes.ContainsKey(paperNode.Id));
                    var adj = await paperNode.GetAdjacentNodesAsync(asClient);
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
            foreach (var paper in await author.GetPapersAsync(asClient))
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
            var er = await asClient.EvaluateAsync(SEB.EntityOrAuthorIdEquals(id), 2, 0);
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
        /// 根据给定的两个节点，探索能够与这两个节点相连的中间结点集合。
        /// </summary>
        private Task ExploreInterceptionNodesAsync(IEnumerable<KgNode> nodes1, KgNode node2)
        {
            var tasks = nodes1
                .ToLookup(n => n.GetType())
                .Select(g =>
                {
                    if (g.Key == typeof (PaperNode))
                        // 论文可以批量处理。
                        return ExploreInterceptionNodesInternalAsync(g, node2);
                        // 其它节点只能一个一个来。
                    return Task.WhenAll(g.Select(node => ExploreInterceptionNodesAsync(node, node2)));
                });
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// 根据给定的两个节点，探索能够与这两个节点相连的中间结点集合。
        /// </summary>
        private Task ExploreInterceptionNodesAsync(IEnumerable<PaperNode> nodes1, KgNode node2)
        {
            return ExploreInterceptionNodesInternalAsync(nodes1, node2);
        }

        /// <summary>
        /// 根据给定的两个节点，探索能够与这两个节点相连的中间结点集合。
        /// </summary>
        private Task ExploreInterceptionNodesAsync(IEnumerable<AuthorNode> nodes1, KgNode node2)
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
        private async Task ExploreInterceptionNodesInternalAsync(IEnumerable<KgNode> nodes1, KgNode node2)
        {
            if (nodes1 == null) throw new ArgumentNullException(nameof(nodes1));
            if (node2 == null) throw new ArgumentNullException(nameof(node2));
            var nodes1Array = nodes1.ToArray();
            if (nodes1Array.Length == 0)
                return;     // Nothing to explore.
            KgNode node1 = null;
            // PossibleMultipleEnumeration -- 反正只是一次 Cast<T>，反复枚举应该没什么问题。
            IEnumerable<PaperNode> papers1 = null;
            // 在进行上下文相关探索之前，先对两个节点进行局部探索。
            if (nodes1Array.Length == 1)
            {
                node1 = nodes1Array[0];
                if (node1 is PaperNode)
                    papers1 = new[] { (PaperNode)node1 };
                await Task.WhenAll(LocalExploreAsync(node1), LocalExploreAsync(node2));
            }
            else
            {
                papers1 = nodes1Array.Cast<PaperNode>();
                await Task.WhenAll(LocalExploreAsync(papers1),
                    LocalExploreAsync(node2));
            }
            var paper2 = node2 as PaperNode;
            Func<string, Task> ExploreFromPapers1References = searchConstraint =>
            {
                // 注意，我们是来做全局搜索的。像是 Id -> AuId -> Id
                // 这种探索在局部探索阶段应该已经解决了。
                Debug.Assert(papers1 != null);
                var tasks = papers1.SelectMany(p1 => graph
                    .AdjacentOutVertices(p1.Id))
                    .Distinct()     // 注意，参考文献(或是作者)很可能会重复。
                    .Where(id3 => nodes[id3] is PaperNode)
                    .Partition(SEB.MaxChainedIdCount)
                    .Select(async id3s =>
                    {
                        //TODO 在探索作者所有的文章时，这些文章的参考文献其实已经被探索过了。
                        //在这里跳过这些节点即可。
                        var er = await asClient.EvaluateAsync(SEB.And(
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
            // 带有 作者/会议/期刊 等属性限制，搜索引用中含有 paper2 的论文 Id。
            Func<string, int, Task> ExploreToPaper2WithAttributes =
                async (attributeConstraint, maxPapers) =>
                {
                    var er = await asClient.EvaluateAsync(SEB.And(
                        attributeConstraint,
                        SEB.ReferenceIdContains(paper2.Id)),
                        maxPapers);
                    foreach (var et in er.Entities)
                    {
                        RegisterNode(new PaperNode(et));
                        // ??Id1 <-> Id3
                        Debug.Assert(!(node1 is PaperNode));
                        RegisterEdge(node1.Id, et.Id, true);
                        // Id3 -> Id2
                        RegisterEdge(et.Id, paper2.Id, false);
                    }
                };
            if (paper2 != null)
            {
                if (papers1 != null)
                {
                    // Id1 -> Id3 -> Id2
                    await ExploreFromPapers1References(SEB.ReferenceIdContains(paper2.Id));
                }
                else if (node1 is AuthorNode)
                {
                    // AA.AuId <-> Id -> Id
                    await ExploreToPaper2WithAttributes(SEB.AuthorIdContains(node1.Id), Assumptions.AuthorMaxPapers);
                }
                else if (node1 is FieldOfStudyNode)
                {
                    // F.FId <-> Id -> Id
                    await
                        ExploreToPaper2WithAttributes(SEB.FieldOfStudyIdEquals(node1.Id), Assumptions.PaperMaxCitations);
                }
                else if (node1 is ConferenceNode)
                {
                    // F.FId <-> Id -> Id
                    await ExploreToPaper2WithAttributes(SEB.ConferenceIdEquals(node1.Id), Assumptions.PaperMaxCitations);
                }
                else if (node1 is JournalNode)
                {
                    // F.FId <-> Id -> Id
                    await ExploreToPaper2WithAttributes(SEB.JournalIdEquals(node1.Id), Assumptions.PaperMaxCitations);
                }
                else if (node1 is AffiliationNode)
                {
                    // AA.AfId <-> AA.AuId <-> Id
                    await ExploreToPaper2WithAttributes(SEB.JournalIdEquals(node1.Id), Assumptions.PaperMaxCitations);
                }
            }
            else if (node2 is AuthorNode)
            {
                if (papers1 != null)
                {
                    // Id1 -> Id3 -> AA.AuId2
                    await ExploreFromPapers1References(SEB.AuthorIdContains(node2.Id));
                }
                else if (node1 is AuthorNode)
                {
                    // AA.AuId1 <-> Id3 <-> AA.AuId2
                    var author1 = (AuthorNode)node1;
                    // 探索 AA.AuId1 的所有论文。此处的探索还可以顺便确定 AuId1 的所有组织。
                    // 注意到每个作者都会写很多论文
                    // 不论如何，现在尝试从 Id1 向 Id2 探索。
                    // 我们需要列出 Id1 的所有文献，以获得其曾经位于的所有组织。
                    await ExploreAuthorPapersAsync(author1);
                    // AA.AuId1 <-> AA.AfId3 <-> AA.AuId2
                    var author2in = graph.AdjacentInVertices(node2.Id);
                    foreach (var afid in graph.AdjacentOutVertices(node1.Id).Where(id =>
                        nodes[id] is AffiliationNode))
                    {
                        // 如果已知的 Author2 已经存在对此机构的联系，那么就不用上网去确认了。
                        if (!author2in.Contains(afid))
                        {
                            if (await asClient.EvaluationHasResultAsync(
                                SEB.AuthorIdWithAffiliationIdContains(node2.Id, afid)))
                            {
                                // AA.AfId3 <-> AA.AuId2
                                RegisterEdge(node2.Id, afid, true);
                            }
                        }
                    }
                }
            }
        }
    }
}
