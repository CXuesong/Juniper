﻿using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.Academic.Contract
{
    /// <summary>
    /// 表示 Evauation 返回的的一个实体。
    /// </summary>
    public class Entity
    {
        [JsonProperty("logprob")]
        public float LogarithmProbability { get; set; }

        [JsonProperty("Id")]
        public long Id { get; set; }

        [JsonProperty("RId")]
        public long[] ReferenceIds { get; set; }

        [JsonProperty("Ti")]
        public string Title { get; set; }

        [JsonProperty("Y")]
        public short Year { get; set; }

        [JsonProperty("AA")]
        public Author[] Authors { get; set; }

        [JsonProperty("J")]
        public Journal Journal { get; set; }

        [JsonProperty("C")]
        public Conference Conference { get; set; }

        /// <summary>
        /// 文献被引用的次数。
        /// </summary>
        [JsonProperty("CC")]
        public int CitationCount { get; set; }

        /// <summary>
        /// 研究领域。注意，此属性可能为 null 。
        /// </summary>
        [JsonProperty("F")]
        public FieldOfStudy[] FieldsOfStudy { get; set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        public override string ToString()
        {
            return $"[{Id}]{Title?.ToTitleCase()}";
        }
    }

    public class Author
    {
        [JsonProperty("AuN")]
        public string Name { get; set; }

        [JsonProperty("AuId")]
        public long Id { get; set; }

        [JsonProperty("AfN")]
        public string AffiliationName { get; set; }

        /// <summary>
        /// （可选）（作者有可能不属于任何组织。）
        /// </summary>
        [JsonProperty("AfId")]
        public long? AffiliationId { get; set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        public override string ToString()
        {
            return $"[{Id}]{Name?.ToTitleCase()} @ [{AffiliationId}]{AffiliationName?.ToTitleCase()}";
        }
    }

    public class Conference
    {
        [JsonProperty("CId")]
        public long Id { get; set; }

        [JsonProperty("CN")]
        public string Name { get; set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        public override string ToString()
        {
            return $"[{Id}]{Name?.ToTitleCase()}";
        }
    }

    public class Journal
    {
        [JsonProperty("JId")]
        public long Id { get; set; }

        [JsonProperty("JN")]
        public string Name { get; set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        public override string ToString()
        {
            return $"[{Id}]{Name?.ToTitleCase()}";
        }
    }

    public class FieldOfStudy
    {
        [JsonProperty("FId")]
        public long Id { get; set; }

        [JsonProperty("FN")]
        public string Name { get; set; }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        public override string ToString()
        {
            return $"[{Id}]{Name.ToTitleCase()}";
        }
    }

    internal class PaperIdComparer : IEqualityComparer<Entity>
    {
        public static readonly PaperIdComparer Default = new PaperIdComparer();

        public bool Equals(Entity x, Entity y)
        {
            return x?.Id == y?.Id;
        }

        public int GetHashCode(Entity obj)
        {
            return obj?.Id.GetHashCode() ?? 0;
        }
    }
}
