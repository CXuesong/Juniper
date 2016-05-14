using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.Academic.Contract
{
    public class EvaluationResult
    {
        [JsonProperty("expr")]
        public string Expression { get; set; }

        public IList<Entity> Entities { get; set; }

        public bool Aborted { get; set; }
    }
}
