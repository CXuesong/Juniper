using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.Academic.Contract
{
    public class Author
    {
        [JsonProperty("AuN")]
        public string Name { get; set; }

        [JsonProperty("AuId")]
        public long Id { get; set; }

        /// <summary>
        /// （可选）
        /// </summary>
        [JsonProperty("AfN")]
        public string AffiliationName { get; set; }

        [JsonProperty("AfId")]
        public long AffiliationId { get; set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        public override string ToString()
        {
            return $"[{Id}]{Name?.ToTitleCase()} @ [{AffiliationId}]{AffiliationName?.ToTitleCase()}";
        }
    }

    internal class AuthorIdComparer : IEqualityComparer<Author>
    {
        public static readonly AuthorIdComparer Default = new AuthorIdComparer();

        public bool Equals(Author x, Author y)
        {
            return x?.Id == y?.Id;
        }

        public int GetHashCode(Author obj)
        {
            return obj?.Id.GetHashCode() ?? 0;
        }
    }

    internal class AuthorAffiliationComparer : IEqualityComparer<Author>
    {
        public static readonly AuthorAffiliationComparer Default = new AuthorAffiliationComparer();

        public bool Equals(Author x, Author y)
        {
            return x?.AffiliationId == y?.AffiliationId;
        }

        public int GetHashCode(Author obj)
        {
            return obj?.AffiliationId.GetHashCode() ?? 0;
        }
    }
}
