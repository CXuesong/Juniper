using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Results;
using Newtonsoft.Json;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer.Controllers
{
    /// <summary>
    /// 为 MAGIK 服务提供控制器逻辑。
    /// </summary>
    [JsonArgumentExceptionFilter]
    [JsonExceptionsFilter]
    public class MagikController : ApiController
    {
        /// <summary>
        /// 主要 API 入口。
        /// 同时，对于无参数的 API 调用，也会经过此处。
        /// </summary>
        [Route("magik/v1/paths")]
        public Task<IHttpActionResult> Get(string expr = null, string action = null)
        {
            if (action != null)
            {
                switch (action)
                {
                    case "purge":
                        Utility.PurgeAnalyzer();
                        break;
                    default:
                        throw new ArgumentException("无效的操作。", nameof(action));
                }
            }
            if (expr != null)
            {
                long[] idPair;
                try
                {
                    idPair = JsonConvert.DeserializeObject<long[]>(expr);
                }
                catch (Exception ex) when (ex is JsonSerializationException
                                           || ex is JsonReaderException)
                {
                    throw new ArgumentException(ex.Message, nameof(expr));
                }
                if (idPair.Length != 2)
                    throw new ArgumentException("无效的节点对。节点对有且仅有两个元素。", nameof(expr));
                return Get(idPair[0], idPair[1]);
            }
            return Task.FromResult((IHttpActionResult)Ok());
        }

        /// <summary>
        /// 主要 API 入口。（BOP）
        /// </summary>
        /// <remarks>
        /// For each test case, Judgement System will send a GET request to
        /// http://{your_prefix}.chinacloudapp.cn/{your_path}?id1={id1}&id2={id2}
        /// then calculate your score by accuracy and running time.
        /// </remarks>
        [Route("magik/v1/paths")]
        public async Task<IHttpActionResult> Get(long id1, long id2)
        {
            var sw = Stopwatch.StartNew();
            var analyzer = Utility.GetAnalyzer();
            try
            {
                var paths = await analyzer.FindPathsAsync(id1, id2);
                // 返回只要 Id 就可以了。
                // 由于结构比较简单，所以可以强行 json 。
                var resultBuilder = new StringBuilder("[");
                var isFirst = true;
                foreach (var path in paths)
                {
                    if (isFirst)
                        isFirst = false;
                    else
                        resultBuilder.Append(",\n");
                    resultBuilder.Append("[");
                    for (int j = 0; j < path.Length; j++)
                    {
                        if (j > 0) resultBuilder.Append(",");
                        resultBuilder.Append(path[j].Id);
                    }
                    resultBuilder.Append("]");
                }
                resultBuilder.Append("]");
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(resultBuilder.ToString(), null, Utility.JsonMediaType)
                };
                return new ResponseMessageResult(resp);
            }
            finally
            {
                analyzer.LogStatistics();
                analyzer.SearchClient.LogStatistics();
                TimerLogger.TraceTimer("MagikController", sw);
            }
        }
    }
}
