using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.Academic.Contract
{
    public class CalcHistogramResult
    {
        [JsonProperty("expr")]
        public string Expression { get; set; }

        /// <summary>
        /// 符合条件的论文总数。
        /// 注意，有些符合条件的论文可能并没有所需要的属性的值，因此
        /// <see cref="CalcHistogramResult.EntityCount"/> 总是不超过 <see cref="Histogram.EntityCount"/> 。
        /// </summary>
        [JsonProperty("num_entities")]
        public int EntityCount { get; set; }

        public IList<Histogram> Histograms { get; set; }

        public bool Aborted { get; set; }
    }

    public class Histogram
    {
        public string Attribute { get; set; }

        /// <summary>
        /// 直方图中的项目总数。
        /// 注意 <see cref="Entries"/> 中项目的数量取决于请求时所采用的 count 属性。
        /// </summary>
        [JsonProperty("distinct_values")]
        public int EntryCount { get; set; }

        /// <summary>
        /// 符合条件、具有 <see cref="Attribute"/> 中对应属性的的论文总数。
        /// 注意，有些符合条件的论文可能并没有所需要的属性的值，因此
        /// <see cref="CalcHistogramResult.EntityCount"/> 总是不超过 <see cref="Histogram.EntityCount"/> 。
        /// </summary>
        [JsonProperty("total_count")]
        public int EntityCount { get; set; }

        /// <summary>
        /// 直方图中的项目列表。列表中项目的数量取决于请求时所采用的 count 属性。
        /// </summary>
        [JsonProperty("histogram")]
        public IList<HistogramEntry> Entries { get; set; }
    }

    public class HistogramEntry
    {
        public object Value { get; set; }

        [JsonProperty("logprob")]
        public float LogarithmProbability { get; set; }

        public int Count { get; set; }

        public override string ToString()
            => Value + ": " + Count;
    }
}
