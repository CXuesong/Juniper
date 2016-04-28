using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;
using Microsoft.Contests.Bop.Participants.Magik.Academic.Contract;

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    /// <summary>
    /// 表示知识图上的一个节点。这是一个公共基类。
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
        public AuthorNode(Author entity) : base(entity.Id, entity.Name)
        {
        }

        public AuthorNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由此节点指向的邻节点。
        /// </summary>
        public override async Task<ICollection<KgNode>> GetAdjacentOutNodesAsync()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 组织节点。
    /// </summary>
    public class AffiliationNode : KgNode
    {
        public AffiliationNode(Author entity) : base(entity.AffiliationId, entity.AffiliationName)
        {
        }

        public AffiliationNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由此节点指向的邻节点。
        /// </summary>
        public override Task<ICollection<KgNode>> GetAdjacentOutNodesAsync()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 期刊节点。
    /// </summary>
    public class JournalNode : KgNode
    {
        public JournalNode(Journal entity) : base(entity.Id, entity.Name)
        {
        }

        public JournalNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由此节点指向的邻节点。
        /// </summary>
        public override Task<ICollection<KgNode>> GetAdjacentOutNodesAsync()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 会议节点。
    /// </summary>
    public class ConferenceNode : KgNode
    {
        public ConferenceNode(Conference entity) : base(entity.Id, entity.Name)
        {
        }

        public ConferenceNode(long id, string name) : base(id, name)
        {
        }

        /// <summary>
        /// 异步枚举由此节点指向的邻节点。
        /// </summary>
        public override Task<ICollection<KgNode>> GetAdjacentOutNodesAsync()
        {
            throw new NotImplementedException();
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
        /// 异步枚举由此节点指向的邻节点。
        /// </summary>
        public override Task<ICollection<KgNode>> GetAdjacentOutNodesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
