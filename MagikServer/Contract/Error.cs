using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer.Contract
{
    /// <summary>
    /// 用于包装错误信息。
    /// </summary>
    public class Error
    {
        public Error(string type, string message)
        {
            Type = type;
            Message = message;
        }

        [JsonProperty("type")]
        public string Type { get; }

        [JsonProperty("message")]
        public string Message { get; }
    }
}
