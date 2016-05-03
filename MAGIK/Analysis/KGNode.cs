using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;
using Microsoft.Contests.Bop.Participants.Magik.Academic.Contract;

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    /// <summary>
    /// 表示知识图上的一个节点。这是一个公共基类。
    /// 并且，我正试图让这个类成为一个非可变类型。
    /// </summary>
    public abstract class KgNode
    {
        /// <summary>
        /// 表示一个空白的结点集合。
        /// </summary>
        public static readonly KgNode[] EmptyNodes = new KgNode[0];

        protected KgNode(long id, string name)
        {
            Debug.Assert(id != 0);
            Id = id;
            Name = name;
        }

        /// <summary>
        /// 节点的 Id 。这是一个节点的本体。
        /// </summary>
        public long Id { get; }

        /// <summary>
        /// 节点的名称。例如论文的标题、作者的姓名等。
        /// 此属性仅为了调试方便使用。生产环境中，为了减少网络传输，可以只接收 Id 。
        /// </summary>
        public string Name { get; private set; }
        // 为了调试方便，允许在此类型接收到更多网络数据后，设置 Name
        // 但如此会在一定程度上破坏此类型的不可变性。

        /// <summary>
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。
        /// 注意，此函数对图进行简单探索。对于潜在的可能列出大量结果的情况，
        /// 此函数不予考虑。这一类查询需要根据上下文进一步限定条件。
        /// </summary>
        /// <param name="asClient"></param>
        /// <seealso cref="Analyzer.ExploreInterceptionNodesAsync"/>
        public abstract Task<ICollection<KgNode>> GetAdjacentNodesAsync(AcademicSearchClient asClient);

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        public override string ToString()
        {
            return $"[{GetType().Name}:{Id}] {Name?.ToTitleCase()}";
        }
    }

    /// <summary>
    /// 论文节点。
    /// </summary>
    public sealed class PaperNode : KgNode
    {
        private readonly List<KgNode> loadedNodes;

        public PaperNode(Entity entity) : base(entity.Id, entity.Title)
        {
            loadedNodes = ParseEntity(entity);
        }

        public PaperNode(long id, string name) : base(id, name)
        {
            loadedNodes = null;
        }

        private void ValidateCache()
        {
            if (loadedNodes == null)
                throw new NotSupportedException("当前论文节点不是由 Entity 生成的，因此无法获得更多信息。");
        }

        private List<KgNode> ParseEntity(Entity entity)
        {
            Debug.Assert(entity != null);
            // Id <-> AA.AuId
            var nodes = entity.Authors.Select(au =>
                new AuthorNode(au)).Cast<KgNode>().ToList();
            // *Id -> Id (RId)
            if (entity.ReferenceIds != null)
                nodes.AddRange(entity.ReferenceIds.Select(rid =>
                    new PaperNode(rid, null)));
            // Id <-> J.JId
            if (entity.Journal != null)
                nodes.Add(new JournalNode(entity.Journal));
            // Id <-> C.CId
            if (entity.Conference != null)
                nodes.Add(new ConferenceNode(entity.Conference));
            // Id <-> F.FId
            if (entity.FieldsOfStudy != null)
                nodes.AddRange(entity.FieldsOfStudy.Select(fos =>
                    new FieldOfStudyNode(fos)));
            return nodes;
        }

        /// <summary>
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。
        /// （除了反向引用节点以外，因为这样的节点可能会很多。）
        /// </summary>
        /// <param name="asClient"></param>
        public override async Task<ICollection<KgNode>> GetAdjacentNodesAsync(AcademicSearchClient asClient)
        {
            var nodes = loadedNodes;
            if (loadedNodes == null)
            {
                var er = await asClient
                    .EvaluateAsync(SearchExpressionBuilder.EntityIdEquals(Id), 2, 0);
                if (er.Entities.Count == 0)
                {
                    Logger.Magik.Trace(this, "找不到 Id 对应的实体。");
                    return EmptyNodes;
                }
                if (er.Entities.Count != 1)
                    Logger.Magik.Trace(this, "Id 请求返回的实体不唯一。");
                nodes = ParseEntity(er.Entities[0]);
            }
            return nodes;
        }

        /// <summary>
        /// 判断此节点是否为仅包含论文 Id，不包含其它任何信息的存根。
        /// </summary>
        public bool IsStub => loadedNodes == null;

        /// <summary>
        /// 对于由 Entity 生成的 PaperNode ，直接获取本地缓存中的作者信息。
        /// </summary>
        public IEnumerable<AuthorNode> Authors
        {
            get
            {
                ValidateCache();
                return loadedNodes.OfType<AuthorNode>();
            }
        }
    }

    /// <summary>
    /// 作者节点。
    /// </summary>
    public sealed class AuthorNode : KgNode
    {
        // 注意！一个作者可以分属不同的机构。但一般也就属于那么几家而已。
        private static readonly PaperNode[] EmptyPapers = new PaperNode[0];
        private static readonly AffiliationNode NoLocalCacheFlag = new AffiliationNode(-1, null);
        private readonly AffiliationNode _LoadedAffiliation;

        public AuthorNode(Author author) : base(author.Id, author.Name)
        {
            if (author.AffiliationId != null)
                _LoadedAffiliation = new AffiliationNode(author);
        }

        public AuthorNode(long id, string name) : base(id, name)
        {
            _LoadedAffiliation = NoLocalCacheFlag;
        }

        /// <summary>
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。
        /// </summary>
        /// <param name="asClient"></param>
        public override Task<ICollection<KgNode>> GetAdjacentNodesAsync(AcademicSearchClient asClient)
        {
            // AA.AuId -> Id
            // Id -> AA.AuId
            // AA.AuId -> AA.AfId
            // AA.AfId -> AA.AuId
            // 我们应当注意到，一个作者可以分属不同的机构。
            // 另外，为了避免列出作者的所有文章，所有和作者相关的邻节点检索全部在
            // Analyzer.ExploreInterceptionNodesAsync 中完成。
            return Task.FromResult((ICollection<KgNode>) (
                _LoadedAffiliation != null
                    ? new[] {_LoadedAffiliation}
                    : EmptyNodes));
        }

        /// <summary>
        /// 检索此作者参与的所有论文。
        /// </summary>
        public async Task<ICollection<PaperNode>> GetPapersAsync(AcademicSearchClient asClient)
        {
            var er = await asClient.EvaluateAsync(
                SearchExpressionBuilder.AuthorIdContains(Id),
                Assumptions.AuthorMaxPapers);
            if (er.Entities.Count == 0) return EmptyPapers;
            // 啊哈！我们应当注意到，一个作者可以分属不同的机构。
            // 另外，也许我们可以充分利用搜索结果，因为在搜索结果里面也包含了和
            //      这个作者协作的其他作者的机构信息。
            return er.Entities
                .Select(et => new PaperNode(et))
                .ToArray();
        }

        /// <summary>
        /// 对于由 Entity 生成的 AuthorNode ，直接获取本地缓存中的作者所属机构信息。
        /// </summary>
        public AffiliationNode Affiliation
        {
            get
            {
                if (_LoadedAffiliation == NoLocalCacheFlag)
                    throw new NotSupportedException("当前作者节点不是由 Entity/Author 生成的，因此无法获得更多信息。");
                return _LoadedAffiliation;
            }
        }
    }

    /// <summary>
    /// 组织节点。
    /// </summary>
    public sealed class AffiliationNode : KgNode
    {
        public AffiliationNode(Author author) : base(author.AffiliationId.Value, author.AffiliationName)
        {
        }

        public AffiliationNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。
        /// </summary>
        /// <param name="asClient"></param>
        public override Task<ICollection<KgNode>> GetAdjacentNodesAsync(AcademicSearchClient asClient)
        {
            return Task.FromResult((ICollection<KgNode>) EmptyNodes);
        }
    }

    /// <summary>
    /// 期刊节点。
    /// </summary>
    public sealed class JournalNode : KgNode
    {
        public const int JOURNAL_MAX_PAPERS = 2000000;

        public JournalNode(Journal entity) : base(entity.Id, entity.Name)
        {
        }

        public JournalNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。
        /// </summary>
        /// <param name="asClient"></param>
        public override Task<ICollection<KgNode>> GetAdjacentNodesAsync(AcademicSearchClient asClient)
        {
            return Task.FromResult((ICollection<KgNode>)EmptyNodes);
        }
    }

    /// <summary>
    /// 会议节点。
    /// </summary>
    public sealed class ConferenceNode : KgNode
    {
        public ConferenceNode(Conference entity) : base(entity.Id, entity.Name)
        {
        }

        public ConferenceNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。
        /// </summary>
        /// <param name="asClient"></param>
        public override Task<ICollection<KgNode>> GetAdjacentNodesAsync(AcademicSearchClient asClient)
        {
            return Task.FromResult((ICollection<KgNode>)EmptyNodes);
        }
    }

    /// <summary>
    /// 研究领域节点。
    /// </summary>
    public sealed class FieldOfStudyNode : KgNode
    {
        public FieldOfStudyNode(FieldOfStudy entity) : base(entity.Id, entity.Name)
        {
        }

        public FieldOfStudyNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。
        /// </summary>
        /// <param name="asClient"></param>
        public override Task<ICollection<KgNode>> GetAdjacentNodesAsync(AcademicSearchClient asClient)
        {
            return Task.FromResult((ICollection<KgNode>)EmptyNodes);
        }
    }

    /// <summary>
    /// 用于根据 Id 比较两个 <see cref="KgNode"/> 的等价性。
    /// </summary>
    public class KgNodeEqualityComparer : EqualityComparer<KgNode>
    {
        public new static readonly KgNodeEqualityComparer Default = new KgNodeEqualityComparer();

        /// <summary>
        /// 确定类型的两个对象的 Id 是否相等。
        /// </summary>
        /// <returns>
        /// 如果指定的对象相等，则为 true；否则为 false。
        /// </returns>
        /// <param name="x">要比较的第一个对象。</param><param name="y">要比较的第二个对象。</param>
        public override bool Equals(KgNode x, KgNode y)
        {
            return x?.Id == y?.Id;
        }

        /// <summary>
        /// 在派生类中重写时，用作指定对象的哈希算法和数据结构（如哈希表）的哈希函数。
        /// </summary>
        /// <returns>
        /// 指定对象的哈希代码。
        /// </returns>
        /// <param name="obj">要为其获取哈希代码的对象。</param><exception cref="T:System.ArgumentNullException">The type of <paramref name="obj"/> is a reference type and <paramref name="obj"/> is null.</exception>
        public override int GetHashCode(KgNode obj)
        {
            return obj?.Id.GetHashCode() ?? 0;
        }
    }
}
