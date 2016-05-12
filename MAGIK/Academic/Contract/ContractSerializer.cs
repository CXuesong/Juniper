using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Contests.Bop.Participants.Magik.Academic.Contract
{
    public static class ContractSerializer
    {
        public static T Deserialize<T>(TextReader reader)
            where T : class
        {
            using (var jreader = new JsonTextReader(reader))
            {
                var jobj = JObject.Load(jreader);
                return Deserialize<T>(jobj);
            }
        }

        public static T Deserialize<T>(JToken jobj)
            where T:class
        {
            if (jobj == null) return null;
            if (typeof (T) == typeof (Entity))
            {
                return new Entity
                {
                    LogarithmProbability = (float) jobj["logprob"],
                    Id = (long) jobj["Id"],
                    Title = (string) jobj["Ti"],
                    Year = (short) jobj["Y"],
                    Authors = jobj["AA"]?.Select(Deserialize<Author>).ToArray(),
                    Conference = Deserialize<Conference>(jobj["C"]),
                    Journal = Deserialize<Journal>(jobj["J"]),
                    FieldsOfStudy = jobj["F"]?.Select(Deserialize<FieldOfStudy>).ToArray(),
                    ReferenceIds = jobj["RId"]?.Select(t => (long) t).ToArray(),
                    CitationCount = (int) jobj["CC"],
                } as T;
            }
            if (typeof (T) == typeof (EvaluationResult))
            {
                return new EvaluationResult
                {
                    Expression = (string)jobj["expr"],
                    Entities = jobj["entities"]?.Select(Deserialize<Entity>).ToArray(),
                } as T;
            }
            if (typeof (T) == typeof (Author))
            {
                return new Author
                {
                    Id = (long)jobj["AuId"],
                    Name = (string)jobj["AuN"],
                    AffiliationId = (long?)jobj["AfId"],
                    AffiliationName = (string)jobj["AfN"],
                } as T;
            }
            if (typeof(T) == typeof(FieldOfStudy))
            {
                return new FieldOfStudy
                {
                    Id = (long)jobj["FId"],
                    Name = (string)jobj["FN"],
                } as T;
            }
            if (typeof(T) == typeof(Conference))
            {
                return new Conference
                {
                    Id = (long)jobj["CId"],
                    Name = (string)jobj["CN"],
                } as T;
            }
            if (typeof(T) == typeof(Journal))
            {
                return new Journal
                {
                    Id = (long)jobj["JId"],
                    Name = (string)jobj["JN"],
                } as T;
            }
            throw new NotSupportedException();
        }
    }
}
