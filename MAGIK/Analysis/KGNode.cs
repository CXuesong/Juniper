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
        public static readonly KgNodePair[] EmptyNodePairs = new KgNodePair[0];

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
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。
        /// </summary>
        public abstract Task<ICollection<KgNodePair>> GetAdjacentNodesAsync();

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
    public class PaperNode : KgNode
    {
        private const int PAPER_MAX_CITED = 100000;

        private readonly List<KgNodePair> loadedNodes;

        public PaperNode(Entity entity) : base(entity.Id, entity.Title)
        {
            loadedNodes = ParseEntity(entity);
        }

        public PaperNode(long id, string name) : base(id, name)
        {
            loadedNodes = null;
        }

        private List<KgNodePair> ParseEntity(Entity entity)
        {
            Debug.Assert(entity != null);
            var nodes = new List<KgNodePair>();
            // Id -> AA.AuId
            // AA.AuId -> Id
            foreach (var au in entity.Authors)
            {
                var p = new KgNodePair(this, new AuthorNode(au));
                nodes.Add(p);
                nodes.Add(p.Reverse());
            }
            // *Id -> Id (RId)
            if (entity.ReferenceIds != null)
                nodes.AddRange(entity.ReferenceIds.Select(rid => new KgNodePair(this, new PaperNode(rid, null))));
            // Id -> J.JId
            if (entity.Journal != null)
            {
                var p = new KgNodePair(this, new JournalNode(entity.Journal));
                nodes.Add(p);
                nodes.Add(p.Reverse());
            }
            // Id -> C.CId
            if (entity.Conference != null)
            {
                var p = new KgNodePair(this, new ConferenceNode(entity.Conference));
                nodes.Add(p);
                nodes.Add(p.Reverse());
            }
            // Id -> F.FId
            // F.FId -> Id
            if (entity.FieldsOfStudy != null)
            {
                foreach (var fos in entity.FieldsOfStudy)
                {
                    var p = new KgNodePair(this, new FieldOfStudyNode(fos));
                    nodes.Add(p);
                    nodes.Add(p.Reverse());
                }
            }
            return nodes;
        }

        /// <summary>
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。
        /// </summary>
        public override async Task<ICollection<KgNodePair>> GetAdjacentNodesAsync()
        {
            var nodes = loadedNodes;
            if (loadedNodes == null)
            {
                var er = await GlobalServices.ASClient
                    .EvaluateAsync(SearchExpressionBuilder.EntityIdEquals(Id), 2, 0);
                if (er.Entities.Count == 0)
                {
                    Logging.Trace(this, "找不到 Id 对应的实体。");
                    return EmptyNodePairs;
                }
                if (er.Entities.Count != 1)
                    Logging.Trace(this, "Id 请求返回的实体不唯一。");
                nodes = ParseEntity(er.Entities[0]);
                return nodes;
            }
            var backReferenceQueryTask = GlobalServices.ASClient.EvaluateAsync(
                SearchExpressionBuilder.ReferenceIdContains(Id), PAPER_MAX_CITED);
            // Id -> *Id (RId)
            var er1 = await backReferenceQueryTask;
            nodes.AddRange(er1.Entities.Select(et => new KgNodePair(new PaperNode(et), this)));
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

        public AuthorNode(Author author) : base(author.Id, author.Name)
        {
            //_LoadedAffiliation = new AffiliationNode(entity);
        }

        public AuthorNode(long id, string name) : base(id, name)
        {
            //_LoadedAffiliation = null;
        }

        /// <summary>
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。
        /// </summary>
        public override async Task<ICollection<KgNodePair>> GetAdjacentNodesAsync()
        {
            var er = await GlobalServices.ASClient.EvaluateAsync(
                SearchExpressionBuilder.AuthorIdEquals(Id), AUTHOR_MAX_PAPERS);
            if (er.Entities.Count == 0) return EmptyNodePairs;
            // AA.AuId -> Id
            // Id -> AA.AuId
            var nodes = new List<KgNodePair>();
            foreach (var et in er.Entities)
            {
                var p = new KgNodePair(this, new PaperNode(et));
                nodes.Add(p);
                nodes.Add(p.Reverse());
            }
            // AA.AuId -> AA.AfId
            // AA.AfId -> AA.AuId
            // 啊哈！我们应当注意到，一个作者可以分属不同的机构。
            // 另外，也许我们可以充分利用搜索结果，因为在搜索结果里面也包含了和
            //      这个作者协作的其他作者的机构信息。
            var affiliations = er.Entities
                .Select(et => et.Authors.First(au => au.Id == this.Id))
                .Distinct(AuthorAffiliationComparer.Default);
            foreach (var af in affiliations)
            {
                var p = new KgNodePair(this, new AffiliationNode(af));
                nodes.Add(p);
                nodes.Add(p.Reverse());
            }
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
        public const int AFFILIATION_MAX_PAPERS = 100000;

        public AffiliationNode(Author author) : base(author.AffiliationId, author.AffiliationName)
        {
        }

        public AffiliationNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。（很慢！）
        /// </summary>
        public override async Task<ICollection<KgNodePair>> GetAdjacentNodesAsync()
        {
            // TODO 引入一个 AttributeBuilder 或使用按位枚举代替 attribute 表达式
            // 找出 1091 人 + 1400 人大概需要 45 秒。好吧，估计是我的网速跪了。
            // 我们只需要找到作者。
            var er = await GlobalServices.ASClient.EvaluateAsync(
                SearchExpressionBuilder.AffiliationIdEquals(Id), AFFILIATION_MAX_PAPERS,
                "AA.AuId,AA.AuN,AA.AfId,AA.AfN");
            if (er.Entities.Count == 0) return EmptyNodePairs;
            var nodes = new List<KgNodePair>();
            // AA.AfId -> AA.AuId
            // AA.AuId -> AA.AfId
            // 注意到，一篇文章可能有多个作者属于当前 Id 对应的机构。
            // 所以使用 SelectMany + Where 而非 Select + First
            var authors = er.Entities
                .SelectMany(et => et.Authors.Where(au => au.AffiliationId == this.Id))
                .Distinct(AuthorIdComparer.Default);
            foreach (var au in authors)
            {
                var p = new KgNodePair(this, new AuthorNode(au));
                nodes.Add(p);
                nodes.Add(p.Reverse());
            }
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
        public const int JOURNAL_MAX_PAPERS = 2000000;

        public JournalNode(Journal entity) : base(entity.Id, entity.Name)
        {
        }

        public JournalNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。（慢！）
        /// </summary>
        public override async Task<ICollection<KgNodePair>> GetAdjacentNodesAsync()
        {
            var er = await GlobalServices.ASClient.EvaluateAsync(
                SearchExpressionBuilder.JournalIdEquals(Id), JOURNAL_MAX_PAPERS);
            if (er.Entities.Count == 0) return EmptyNodePairs;
            var nodes = new List<KgNodePair>();
            // J.JId -> Id
            // Id -> J.JId
            foreach (var et in er.Entities)
            {
                var p = new KgNodePair(this, new PaperNode(et));
                nodes.Add(p);
                nodes.Add(p.Reverse());
            }
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
        public const int CONFERENCE_MAX_PAPERS = 200000;

        public ConferenceNode(Conference entity) : base(entity.Id, entity.Name)
        {
        }

        public ConferenceNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。（慢！）
        /// </summary>
        public override async Task<ICollection<KgNodePair>> GetAdjacentNodesAsync()
        {
            var er = await GlobalServices.ASClient.EvaluateAsync(
                SearchExpressionBuilder.ConferenceIdEquals(Id), CONFERENCE_MAX_PAPERS);
            if (er.Entities.Count == 0) return EmptyNodePairs;
            var nodes = new List<KgNodePair>();
            // C.CId -> Id
            // Id -> C.CId
            foreach (var et in er.Entities)
            {
                var p = new KgNodePair(this, new PaperNode(et));
                nodes.Add(p);
                nodes.Add(p.Reverse());
            }
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
        /// 异步枚举由与此节点相连的邻节点。使用节点对来表示边的指向。（特别慢！）
        /// </summary>
        public override Task<ICollection<KgNodePair>> GetAdjacentNodesAsync()
        {
            // …… 以至于我已经不忍心写出来了 ||-_-
            return Task.FromResult<ICollection<KgNodePair>>(EmptyNodePairs);
        }
    }

    /// <summary>
    /// 一个有序节点对。
    /// </summary>
    public struct KgNodePair
    {
        public KgNodePair(KgNode node1, KgNode node2)
        {
            if (node1 == null) throw new ArgumentNullException(nameof(node1));
            if (node2 == null) throw new ArgumentNullException(nameof(node2));
            Node1 = node1;
            Node2 = node2;
        }

        /// <summary>
        /// 获取与当前节点对反向的节点对。
        /// </summary>
        public KgNodePair Reverse()
        {
            return new KgNodePair(Node2, Node1);
        }

        /// <summary>
        /// 节点1/源点。
        /// </summary>
        public KgNode Node1 { get; }

        /// <summary>
        /// 节点2/漏点。
        /// </summary>
        public KgNode Node2 { get; }
    }
}
