using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Contests.Bop.Participants.Magik
{
    /// <summary>
    /// 全局假设（脑洞）。
    /// </summary>
    public static class Assumptions
    {
        /// <summary>
        /// 一个作者最多能发表的论文数量。
        /// </summary>
        public const int AuthorMaxPapers = 10000;

        /// <summary>
        /// 一篇论文最多能被引用的次数。
        /// </summary>
        public const int PaperMaxCitations = 100000;
    }
}
