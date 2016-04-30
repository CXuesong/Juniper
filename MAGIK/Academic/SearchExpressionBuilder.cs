using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Contests.Bop.Participants.Magik.Academic
{
    /// <summary>
    /// 提供一系列静态方法，用以生成合法的 Evaluation 表达式。
    /// </summary>
    /// <remarks>
    /// 关于表达式语法，请参阅：
    /// https://www.microsoft.com/cognitive-services/en-us/academic-knowledge-api/documentation/queryexpressionsyntax
    /// </remarks>
    public static class SearchExpressionBuilder
    {
        public static string EntityIdEquals(long id)
            => $"Id={id}";

        public const int MaxChainedIdCount = 100;

        /// <summary>
        /// 要求实体 Id 是给定集合中的一个 Id 。ids 数量不应当超过 100 。
        /// </summary>
        public static string EntityIdIn(IEnumerable<long> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            Debug.Assert(ids.Count() <= MaxChainedIdCount);
            string expr = null;
            foreach (var id in ids)
            {
                if (expr == null) expr = EntityIdEquals(id);
                else expr = Or(expr, EntityIdEquals(id));
            }
            return expr;
        }

        public static string ReferenceIdContains(long id)
            => $"RId={id}";

        public static string AuthorIdContains(long id)
            => $"Composite(AA.AuId={id})";

        public static string AffiliationIdContains(long id)
            => $"Composite(AA.AfId={id})";

        /// <summary>
        /// 限定作者及其所在的机构。（而不是作者或机构。）
        /// </summary>
        public static string AuthorIdWithAffiliationIdContains(long authorId, long affiliationId)
            => $"Composite(And(AA.AuId={authorId},AA.AfId={affiliationId}))";

        public static string ConferenceIdEquals(long id)
            => $"Composite(AA.AuId={id})";

        public static string JournalIdEquals(long id)
            => $"Composite(J.JId={id})";

        public static string FieldOfStudyIdEquals(long id)
            => $"Composite(F.FId={id})";

        public static string EntityOrAuthorIdEquals(long id)
            => $"Or(Id={id},Composite(AA.AuId={id}))";

        public static string And(string expr1, string expr2)
            => $"And({expr1},{expr2})";

        public static string Or(string expr1, string expr2)
            => $"Or({expr1},{expr2})";
    }
}
