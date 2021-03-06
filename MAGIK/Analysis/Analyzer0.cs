﻿//  Analyzer    基础

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            Debug.Assert(graph.Contains(id));
            return _Status.GetOrAdd(id, i => new NodeStatus(id));
        }

        /// <summary>
        /// 简单地注册一个节点。不执行任何探索。
        /// </summary>
        private void RegisterNode(KgNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
#if DEBUG
            var factoryCalled = false;
            var nn = nodes.GetOrAdd(node.Id, id =>
            {
                factoryCalled = true;
                return node;
            });
            graph.Add(node.Id);
            if (!factoryCalled)
            {
                // 此节点已经被发现
                // 断言节点类型。
                if (nn.GetType() != node.GetType())
                    Debug.Fail(string.Format("试图注册的节点{0}与已注册的节点{1}具有不同的类型。", node, nn));
            }
#else
            // 性能关键，不要构造复杂的工厂函数。
            nodes.GetOrAdd(node.Id, node);
            graph.Add(node.Id);
#endif
        }

        /// <summary>
        /// 简单地注册一个节点。不执行任何探索。
        /// 如果节点已经存在，则返回现有的节点。
        /// </summary>
        private PaperNode RegisterNode(Entity entity)
        {
            var nd = nodes.GetOrAdd(entity.Id, id => new PaperNode(id, null));
            graph.Add(entity.Id);
            return (PaperNode) nd;
        }

        /// <summary>
        /// 简单地注册一个节点。不执行任何探索。
        /// 如果节点已经存在，则返回现有的节点。
        /// </summary>
        private AuthorNode RegisterAuthorNode(Author author)
        {
            var nd = nodes.GetOrAdd(author.Id, id => new AuthorNode(id, null));
            graph.Add(author.Id);
            return (AuthorNode)nd;
        }

        /// <summary>
        /// 简单地注册一个节点。不执行任何探索。
        /// 如果节点已经存在，则返回现有的节点。
        /// </summary>
        private AffiliationNode RegisterAffiliationNode(Author author)
        {
            Debug.Assert(author.AffiliationId != null);
            var nd = nodes.GetOrAdd(author.AffiliationId.Value, id => new AffiliationNode(id, null));
            graph.Add(author.Id);
            return (AffiliationNode) nd;
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
        /// 注册一条边。
        /// </summary>
        private void RegisterEdge(KgNode node1, KgNode node2)
        {
            // 已知的单向边只有一种： Id1 --> Id2 （Id1.RId = Id2）
            RegisterEdge(node1.Id, node2.Id, !(node1 is PaperNode && node2 is PaperNode));
        }

        /// <summary>
        /// 如果指定的节点尚未探索，则探索此节点，并注册与 node 相邻的所有节点。
        /// 如果其他线程正在探索此节点，则等待此节点探索完毕。
        /// 对于已经取得 Entity 信息的论文，在 graph 中拓展其联系。
        /// </summary>
        private async Task ExplorePaperAsync(Entity entity)
        {
            Debug.Assert(entity != null);
            var s = GetStatus(entity.Id);
            if (!await s.MarkAsFetchingOrUntilFetched(NodeStatus.PaperFetching))
                return;
            ExplorePaperUnsafe(entity);
            s.MarkAsFetched(NodeStatus.PaperFetching);
        }

        /// <summary>
        /// 直接进行局部探索，不加锁，不检查是否探索过。
        /// </summary>
        /// <remarks>此函数应当由 LocalExplore 系列函数调用。</remarks>
        private void ExplorePaperUnsafe(Entity entity)
        {
            // 探索是指从某节点向外延展。那么 某节点 肯定是已经注册过的。
            Debug.Assert(graph.Contains(entity.Id));
            // 记录被引用次数。
            if (entity.CitationCount > 0) _CitationCountDict[entity.Id] = entity.CitationCount;
            var node = nodes[entity.Id];
            foreach (var an in KgNode.EnumerateLocalAdjacents(entity))
            {
                RegisterNode(an);
                RegisterEdge(node, an);
            }
        }

        /// <summary>
        /// 同时探索多个节点。适用于文章节点。注意，此函数仅在查询方面进行了优化，
        /// 如果其他线程正在探索此节点，则等待此节点探索完毕。
        /// </summary>
        private async Task FetchPapersAsync(IReadOnlyCollection<KgNode> paperNodes)
        {
            Debug.Assert(paperNodes != null);
            // 注意，有些节点可能仍处于 正在探索 的状态。
            //      需要在返回前等待这些正在探索的节点。
            var nodesToFetch = paperNodes.Where(n => GetStatus(n.Id).TryMarkAsFetching(NodeStatus.PaperFetching))
                .ToArray();
            if (nodesToFetch.Length == 0) goto WAIT_FOR_EXPLORATIONS;
            await Task.WhenAll(nodesToFetch.Select(n => n.Id)
                .Partition(SEB.MaxChainedIdCount)
                .Select(ids =>
                {
                    // 假定 Partition 返回的是 IList / ICollection
                    var idc = (ICollection<long>)ids;
                    return SearchClient.EvaluateAsync(SEB.EntityIdIn(idc),
                        SEB.MaxChainedIdCount, ConcurrentPagingMode.Pessimistic,
                        page =>
                        {
                            if (page.Entities.Count < idc.Count)
                                Logger.Magik.Warn(this, "批量查询实体 Id 时，返回结果数量不足。期望：{0}，实际：{1}。", idc.Count,
                                    page.Entities.Count);
                            foreach (var entity in page.Entities)
                                ExplorePaperUnsafe(entity);
                            return Task.CompletedTask;
                        });
                }));
            foreach (var n in nodesToFetch)
            {
                GetStatus(n.Id).MarkAsFetched(NodeStatus.PaperFetching);
            }
            WAIT_FOR_EXPLORATIONS:
            //确保返回前，所有 Fetching 的节点已经由此线程或其他线程处理完毕。
            var waitResult = await Task.WhenAll(paperNodes.Select(n =>
                GetStatus(n.Id).UntilFetchedAsync(NodeStatus.PaperFetching)));
            Debug.Assert(waitResult.All(r => r));
        }

        /// <summary>
        /// 同时探索多个节点。适用于文章节点。注意，此函数仅在查询方面进行了优化，
        /// 如果其他线程正在探索此节点，则等待此节点探索完毕。
        /// </summary>
        private async Task FetchPapersAsync(IReadOnlyCollection<KgNode> paperNodes, string constraint)
        {
            Debug.Assert(paperNodes != null);
            if (constraint == null)
            {
                await FetchPapersAsync(paperNodes);
                return;
            }
            // 1. 注意，有些节点可能仍处于 正在探索 的状态。
            //      需要在返回前等待这些正在探索的节点。
            // 2. 很不幸，一旦标记一个节点开始探索后，没有办法再标记为未探索，
            //      所以在有约束的情况下，只能在内层循环中对节点分别标记。
            var nodesToFetch = paperNodes.Where(n => GetStatus(n.Id).GetFetchingStatus(NodeStatus.PaperFetching) == FetchingStatus.Unfetched)
                .ToArray();
            if (nodesToFetch.Length == 0) goto WAIT_FOR_EXPLORATIONS;
            await Task.WhenAll(nodesToFetch.Select(n => n.Id)
                .Partition(SEB.MaxChainedIdCount)
                .Select(ids =>
                {
                    // 假定 Partition 返回的是 IList / ICollection
                    var idc = (ICollection<long>) ids;
                    return SearchClient.EvaluateAsync(SEB.And(SEB.EntityIdIn(idc), constraint),
                        SEB.MaxChainedIdCount,
                        ConcurrentPagingMode.Optimistic,
                        async page =>
                        {
                            foreach (var entity in page.Entities)
                                await ExplorePaperAsync(entity);
                        });
                }));
            WAIT_FOR_EXPLORATIONS:
            ;
        }

        /// <summary>
        /// 异步探索作者的所有论文，顺便探索他/她在发表这些论文时所位于的机构。
        /// 如果其他线程正在探索此节点，则等待此节点探索完毕。
        /// </summary>
        private async Task FetchAuthorsPapersAsync(IReadOnlyCollection<AuthorNode> authorNodes)
        {
            // 有些类似于
            //      Task LocalExploreAsync(IEnumerable<PaperNode> paperNodes);
            // 探索 author 的所有论文。此处的探索还可以顺便确定 author 的所有组织。
            var nodesToExplore = authorNodes
                .Where(n => GetStatus(n.Id).TryMarkAsFetching(NodeStatus.AuthorPapersFetching))
                .ToArray();
            if (nodesToExplore.Length == 0) goto WAIT_FOR_EXPLORATIONS;
            // 随后，先把 authorNodes 注册一遍。
            var fetchTasks = nodesToExplore.Select(n => n.Id)
                .Partition(SEB.MaxChainedAuIdCount)
                .Select(async ids =>
                {
                    // 一次探索若干作者。这意味着不同作者的文章是混在一起的。
                    // 假定 Partition 返回的是 IList / ICollection
                    var idc = (ICollection<long>) ids;
                    //var explorationResult = 
                    await SearchClient.EvaluateAsync(SEB.AuthorIdIn(idc),
                        Assumptions.AuthorMaxPapers*idc.Count,
                        ConcurrentPagingMode.Optimistic, async page =>
                        {
                            foreach (var et in page.Entities)
                            {
                                // 此处还可以注册 paper 与其所有作者之间的关系。
                                // 这样做的好处是，万一 author1 和 author2 同时写了一篇论文。
                                // 在这里就可以发现了。
                                RegisterNode(et);
                                var localExploreTask = ExplorePaperAsync(et);
                                // 为检索结果里的所有作者注册所有可能的机构。
                                // 这里比较麻烦，因为一个作者可以属于多个机构，所以
                                // 不能使用 LocalExploreAsync （会认为已经探索过。）
                                // 而需要手动建立节点关系。
                                foreach (var au in et.Authors)
                                {
                                    if (au.AffiliationId == null) continue;
                                    var author = RegisterAuthorNode(au);
                                    var affiliation = RegisterAffiliationNode(au);
                                    RegisterEdge(author, affiliation);
                                }
                                await localExploreTask;
                            }
                            //return page.Entities.Count;
                        });
                    // 实际情况应当是， SUM(er.Entities.Count) >> idc.Count
                    //var pageSubtotal = explorationResult.Sum();
                    //if (pageSubtotal < idc.Count)
                    //    Logger.Magik.Warn(this, "批量查询实体 Id 时，返回结果数量不足。期望：>>{0}，实际：{1}。", idc.Count, pageSubtotal);
                    //return pageSubtotal;
                });
            //var subtotal = 
            await Task.WhenAll(fetchTasks);
            //var total = subtotal.Sum();
            // 标记为“已经探索过”。
            foreach (var an in nodesToExplore)
                GetStatus(an.Id).MarkAsFetched(NodeStatus.AuthorPapersFetching);
            WAIT_FOR_EXPLORATIONS:
            //确保返回前，所有 Fetching 的节点已经由此线程或其他线程处理完毕。
            var waitResult = await Task.WhenAll(authorNodes.Select(n =>
                GetStatus(n.Id).UntilFetchedAsync(NodeStatus.AuthorPapersFetching)));
            Debug.Assert(waitResult.All(r => r));
        }

        private async Task FetchCitationsToPaperAsync(PaperNode paper, string constraint, ConcurrentPagingMode pagingMode)
        {
            Debug.Assert(paper != null);
            if (constraint == null)
            {
                if (!await GetStatus(paper.Id).MarkAsFetchingOrUntilFetched(NodeStatus.PaperCitationsFetching))
                    return;
            }
            // 一般来说， Paper2 肯定就是题目中的终结点，
            // 因此我们是知道其具体信息的。
            var maxPapers = CitationCount(paper);
            var strategy = maxPapers == null
                ? ConcurrentPagingMode.Optimistic
                : pagingMode;
            if (maxPapers == null) maxPapers = Assumptions.PaperMaxCitations;
            var expr = SEB.ReferenceIdContains(paper.Id);
            if (constraint != null)
                expr = SEB.And(expr, constraint);
            await SearchClient.EvaluateAsync(expr, maxPapers.Value, strategy, async page =>
            {
                await Task.WhenAll(page.Entities.Select(entity =>
                {
                    RegisterNode(entity);
                    return ExplorePaperAsync(entity);
                }));
            });
            if (constraint == null)
                GetStatus(paper.Id).MarkAsFetched(NodeStatus.PaperCitationsFetching);
        }

        //    /// <summary>
        //    /// 使用直方图方法计算一个作者的所有机构。
        //    /// </summary>
        //    private async Task FetchAuthorAffiliations(long authorId)
        //    {
        //        affiliationIds.Partition(SEB.MaxChainedAfIdCount)
        //            .Select(afids =>
        //            {
        //              SearchClient.CalcHistogramAsync(SEB.AuthorIdContains(authorId), "")

        //            });
        //        var nodesToExplore = authorNodes
        //            .Where(n => GetStatus(n.Id).TryMarkAsFetching(NodeStatus.AuthorPapersFetching))
        //            .ToArray();
        //        if (nodesToExplore.Length == 0) goto WAIT_FOR_EXPLORATIONS;
        //        WAIT_FOR_EXPLORATIONS:
        //        //确保返回前，所有 Fetching 的节点已经由此线程或其他线程处理完毕。
        //        var waitResult = await Task.WhenAll(authorNodes.Select(n =>
        //            GetStatus(n.Id).UntilFetchedAsync(NodeStatus.AuthorPapersFetching)));
        //        Debug.Assert(waitResult.All(r => r));
        //}

        /// <summary>
        /// 探索作者与指定机构 Id 列表之间的关系。
        /// 此操作已进行优化，仅用于探索作者与机构之间的关系。
        /// </summary>
        private async Task FetchAuthorAffiliationRelations(long authorId, IEnumerable<long> affiliationIds)
        {
            var afidEnumerator = affiliationIds.GetEnumerator();
            var currentAfIds = new HashSet<long>();
            Action FeedAffiliationIds = () =>
            {
                while (currentAfIds.Count < SEB.MaxChainedAfIdCount
                       && afidEnumerator.MoveNext())
                {
                    // 我们只探索有必要探索的边。
                    if (!graph.Contains(authorId, afidEnumerator.Current))
                        currentAfIds.Add(afidEnumerator.Current);
                }
            };
            FeedAffiliationIds();
            while (currentAfIds.Count > 0)
            {
                // 直入主题，连多余的属性都不用包括。
                var er = await SearchClient.EvaluateAsync(SEB.AuthorIdContains(authorId, currentAfIds),
                    100, 0, GlobalServices.AuthorAffiliationASEvaluationAttributes);
                // 作者不属于 ids 中的任何一个机构。那就换一波吧。
                if (er.Entities.Count == 0) currentAfIds.Clear();
                // 包含结果。把这些机构排除掉。
                // 顺便把搜到的论文小探索一下。
                foreach (var et in er.Entities)
                {
                    foreach(var au in et.Authors)
                    {
                        if (au.AffiliationId != null)
                        {
                            // 为检索结果里的所有作者注册所有可能的机构。
                            // 这里比较麻烦，因为一个作者可以属于多个机构，所以
                            // 不能使用 LocalExploreAsync （会认为已经探索过。）
                            // 而需要手动建立节点关系。
                            RegisterNode(KgNode.CreateAffiliation(au));
                            RegisterEdge(au.Id, au.AffiliationId.Value, true);
                            currentAfIds.Remove(au.AffiliationId.Value);
                        }
                    }
                    //await localExplorationTask;
                }
                // 然后看看还有没有其他的机构来探索一下。
                FeedAffiliationIds();
            }
        }

        /// <summary>
        /// 根据指定的 Id ，获取论文或者作者节点，并对论文节点进行局部拓展。
        /// </summary>
        /// <returns>如果找不到符合要求的节点，则返回<c>null</c>。</returns>
        private async Task<KgNode> BeginExplorationAt(long id)
        {
            // 先尝试在节点缓存中查找。
            KgNode existing;
            if (nodes.TryGetValue(id, out existing))
            {
                var paper = existing as PaperNode;
                if (paper != null && await GetStatus(id).UntilFetchedAsync(NodeStatus.PaperFetching))
                    return existing;    // We're done.
                if (paper == null)
                {
                    // Author, etc.
                    // We're done.
                    return existing;
                }
            }
            // 去网上找找。
            var er = await SearchClient.EvaluateAsync(SEB.EntityOrAuthorIdEquals(id), 2, 0);
            if (er.Entities.Count == 0) return null;
            foreach (var et in er.Entities)
            {
                if (et.Authors == null || et.Authors.Length == 0)
                {
                    // 很有可能意味着这是一个作者节点，因为论文都是有作者的。
                    // ISSUE 如果试图将作者Id（AA.AuId）作为实体Id（Id）进行查询的话，
                    //          是可以查询出结果的。只是检索的结果除了 logprob 和 id
                    //          以外，其他属性都是空的。
                    // 所以需要跳过这一轮。
                    continue;
                }
                var au = et.Authors.FirstOrDefault(a => a.Id == id);
                if (au != null)
                {
                    return RegisterAuthorNode(au);
                }
                if (et.Id == id)
                {
                    var paper = RegisterNode(et);
                    await ExplorePaperAsync(et);
                    return paper;
                }
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
                    if (g.Key == typeof (PaperNode)
                        || g.Key == typeof (AuthorNode)
                        || g.Key == typeof (AffiliationNode)
                        || g.Key == typeof (FieldOfStudyNode))
                        // 论文、作者、组织、领域 可以批量处理。
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
            return Task.WhenAll(nodes1.Select(au => ExploreInterceptionNodesInternalAsync(new[] {au}, node2)));
        }

        /// <summary>
        /// 根据给定的两个节点，探索能够与这两个节点相连的中间结点集合。
        /// </summary>
        private Task ExploreInterceptionNodesAsync(KgNode node1, KgNode node2)
        {
            return ExploreInterceptionNodesInternalAsync(new[] {node1}, node2);
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
            if (nodes1.Count == 0) return; // Nothing to explore.
            KgNode node1;
            var explore12DomainKey = new InterceptionFetchingDomainKey(node2.Id);
            // 注意， node1 和 node2 的方向是不能互换的，
            // 所以不要想着把对侧也标记为已经探索过了。
            var nodes1ToExplore = nodes1
                .Where(n => GetStatus(n.Id).TryMarkAsFetching(explore12DomainKey))
                .ToArray();
            Debug.Assert(nodes1ToExplore.IsDistinct());
            if (nodes1ToExplore.Length == 0) goto WAIT_FOR_EXPLORATIONS;
            IReadOnlyCollection<PaperNode> papers1 = null;
            IReadOnlyCollection<AuthorNode> authors1 = null;
            IReadOnlyCollection<AffiliationNode> affiliations1 = null;
            IReadOnlyCollection<FieldOfStudyNode> foss1 = null;
            if (nodes1ToExplore.Length == 1)
            {
                node1 = nodes1ToExplore[0];
                if (node1 is PaperNode)
                    papers1 = new[] {(PaperNode) node1};
                else if (node1 is AuthorNode)
                    authors1 = new[] {(AuthorNode) node1};
                else if (node1 is AffiliationNode)
                    affiliations1 = new[] {(AffiliationNode) node1};
                else if (node1 is FieldOfStudyNode)
                    foss1 = new[] {(FieldOfStudyNode) node1};
            }
            else
            {
                node1 = null;
                if (nodes1ToExplore[0] is PaperNode)
                {
                    papers1 = nodes1ToExplore.CollectionCast<PaperNode>();
                }
                else if (nodes1ToExplore[0] is AuthorNode)
                {
                    authors1 = nodes1ToExplore.CollectionCast<AuthorNode>();
                }
                else if (nodes1ToExplore[0] is AffiliationNode)
                {
                    affiliations1 = nodes1ToExplore.CollectionCast<AffiliationNode>();
                }
                else if (nodes1ToExplore[0] is FieldOfStudyNode)
                {
                    foss1 = nodes1ToExplore.CollectionCast<FieldOfStudyNode>();
                }
                else
                {
                    throw new ArgumentException("集合包含元素的类型过于复杂。请尝试使用单元素集合多次调用。", nameof(node1));
                }
            }
            if (papers1 != null)
            {
                // 需要调查 papers1 的 AuId JId CId FId 等联结关系。
                await FetchPapersAsync(papers1);
            }
            var paper2 = node2 as PaperNode;
            if (paper2 != null)
                Debug.Assert(GetStatus(paper2.Id)
                    .GetFetchingStatus(NodeStatus.PaperFetching) == FetchingStatus.Fetched);
            PaperNode[] papers1References = null;
            if (papers1 != null)
            {
                // 注意，参考文献(或是作者——尽管在这里不需要)很可能会重复。
                papers1References = papers1.SelectMany(p1 => graph
                    .AdjacentOutVertices(p1.Id))
                    .Distinct()
                    .Select(id => nodes[id])
                    .OfType<PaperNode>()
                    .ToArray();
            }
            // searchConstraint : 建议尽量简短，因为 FromPapers1 的约束可能
            // 会是 100 个条件的并。
            Func<string, Task> FetchFromPapers1References = searchConstraint =>
            {
                // 注意，我们是来做全局搜索的。像是 Id -> AuId -> Id
                // 这种探索在探索论文时应该已经解决了。
                Debug.Assert(papers1 != null);
                Debug.Assert(papers1References != null);
                var tasks = papers1References
                    .Partition(SEB.MaxChainedIdCount)
                    .Select(nodes3 => FetchPapersAsync(nodes3, searchConstraint));
                return Task.WhenAll(tasks);
            };
            if (paper2 != null)
            {
                // 带有 作者/研究领域/会议/期刊 等属性限制，
                // 搜索 >引用< 中含有 paper2 的论文 Id。
                // attributeConstraint 可以长一些。
                if (papers1 != null)
                {
                    // Id1 -> Id3 -> Id2
                    //Trace.WriteLine("Fetch From Papers1 " + papers1.Count);
                    var citations = CitationCount(paper2);
                    var papersToFetch = papers1References.Length;
                    if (citations < papersToFetch)
                    {
                        // 从 Id2 方向向左探索 Id1 可能会好一些。
                        // 注意到 Left to Right 一次只能探索约 100 篇
                        // Right to Left 一次可以探索约 1000 篇，就是有点儿慢。
                        await FetchCitationsToPaperAsync(paper2, null, ConcurrentPagingMode.Pessimistic);
                    }
                    else
                    {
                        await FetchFromPapers1References(SEB.ReferenceIdContains(paper2.Id));
                    }
                }
                else if (authors1 != null)
                {
                    // AA.AuId <-> Id -> Id
                    await Task.WhenAll(authors1
                        .Select(n => n.Id)
                        .Partition(SEB.MaxChainedAuIdCount)
                        .Select(id1s => FetchCitationsToPaperAsync(paper2, SEB.AuthorIdIn(id1s),
                            ConcurrentPagingMode.Optimistic)));
                }
                else if (affiliations1 != null)
                {
                    // AA.AfId <-> AA.AuId <-> Id
                    // 注意，要确认 AuId 是否位于 nodes1 列表中。
                    // 这里的 AuId 是 Id2 论文的作者列表。
                    await Task.WhenAll(graph.AdjacentOutVertices(node2.Id)
                        .Where(id => nodes[id] is AuthorNode)
                        .Select(id => FetchAuthorAffiliationRelations(id, affiliations1.Select(af => af.Id))));
                }
                else if (foss1 != null)
                {
                    // F.FId <-> Id -> Id
                    await Task.WhenAll(foss1.Select(fos => fos.Id)
                        .Partition(SEB.MaxChainedFIdCount)
                        .Select(fids => FetchCitationsToPaperAsync(paper2, SEB.FieldOfStudyIdIn(fids),
                            ConcurrentPagingMode.Pessimistic)));
                }
                else if (node1 is ConferenceNode)
                {
                    // C.CId <-> Id -> Id
                    await FetchCitationsToPaperAsync(paper2, SEB.ConferenceIdEquals(node1.Id),
                        ConcurrentPagingMode.Optimistic);
                }
                else if (node1 is JournalNode)
                {
                    // J.JId <-> Id -> Id
                    await FetchCitationsToPaperAsync(paper2, SEB.JournalIdEquals(node1.Id),
                        ConcurrentPagingMode.Optimistic);
                }
                else
                {
                    Debug.WriteLine("Ignoreed: {0}-{1}", nodes1ToExplore[0], node2);
                }
            }
            else
            {
                Debug.Assert(node2 is AuthorNode);
                // 带有 研究领域/会议/期刊 等属性限制，
                // 搜索 author2 的论文 Id。
                // attributeConstraint 可以长一些。
                Func<string, Task> ExplorePapersOfAuthor2WithAttributes = async attributeConstraint =>
                {
                    // 如果作者的所有论文已经被探索过了，
                    // 那么很幸运，不需要再探索了。
                    if (await GetStatus(node2.Id).UntilFetchedAsync(NodeStatus.AuthorPapersFetching))
                        return;
                    await SearchClient.EvaluateAsync(SEB.And(
                        attributeConstraint, SEB.AuthorIdContains(node2.Id)),
                        Assumptions.AuthorMaxPapers, ConcurrentPagingMode.Optimistic, page =>
                            Task.WhenAll(page.Entities.Select(async entity =>
                            {
                                RegisterNode(entity);
                                await ExplorePaperAsync(entity);
                            })));
                };
                if (papers1 != null)
                {
                    // Id1 -> Id3 -> AA.AuId2
                    await FetchFromPapers1References(SEB.AuthorIdContains(node2.Id));
                }
                else if (authors1 != null)
                {
                    // AA.AuId1 <-> Id3 <-> AA.AuId2
                    // 探索 AA.AuId1 的所有论文。此处的探索还可以顺便确定 AuId1 的所有组织。
                    // 注意到每个作者都会写很多论文
                    // 不论如何，现在尝试从 Id1 向 Id2 探索。
                    // 我们需要列出 Id1 的所有文献，以获得其曾经位于的所有组织。
                    await FetchAuthorsPapersAsync(authors1);
                    // AA.AuId1 <-> AA.AfId3 <-> AA.AuId2
                    // AA.AuId1 <-> AA.AfId3 已经在前面探索完毕。
                    // 只需探索 AA.AfId3 <-> AA.AuId2 。
                    await FetchAuthorAffiliationRelations(node2.Id, authors1
                        .SelectMany(au1 => graph.AdjacentOutVertices(au1.Id)
                            .Where(id2 => nodes[id2] is AffiliationNode)));
                }
                else if (affiliations1 != null)
                {
                    // 不可能和 Affiliations 扯上 2-hop 关系啦……
                    // AA.AfId - ? - AA.AuId
                }
                else if (foss1 != null)
                {
                    // F.FId <-> Id <-> AA.AuId
                    await Task.WhenAll(foss1.Select(fos => fos.Id)
                        .Partition(SEB.MaxChainedFIdCount)
                        .Select(fids => ExplorePapersOfAuthor2WithAttributes(SEB.FieldOfStudyIdIn(fids))));
                }
                else if (node1 is ConferenceNode)
                {
                    // C.CId <-> Id <-> AA.AuId
                    await ExplorePapersOfAuthor2WithAttributes(SEB.ConferenceIdEquals(node1.Id));
                }
                else if (node1 is JournalNode)
                {
                    // J.JId <-> Id <-> AA.AuId
                    await ExplorePapersOfAuthor2WithAttributes(SEB.JournalIdEquals(node1.Id));
                }
                else
                {
                    Debug.WriteLine("Ignoreed: {0}-{1}", nodes1ToExplore[0], node2);
                }
            }
            // 标记探索完毕
            foreach (var n in nodes1ToExplore)
                GetStatus(n.Id).MarkAsFetched(explore12DomainKey);
            // 等待其他线程（如果有）
            WAIT_FOR_EXPLORATIONS:
            var waitResult =
                await Task.WhenAll(nodes1.Select(n => GetStatus(n.Id).UntilFetchedAsync(explore12DomainKey)));
            Debug.Assert(waitResult.All(r => r));
        }
    }
}
