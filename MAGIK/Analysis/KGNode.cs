using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic.Contract;

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    /// <summary>
    /// 表示知识图上的一个节点。这是一个公共基类。
    /// </summary>
    public abstract class KgNode
    {
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
        public string Name { get; }

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
        public PaperNode(Entity entity) : base(entity.Id, entity.Title)
        {
        }

        public PaperNode(long id, string name) : base(id, name)
        {
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
    }
}
