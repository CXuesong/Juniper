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
        /// <summary>
        /// 最长的查询条件（整个表达式）的长度。
        /// </summary>
        public const int MaxQueryLength = 2048;

        /// <summary>
        /// 最大允许的 And(Id=2026561929,) 并联数量。
        /// </summary>
        public const int MaxChainedIdCount = 85;

        /// <summary>
        /// 最大允许的 And(Composite(AA.AuId=2026561929),) 并联数量。
        /// </summary>
        public const int MaxChainedAuIdCount = 50;

        /// <summary>
        /// 最大允许的 And(Composite(AA.AfId=2026561929),) 并联数量。
        /// </summary>
        public const int MaxChainedAfIdCount = MaxChainedAuIdCount;

        /// <summary>
        /// 最大允许的 And(FId=122026561929,) 并联数量。
        /// </summary>
        public const int MaxChainedFIdCount = 84;

        public static string EntityIdEquals(long id)
            => $"Id={id}";

        public static string EntityTitleEquals(string title)
            => $"Ti={title}";

        /// <summary>
        /// 要求实体 Id 是给定集合中的一个 Id 。ids 数量不应当超过 85 。
        /// </summary>
        public static string EntityIdIn(IEnumerable<long> ids)
            => ChainExpressions(ids.Select(EntityIdEquals), "Or");

        public static string ReferenceIdContains(long id)
            => $"RId={id}";

        public static string AuthorIdContains(long id)
            => $"Composite(AA.AuId={id})";

        /// <summary>
        /// 要求实体存在一个作者的 AuId 是给定集合中的一个 Id 。ids 数量不应当超过 50 。
        /// </summary>
        public static string AuthorIdIn(IEnumerable<long> ids)
            => ChainExpressions(ids.Select(id => AuthorIdContains(id)), "Or");

        public static string AffiliationIdContains(long id)
            => $"Composite(AA.AfId={id})";

        /// <summary>
        /// 限定作者及其所在的机构。（而不是作者或机构。）
        /// </summary>
        public static string AuthorIdContains(long authorId, long affiliationId)
            => $"Composite(And(AA.AuId={authorId},AA.AfId={affiliationId}))";

        /// <summary>
        /// 限定作者及其可能所在的机构列表。（而不是作者或机构。）
        /// （机构与机构之间是 或 的关系。）
        /// </summary>
        public static string AuthorIdContains(long authorId, IEnumerable<long> affiliationIds)
        {
            var afexpr = ChainExpressions(affiliationIds.Select(id => "AA.AfId=" + id), "Or");
            if (string.IsNullOrEmpty(afexpr)) throw new ArgumentException("机构列表为空。", nameof(affiliationIds));
            return $"Composite(And(AA.AuId={authorId},{afexpr}))";
        }

        public static string ConferenceIdEquals(long id)
            => $"Composite(C.CId={id})";

        public static string JournalIdEquals(long id)
            => $"Composite(J.JId={id})";

        public static string FieldOfStudyIdContains(long id)
            => $"Composite(F.FId={id})";

        public static string FieldOfStudyIdIn(IEnumerable<long> ids)
        {
            var afexpr = ChainExpressions(ids.Select(id => "F.FId=" + id), "Or");
            if (string.IsNullOrEmpty(afexpr)) throw new ArgumentException("研究领域列表为空。", nameof(ids));
            return $"Composite({afexpr})";
        }

        public static string EntityOrAuthorIdEquals(long id)
            => $"Or(Id={id},Composite(AA.AuId={id}))";

        public static string And(string expr1, string expr2)
            => $"And({expr1},{expr2})";

        public static string Or(string expr1, string expr2)
            => $"Or({expr1},{expr2})";

        private static string ChainExpressions(IEnumerable<string> subExpressions, string relationKeyword)
        {
            Debug.Assert(subExpressions != null);
            var sb = new StringBuilder();
            var lastStartingIndex = -1;
            var counter = 0;
            string lastExpr = null;
            // AND(expr1, AND(expr2, expr3))
            // AND(expr1, AND(expr2, AND(expr3, 
            foreach (var expr in subExpressions)
            {
                lastExpr = expr;
                lastStartingIndex = sb.Length;
                sb.Append(relationKeyword);
                sb.Append('(');
                sb.Append(expr);
                sb.Append(',');
                counter++;
            }
            if (counter == 0) return string.Empty;
            if (counter == 1) return lastExpr;
            // Fallback
            // AND(expr1, AND(expr2, AND(expr3, 
            //                       ^ LastStartingIndex
            sb.Remove(lastStartingIndex - 1, sb.Length - lastStartingIndex);
            sb.Append(lastExpr);
            sb.Append(')', counter - 1);
            return sb.ToString();
        }
    }
}
