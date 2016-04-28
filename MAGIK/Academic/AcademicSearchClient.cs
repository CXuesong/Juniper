using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic.Contract;
using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.Academic
{
    /// <summary>
    /// 提供了 Academic Search API 的 .NET 封装。
    /// </summary>
    /// <remarks>
    /// 由于 Academic Search API 还是一个比较新鲜的玩意儿，因此 M$ 还没有把 SDK 做出来。
    /// https://www.microsoft.com/cognitive-services/en-us/academic-knowledge-api/documentation/overview
    /// </remarks>
    public partial class AcademicSearchClient
    {
        /// <summary>
        /// The service host
        /// </summary>
        private const string ServiceHost = "https://api.projectoxford.ai/academic/v1.0";

        /// <summary>
        /// The analyze query
        /// </summary>
        private const string InterpretQuery = "interpret";

        /// <summary>
        /// The evaluate query
        /// </summary>
        private const string EvaluateQuery = "evaluate";

        /// <summary>
        /// The subscription key name
        /// </summary>
        private const string _subscriptionKeyName = "Ocp-Apim-Subscription-Key";

        /// <summary>
        /// The subscription key
        /// </summary>
        private string _subscriptionKey;

        /// <summary>
        /// 初始化一个新的 <see cref="VisionServiceClient"/> 实例。
        /// </summary>
        /// <param name="subscriptionKey">订阅密钥。</param>
        public AcademicSearchClient(string subscriptionKey)
        {
            _subscriptionKey = subscriptionKey;
        }

        /// <summary>
        /// 异步计算查询表达式，并进行学术文献的检索。
        /// </summary>
        public Task<EvaluationResult> EvaluateAsync(string expression, int count, int offset, string attributes)
        {
            return EvaluateAsync(expression, count, offset, null, attributes);
        }

        /// <summary>
        /// 异步计算查询表达式，并进行学术文献的检索。
        /// </summary>
        public async Task<EvaluationResult> EvaluateAsync(string expression, int count, int offset,
            string orderBy, string attributes)
        {
            string requestUrl =
                $"{ServiceHost}/evaluate?expr={expression}&model=latest&count={count}&offset={offset}&orderby={orderBy}&attributes={attributes}";
            var request = WebRequest.Create(requestUrl);
            request.Headers[_subscriptionKeyName] = _subscriptionKey;
            request.Method = "GET";
            return await SendAsync<EvaluationResult>(request);
        }

        private async Task<T> SendAsync<T>(WebRequest request)
        {
            try
            {
                var response = await request.GetResponseAsync();
                return ProcessAsyncResponse<T>((HttpWebResponse)response);
            }
            catch (Exception e)
            {
                HandleException(e);
                return default(T);
            }
        }

        private T ProcessAsyncResponse<T>(HttpWebResponse webResponse)
        {
            using (webResponse)
            {
                if (webResponse.StatusCode == HttpStatusCode.OK ||
                    webResponse.StatusCode == HttpStatusCode.Accepted ||
                    webResponse.StatusCode == HttpStatusCode.Created)
                {
                    if (webResponse.ContentLength != 0)
                    {
                        using (var stream = webResponse.GetResponseStream())
                        {
                            if (stream != null)
                            {
                                var message = string.Empty;
                                using (var reader = new StreamReader(stream))
                                {
                                    message = reader.ReadToEnd();
                                }
                                var settings = new JsonSerializerSettings
                                {
                                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                                    NullValueHandling = NullValueHandling.Ignore,
                                    ContractResolver = _defaultResolver
                                };
                                return JsonConvert.DeserializeObject<T>(message, settings);
                            }
                        }
                    }
                }
            }
            return default(T);
        }
    }
}
