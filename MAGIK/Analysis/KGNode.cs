using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public abstract class KgNode : IEquatable<KgNode>
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
        /// <seealso cref="Analyzer.ExploreInterceptionNodesInternalAsync"/>
        public virtual IEnumerable<KgNode> GetAdjacentNodes()
        {
            return Enumerable.Empty<KgNode>();
        }

        /// <summary>
        /// 指示当前对象是否等于同一类型的另一个对象。
        /// </summary>
        /// <returns>
        /// 如果当前对象等于 <paramref name="other"/> 参数，则为 true；否则为 false。
        /// </returns>
        /// <param name="other">与此对象进行比较的对象。</param>
        public bool Equals(KgNode other)
        {
            if (other == null) return false;
            if (this == other) return true;
            return Id == other.Id;
        }

        /// <summary>
        /// 作为默认哈希函数。
        /// </summary>
        /// <returns>
        /// 当前对象的哈希代码。
        /// </returns>
        public override int GetHashCode()
        {
            return Id.GetHashCode();
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
            var other = obj as KgNode;
            if (other == null) return false;
            return Equals(other);
        }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        public override string ToString()
        {
            return $"[{GetType().Name}:{Id}] {Name?.ToTitleCase()}";
        }
    }

    /// <summary>
    /// 论文节点的基类型。注意，此类型有且仅有两个派生类。
    /// </summary>
    public abstract class PaperNodeBase : KgNode
    {
        protected PaperNodeBase(long id, string name) : base(id, name)
        {
        }
    }


    /// <summary>
    /// 不包含其它连接关系的论文节点。
    /// </summary>
    public sealed class PaperNodeStub : PaperNodeBase
    {
        public PaperNodeStub(long id) : base(id, null)
        {

        }

        public PaperNodeStub(long id, string name) : base(id, name)
        {

        }
    }

    /// <summary>
    /// 论文节点。
    /// </summary>
    public sealed class PaperNode : PaperNodeBase
    {
        private readonly JournalNode _Journal;
        private readonly ConferenceNode _Conference;
        private readonly ICollection<AuthorNode> _Authors;
        private readonly ICollection<FieldOfStudyNode> _FieldsOfStudy;
        private readonly ICollection<PaperNodeStub> _References;
        private readonly int _CitationCount;

        public PaperNode(Entity entity) : base(entity.Id, entity.Title)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _CitationCount = entity.CitationCount;
            // Id <-> AA.AuId
            _Authors = entity.Authors.Select(au => new AuthorNode(au)).ToList().AsReadOnly();
            // *Id -> Id (RId)
            _References = entity.ReferenceIds?.Select(id => new PaperNodeStub(id)).ToList().AsReadOnly()
                          ?? Array.AsReadOnly(new PaperNodeStub[0]);
            // Id <-> J.JId
            if (entity.Journal != null)
                _Journal = new JournalNode(entity.Journal);
            // Id <-> C.CId
            if (entity.Conference != null)
                _Conference = new ConferenceNode(entity.Conference);
            // Id <-> F.FId
            _FieldsOfStudy = entity.FieldsOfStudy?.Select(fos => new FieldOfStudyNode(fos)).ToList().AsReadOnly()
                             ?? Array.AsReadOnly(new FieldOfStudyNode[0]);
        }

        public override IEnumerable<KgNode> GetAdjacentNodes()
        {
            if (_Journal != null) yield return _Journal;
            if (_Conference != null) yield return _Conference;
            foreach (var a in _Authors) yield return a;
            foreach (var f in _FieldsOfStudy) yield return f;
            foreach (var r in _References) yield return r;
        }

        /// <summary>
        /// 论文被引用的次数。
        /// </summary>
        public int CitationCount => _CitationCount;
    }

    /// <summary>
    /// 作者节点。
    /// </summary>
    public sealed class AuthorNode : KgNode
    {
        // 注意！一个作者可以分属不同的机构。但一般也就属于那么几家而已。
        private static readonly PaperNodeBase[] EmptyPapers = new PaperNodeBase[0];
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
        public override IEnumerable<KgNode> GetAdjacentNodes()
        {
            // AA.AuId -> Id
            // Id -> AA.AuId
            // AA.AuId -> AA.AfId
            // AA.AfId -> AA.AuId
            // 我们应当注意到，一个作者可以分属不同的机构。
            // 另外，为了避免列出作者的所有文章，所有和作者相关的邻节点检索全部在
            // Analyzer.ExploreInterceptionNodesAsync 中完成。
            return _LoadedAffiliation != null
                ? new[] {_LoadedAffiliation}
                : EmptyNodes;
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
    }

    /// <summary>
    /// 期刊节点。
    /// </summary>
    public sealed class JournalNode : KgNode
    {
        public JournalNode(Journal entity) : base(entity.Id, entity.Name)
        {
        }

        public JournalNode(long id, string name) : base(id, name)
        {
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
    }
}
