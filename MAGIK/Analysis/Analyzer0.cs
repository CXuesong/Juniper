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
        private async Task ExploreInterceptionNodesAsync(KgNode node1, KgNode node2)
        {
            if (node1 == null) throw new ArgumentNullException(nameof(node1));
            if (node2 == null) throw new ArgumentNullException(nameof(node2));
            // 在进行上下文相关探索之前，先对两个节点进行局部探索。
            await Task.WhenAll(LocalExploreAsync(node1), LocalExploreAsync(node2));
            var paper1 = node1 as PaperNode;
            var paper2 = node2 as PaperNode;
            Func<string, Task> ExploreFromPaper1References = (searchConstraint) =>
            {
                var tasks = graph
                    .AdjacentOutVertices(paper1.Id)
                    .Where(id3 => nodes[id3] is PaperNode)
                    .Partition(SEB.MaxChainedIdCount)
                    .Select(async id3s =>
                    {
                        var er = await GlobalServices.ASClient.EvaluateAsync(SEB.And(
                            SEB.EntityIdIn(id3s),
                            searchConstraint),
                            SEB.MaxChainedIdCount);
                        foreach (var et in er.Entities)
                        {
                            RegisterNode(new PaperNode(et));
                            // Id1 -> Id3
                            // 注意，这里是单向边。
                            RegisterEdge(paper1.Id, et.Id, false);
                            // Id3 -> Id2
                            // (Id3 <- ??Id2)
                            RegisterEdge(et.Id, node2.Id, !(node2 is PaperNode));
                        }
                    });
                return Task.WhenAll(tasks);
            };
            // 带有 作者/会议/期刊 等属性限制，搜索引用中含有 paper2 的论文 Id。
            Func<string, int, Task> ExploreToPaper2WithAttributes = async (attributeConstraint, maxPapers) =>
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
                if (paper1 != null)
                {
                    // Id1 -> Id3 -> Id2
                    await ExploreFromPaper1References(SEB.ReferenceIdContains(paper2.Id));
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
                if (paper1 != null)
                {
                    // Id1 -> Id3 -> AA.AuId2
                    await ExploreFromPaper1References(SEB.AuthorIdContains(node2.Id));
                }
                else if (node1 is AuthorNode)
                {
                    // AA.AuId1 <-> Id3 <-> AA.AuId2
                    var author1 = (AuthorNode) node1;
                    // 探索 AA.AuId1 的所有论文。此处的探索还可以顺便确定 AuId1 的所有组织。
                    // 注意到每个作者都会写很多论文
                    // 不论如何，现在尝试从 Id1 向 Id2 探索。
                    // 我们需要列出 Id1 的所有文献，以获得其曾经位于的所有组织。
                    if (await GetStatus(author1.Id).MarkAsExploringOrUntilExplored(NodeStatus.AuthorPapersExploration))
                    {
                        foreach (var paper in await author1.GetPapersAsync())
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
                            foreach (var author in paper.Authors)
                            {
                                // AA.AuId <-> AA.AfId
                                // 其中必定包括
                                // AA.AuId1 <-> AA.AfId3
                                RegisterNode(author);
                                if (author.Affiliation != null)
                                {
                                    RegisterNode(author.Affiliation);
                                    RegisterEdge(author.Id, author.Affiliation.Id, true);
                                }
                            }
                        }
                        GetStatus(author1.Id).MarkAsExplored(NodeStatus.AuthorPapersExploration);
                    }
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
