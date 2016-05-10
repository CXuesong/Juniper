using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    public abstract class AnalyzerBase
    {
        protected AnalyzerBase(AcademicSearchClient searchClient)
        {
            if (searchClient == null) throw new ArgumentNullException(nameof(searchClient));
            SearchClient = searchClient;
        }

        public AcademicSearchClient SearchClient { get; }

        /// <summary>
        /// 根据给定的一对标识符所对应的对象，异步检索二者之间可能的路径。
        /// </summary>
        /// <param name="id1">源点对象标识符。此标识符可以是论文（Id）或作者（AA.AuId）。</param>
        /// <param name="id2">漏点对象标识符。此标识符可以是论文（Id）或作者（AA.AuId）。</param>
        /// <returns>
        /// 一个 Task 。其运行结果是一个集合（ICollection），集合中的每一个项目是一个数组，代表一条路径。
        /// 数组按照 id1 → id2 的顺序返回路径途经的所有节点。
        /// 合法的节点类型包括
        /// 论文（Id）, 研究领域（F.Fid）, 期刊（J.JId）, 会议（C.CId）, 作者（AA.AuId）, 组织（AA.AfId）。
        /// </returns>
        public abstract Task<KgNode[][]> FindPathsAsync(long id1, long id2);
    }
}
