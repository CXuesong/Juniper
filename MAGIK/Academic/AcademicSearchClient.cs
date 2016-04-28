using System;
using System.Diagnostics;
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

        private static readonly TraceSource traceSource =
            new TraceSource("Microsoft.Contests.Bop.Participants.Magik.Academic");

        /// <summary>
        /// 初始化一个新的 <see cref="VisionServiceClient"/> 实例。
        /// </summary>
        /// <param name="subscriptionKey">订阅密钥。</param>
        public AcademicSearchClient(string subscriptionKey)
        {
            _subscriptionKey = subscriptionKey;
        }

        /// <summary>
        /// Academic Search API 服务器主机 Url 。
        /// 此属性的默认值为 https://api.projectoxford.ai/academic/v1.0 。
        /// </summary>
        public string ServiceHostUrl { get; set; } = "https://api.projectoxford.ai/academic/v1.0";

        /// <summary>
        /// 向 Academic Search API 服务器主机提交查询请求时，需要在查询 Url 后附加的内容。
        /// </summary>
        public string QuerySuffix { get; set; }

        /// <summary>
        /// 使用 Evaluate 方法时，如果没有指定 attributes ，则会使用此属性所指定的请求特性集合。
        /// </summary>
        public string EvaluationDefaultAttributes { get; set; }

        /// <summary>
        /// 提交请求时，向服务器发送的 User Agent 。
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// 提交请求时，向服务器发送的 Referer 。
        /// </summary>
        public string Referer { get; set; }


        public Task<EvaluationResult> EvaluateAsync(string expression, int count, int offset)
        {
            return EvaluateAsync(expression, count, offset, null, null);
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
            traceSource.TraceEvent(TraceEventType.Verbose, 0, "Evaluate:{0}", expression);
            var requestUrl =
                $"{ServiceHostUrl}/evaluate?expr={expression}&model=latest&count={count}&offset={offset}&orderby={orderBy}&attributes={attributes ?? EvaluationDefaultAttributes}{QuerySuffix}";
            var request = WebRequest.Create(requestUrl);
            InitializeHeader(request, "GET");
            var result = await SendAsync<EvaluationResult>(request);
            traceSource.TraceEvent(TraceEventType.Verbose, 0, "Evaluate:{0} -> {1} Entities", expression,
                result?.Entities?.Length);
            return result;
        }

        private void InitializeHeader(WebRequest request, string method)
        {
            request.Headers[_subscriptionKeyName] = _subscriptionKey;
            var hwr = (request as HttpWebRequest);
            if (hwr != null)
            {
                hwr.UserAgent = UserAgent;
                hwr.Referer = Referer;
            }
            request.Method = method;
        }
    }
}
