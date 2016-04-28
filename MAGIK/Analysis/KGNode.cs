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
        /// 异步枚举由此节点指向的邻节点。
        /// </summary>
        public abstract Task<ICollection<KgNode>> GetAdjacentOutNodesAsync();

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        public override string ToString()
        {
            return $"[{GetType().Name}:{Id}]{Name}";
        }
    }

    /// <summary>
    /// 论文节点。
    /// </summary>
    public class PaperNode : KgNode
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

        private static List<KgNode> ParseEntity(Entity entity)
        {
            Debug.Assert(entity != null);
            var nodes = entity.Authors.Select(au => (KgNode) new AuthorNode(au)).ToList();
            if (entity.ReferenceIds != null)
                nodes.AddRange(entity.ReferenceIds.Select(rid => new PaperNode(rid, null)));
            if (entity.FieldsOfStudy != null)
                nodes.AddRange(entity.FieldsOfStudy.Select(fos => new FieldOfStudyNode(fos)));
            if (entity.Journal != null) nodes.Add(new JournalNode(entity.Journal));
            if (entity.Conference != null) nodes.Add(new ConferenceNode(entity.Conference));
            return nodes;
        }

        /// <summary>
        /// 异步枚举由此节点指向的邻节点。
        /// </summary>
        public override async Task<ICollection<KgNode>> GetAdjacentOutNodesAsync()
        {
            if (loadedNodes == null)
            {
                var er = await GlobalServices.ASClient
                    .EvaluateAsync(SearchExpressionBuilder.EntityIdEquals(Id), 2, 0);
                if (er.Entities.Length == 0)
                {
                    Logging.Trace(this, "找不到 Id 对应的实体。");
                    return EmptyNodes;
                }
                if (er.Entities.Length != 1)
                    Logging.Trace(this, "Id 请求返回的实体不唯一。");
                var nodes = ParseEntity(er.Entities[0]);
                return nodes;
            }
            return loadedNodes;
        }
    }

    /// <summary>
    /// 作者节点。
    /// </summary>
    public class AuthorNode : KgNode
    {
        /// <summary>
        /// 我们需要调查一下一个作者究竟能有多高产。
        /// </summary>
        public const int AUTHOR_MAX_PAPERS = 2000;

        // 因为一个作者可以分属不同的机构，所以在这里缓存是没有什么卵用的。
        //private AffiliationNode _LoadedAffiliation;

        public AuthorNode(Author entity) : base(entity.Id, entity.Name)
        {
            //_LoadedAffiliation = new AffiliationNode(entity);
        }

        public AuthorNode(long id, string name) : base(id, name)
        {
            //_LoadedAffiliation = null;
        }

        /// <summary>
        /// 异步枚举由此节点指向的邻节点。
        /// </summary>
        public override async Task<ICollection<KgNode>> GetAdjacentOutNodesAsync()
        {
            var er = await GlobalServices.ASClient
                .EvaluateAsync(SearchExpressionBuilder.AuthorIdEquals(Id), AUTHOR_MAX_PAPERS, 0);
            if (er.Entities.Length == 0) return EmptyNodes;
            // AA.Id -> Id
            var nodes = er.Entities.Select(et => (KgNode) new PaperNode(et)).ToList();
            // AA.Id -> AA.AfId
            // 啊哈！我们应当注意到，一个作者可以分属不同的机构。
            // 另外，也许我们可以充分利用搜索结果，因为在搜索结果里面也包含了和
            //      这个作者协作的其他作者的机构信息。
            var affiliations = er.Entities
                .Select(et => et.Authors.First(au => au.Id == this.Id))
                .Distinct(AuthorAffiliationComparer.Default)
                .Select(au => new AffiliationNode(au));
            nodes.AddRange(affiliations);
            return nodes;
        }
    }

    /// <summary>
    /// 组织节点。
    /// </summary>
    public class AffiliationNode : KgNode
    {
        /// <summary>
        /// TODO 我们需要调查一下一个机构究竟能有多高产。
        /// </summary>
        public const int AFFILIATION_MAX_PAPERS = 1000;

        public AffiliationNode(Author entity) : base(entity.AffiliationId, entity.AffiliationName)
        {
        }

        public AffiliationNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由此节点指向的邻节点。（很慢！）
        /// </summary>
        public override async Task<ICollection<KgNode>> GetAdjacentOutNodesAsync()
        {
            // TODO 引入一个 AttributeBuilder 或使用按位枚举代替 attribute 表达式
            // 找出 1091 人 + 1400 人大概需要 45 秒。
            // 我们只需要找到作者。
            var er = await GlobalServices.ASClient
                .EvaluateAsync(SearchExpressionBuilder.AffiliationIdEquals(Id), AFFILIATION_MAX_PAPERS, 0,
                    "AA.AuId,AA.AuN,AA.AfId,AA.AfN");
            if (er.Entities.Length == 0) return EmptyNodes;
            // AA.AfId -> AA.AuId
            // 注意到，一篇文章可能有多个作者属于当前 Id 对应的机构。
            // 所以使用 SelectMany + Where 而非 Select + First
            var nodes = er.Entities
                .SelectMany(et => et.Authors.Where(au => au.AffiliationId == this.Id))
                .Distinct(AuthorIdComparer.Default)
                .Select(au => (KgNode) new AuthorNode(au))
                .ToList();
            return nodes;
        }
    }

    /// <summary>
    /// 期刊节点。
    /// </summary>
    public class JournalNode : KgNode
    {
        /// <summary>
        /// TODO 我们需要调查一下一个杂志究竟能有多高产。
        /// </summary>
        public const int JOURNAL_MAX_PAPERS = 1000;

        public JournalNode(Journal entity) : base(entity.Id, entity.Name)
        {
        }

        public JournalNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由此节点指向的邻节点。（慢！）
        /// </summary>
        public override async Task<ICollection<KgNode>> GetAdjacentOutNodesAsync()
        {
            var er = await GlobalServices.ASClient
                .EvaluateAsync(SearchExpressionBuilder.JournalIdEquals(Id), JOURNAL_MAX_PAPERS, 0);
            if (er.Entities.Length == 0) return EmptyNodes;
            // J.JId -> Id
            var nodes = er.Entities
                .Select(et => (KgNode)new PaperNode(et))
                .ToList();
            return nodes;
        }
    }

    /// <summary>
    /// 会议节点。
    /// </summary>
    public class ConferenceNode : KgNode
    {
        /// <summary>
        /// TODO 我们需要调查一下一个会议究竟能有多高产。
        /// </summary>
        public const int CONFERENCE_MAX_PAPERS = 1000;

        public ConferenceNode(Conference entity) : base(entity.Id, entity.Name)
        {
        }

        public ConferenceNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由此节点指向的邻节点。（慢！）
        /// </summary>
        public override async Task<ICollection<KgNode>> GetAdjacentOutNodesAsync()
        {
            var er = await GlobalServices.ASClient
                .EvaluateAsync(SearchExpressionBuilder.ConferenceIdEquals(Id), CONFERENCE_MAX_PAPERS, 0);
            if (er.Entities.Length == 0) return EmptyNodes;
            // C.CId -> Id
            var nodes = er.Entities
                .Select(et => (KgNode)new PaperNode(et))
                .ToList();
            return nodes;
        }
    }

    /// <summary>
    /// 研究领域节点。
    /// </summary>
    public class FieldOfStudyNode : KgNode
    {
        public FieldOfStudyNode(FieldOfStudy entity) : base(entity.Id, entity.Name)
        {
        }

        public FieldOfStudyNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由此节点指向的邻节点。（特别慢！）
        /// </summary>
        public override Task<ICollection<KgNode>> GetAdjacentOutNodesAsync()
        {
            // …… 以至于我已经不忍心写出来了 ||-_-
            return Task.FromResult<ICollection<KgNode>>(EmptyNodes);
        }
    }
}
