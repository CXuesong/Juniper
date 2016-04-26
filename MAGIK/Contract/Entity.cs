using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.Contract
{
    /// <summary>
    /// 表示 Evauation 返回的的一个实体。
    /// </summary>
    public class Entity
    {
        [JsonProperty("logprob")]
        public double Probability { get; set; }

        [JsonProperty("Id")]
        public long Id { get; set; }

        [JsonProperty("Ti")]
        public string Title { get; set; }

        [JsonProperty("Y")]
        public short Year { get; set; }

        [JsonProperty("AA")]
        public Author[] Authors { get; set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        public override string ToString()
        {
            return $"[{Id}]{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Title)}";
        }
    }
}
