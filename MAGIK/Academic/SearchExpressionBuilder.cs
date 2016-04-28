using System;
using System.Collections.Generic;
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

        public static string AuthorIdEquals(long id)
            => $"Composite(AA.AuId={id})";

        public static string AffiliationIdEquals(long id)
            => $"Composite(AA.AfId={id})";

        public static string ConferenceIdEquals(long id)
            => $"Composite(AA.AuId={id})";

        public static string JournalIdEquals(long id)
            => $"Composite(J.JId={id})";

        public static string FieldOfStudyIdEquals(long id)
            => $"Composite(F.FId={id})";

        public static string EntityOrAuthorIdEquals(long id)
            => $"Or(Id={id},Composite(AA.AuId={id}))";
    }
}
