using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.Contract
{
    public class EvaluationResult
    {
        [JsonProperty("expr")]
        public string Expression { get; set; }

        [JsonProperty("entities")]
        public Entity[] Entities { get; set; }
    }
}
