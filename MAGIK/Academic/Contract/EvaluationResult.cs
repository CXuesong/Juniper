using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.Academic.Contract
{
    public class EvaluationResult
    {
        [JsonProperty("expr")]
        public string Expression { get; set; }

        [JsonProperty("entities")]
        public Entity[] Entities { get; set; }
    }
}
