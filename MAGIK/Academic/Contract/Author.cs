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
            return $"[{Id}]{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Name)}";
        }
    }
}
