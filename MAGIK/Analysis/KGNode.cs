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
    /// 并且是一个非可变类型。
    /// </summary>
    public abstract class KgNode : IEquatable<KgNode>
    {

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
        public string Name { get; }

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

        public static PaperNode Create(Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return new PaperNode(entity.Id, entity.Title);
        }

        public static JournalNode Create(Journal journal)
        {
            if (journal == null) throw new ArgumentNullException(nameof(journal));
            return new JournalNode(journal.Id, journal.Name);
        }

        public static ConferenceNode Create(Conference conference)
        {
            if (conference == null) throw new ArgumentNullException(nameof(conference));
            return new ConferenceNode(conference.Id, conference.Name);
        }

        public static FieldOfStudyNode Create(FieldOfStudy fos)
        {
            if (fos == null) throw new ArgumentNullException(nameof(fos));
            return new FieldOfStudyNode(fos.Id, fos.Name);
        }

        public static AuthorNode CreateAuthor(Author author)
        {
            if (author == null) throw new ArgumentNullException(nameof(author));
            return new AuthorNode(author.Id, author.Name);
        }

        public static AffiliationNode CreateAffiliation(Author author)
        {
            if (author == null) throw new ArgumentNullException(nameof(author));
            if (author.AffiliationId == null) throw new ArgumentException("无机构信息。", nameof(author));
            return new AffiliationNode(author.AffiliationId.Value, author.AffiliationName);
        }

        public static IEnumerable<KgNode> EnumerateLocalAdjacents(Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            // Id <-> AA.AuId
            foreach (var a in entity.Authors) yield return CreateAuthor(a);
            // *Id -> Id (RId)
            if (entity.ReferenceIds != null)
                foreach (var id in entity.ReferenceIds) yield return new PaperNode(id, null);
            // Id <-> J.JId
            if (entity.Journal != null)
                yield return Create(entity.Journal);
            // Id <-> C.CId
            if (entity.Conference != null)
                yield return Create(entity.Conference);
            // Id <-> F.FId
            if (entity.FieldsOfStudy != null)
                foreach (var fos in entity.FieldsOfStudy) yield return Create(fos);
        }
    }

    /// <summary>
    /// 论文节点。
    /// </summary>
    public sealed class PaperNode : KgNode
    {
        public PaperNode(long id, string name) : base(id, name)
        {
        }
    }

    /// <summary>
    /// 作者节点。
    /// </summary>
    public sealed class AuthorNode : KgNode
    {
        public AuthorNode(long id, string name) : base(id, name)
        {
        }
    }

    /// <summary>
    /// 组织节点。
    /// </summary>
    public sealed class AffiliationNode : KgNode
    {
        public AffiliationNode(long id, string name) : base(id, name)
        {
        }
    }

    /// <summary>
    /// 期刊节点。
    /// </summary>
    public sealed class JournalNode : KgNode
    {
        public JournalNode(long id, string name) : base(id, name)
        {
        }
    }

    /// <summary>
    /// 会议节点。
    /// </summary>
    public sealed class ConferenceNode : KgNode
    {
        public ConferenceNode(long id, string name) : base(id, name)
        {
        }
    }

    /// <summary>
    /// 研究领域节点。
    /// </summary>
    public sealed class FieldOfStudyNode : KgNode
    {
        public FieldOfStudyNode(long id, string name) : base(id, name)
        {
        }
    }
}
