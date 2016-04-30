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
                        var er = await GlobalServices.ASClient.EvaluateAsync(SEB.And(
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
                    var er = await GlobalServices.ASClient.EvaluateAsync(SEB.And(
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
                            if (await GlobalServices.ASClient.EvaluationHasResult(
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
