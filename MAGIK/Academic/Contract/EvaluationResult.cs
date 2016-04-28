using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.Academic.Contract
{
    public class EvaluationResult
    {
        [JsonProperty("expr")]
        public string Expression { get; set; }

        [JsonProperty("entities")]
        public IList<Entity> Entities { get; set; }
    }
}
