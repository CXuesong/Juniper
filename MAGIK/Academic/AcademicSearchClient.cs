using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        // The analyze query
        private const string InterpretQuery = "interpret";

        // The evaluate query
        private const string EvaluateQuery = "evaluate";

        // The subscription key name
        private const string _subscriptionKeyName = "Ocp-Apim-Subscription-Key";
        private string _subscriptionKey;
        private int _PagingSize = 1000;

        //private static readonly TraceSource traceSource = new TraceSource("Magik.Academic");

        /// <summary>
        /// 初始化一个新的 <see cref="VisionServiceClient"/> 实例。
        /// </summary>
        /// <param name="subscriptionKey">订阅密钥。</param>
        public AcademicSearchClient(string subscriptionKey)
        {
            _subscriptionKey = subscriptionKey;
        }

#region 配置
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

        /// <summary>
        /// 请求超时。
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 最大重试次数。
        /// </summary>
        public int MaxRetries { get; set; } = 2;

        /// <summary>
        /// 提交请求时自动的分页大小。
        /// </summary>
        public int PagingSize
        {
            get { return _PagingSize; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
                _PagingSize = value;
            }
        }

        /// <summary>
        /// 并行提交分页请求时，并行的任务数量。
        /// </summary>
        public int ConcurrentPagingCount { get; set; } = 4;
#endregion

        #region 统计信息
#if TRACE
        private long queryCounter = 0;
        private long queryTimeMs = 0;
#endif
        #endregion

        /// <summary>
        /// 异步计算查询表达式，并进行学术文献的检索。
        /// </summary>
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
            Logging.Enter(this, $"{expression}, ({offset},{offset + count}]");
            var requestUrl =
                $"{ServiceHostUrl}/evaluate?expr={expression}&model=latest&count={count}&offset={offset}&orderby={orderBy}&attributes={attributes ?? EvaluationDefaultAttributes}{QuerySuffix}";
            var request = WebRequest.Create(requestUrl);
            InitializeHeader(request, "GET");
            var result = await SendAsync<EvaluationResult>(request);
            Logging.Exit(this, $"{result?.Entities?.Count} Entity");
            return result;
        }

        /// <summary>
        /// 异步计算查询表达式，并进行学术文献的检索。启用自动分页。
        /// </summary>
        public Task<EvaluationResult> EvaluateAsync(string expression, int count)
        {
            return EvaluateAsync(expression, count, null, null);
        }

        /// <summary>
        /// 异步计算查询表达式，并进行学术文献的检索。启用自动分页。
        /// </summary>
        public Task<EvaluationResult> EvaluateAsync(string expression, int count, string attributes)
        {
            return EvaluateAsync(expression, count, null, attributes);
        }

        /// <summary>
        /// 异步计算查询表达式，并进行学术文献的检索。启用自动分页。
        /// </summary>
        public async Task<EvaluationResult> EvaluateAsync(string expression, int count, string orderBy, string attributes)
        {
            if (count < PagingSize) return await EvaluateAsync(expression, count, 0, orderBy, attributes);
            Logging.Enter(this, $"{expression}, [1,{count}] Paged");
            var results = new List<Entity>();
            bool noMoreResults = false;
            for (var offset = 0; offset < count; )
            {
                var sessionPages = Math.Min((count - offset)/PagingSize, ConcurrentPagingCount);
                if (sessionPages == 0)
                {
                    // 此时应有 offset < count
                    // 多读一页应该不会有问题吧。
                    sessionPages = 1;
                }
                var result = await Task.WhenAll(Enumerable.Range(0, sessionPages).Select(i =>
                    EvaluateAsync(expression, PagingSize, offset + i*PagingSize, orderBy, attributes)));
                results.AddRange(result.SelectMany(er => er.Entities));
                if (result.Any(er => er.Entities.Count < PagingSize))
                {
                    // No more results.
                    noMoreResults = true;
                    break;
                }
                offset += sessionPages*PagingSize;
            }
            Logging.Exit(this, $"{results.Count} Entity {(noMoreResults ? "" : " Truncated")}");
            return new EvaluationResult
            {
                Expression = expression,
                Entities = results
            };
        }

        /// <summary>
        /// 异步计算查询表达式，估计结果的数量。允许 10% 的估计误差。
        /// </summary>
        public Task<int> EstimateEvaluationCountAsync(string expression, int maxCount)
        {
            return EstimateEvaluationCountAsync(expression, maxCount, 0.1f);
        }

        /// <summary>
        /// 异步计算查询表达式，估计结果的数量。
        /// </summary>
        public async Task<int> EstimateEvaluationCountAsync(string expression, int maxCount, float precision)
        {
            if (maxCount < 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
            if (precision <= 0) throw new ArgumentOutOfRangeException(nameof(precision));
            Logging.Enter(this, expression);
            int min = 0, max = maxCount;
            var mid = (max + min)/2;
            while ((max - min) / (float)mid > precision)
            {
                var er = await EvaluateAsync(expression, 1, mid, "");
                if (er.Entities.Count > 0)
                    min = mid;
                else
                    max = mid;
                mid = (max + min) / 2;
            }
            Logging.Exit(this, mid.ToString());
            return mid;
        }

        /// <summary>
        /// 异步判断某表达式的查询结果数量是否多于 rhs 指定的数量。
        /// </summary>
        public async Task<bool> IsEvaluationCountGreaterThanAsync(string expression, int rhs)
        {
            Logging.Enter(this, expression);
            var er = await EvaluateAsync(expression, 1, rhs, "");
            var result = er.Entities.Count > 0;
            Logging.Exit(this, (result ? ">" : "<=") + rhs);
            return result;
        }

        /// <summary>
        /// 异步判断某表达式是否有查询结果。
        /// </summary>
        public Task<bool> EvaluationHasResult(string expression)
        {
            return IsEvaluationCountGreaterThanAsync(expression, 0);
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

        /// <summary>
        /// 向 Trace 输出调用统计信息。
        /// </summary>
        public void TraceStatistics()
        {
#if TRACE
            // 注意，此处使用 queryTime.TotalSeconds 是没有意义的。因为查询有可能并行。
            Trace.WriteLine($"Academic Search：{queryCounter}次查询。平均{queryTimeMs/queryCounter}ms/次。");
#endif
        }
    }
}
