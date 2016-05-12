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
        private int _MaxRetries = 2;

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

        ///// <summary>
        ///// 当因请求过于频繁而出现服务器错误时，重试前等待的时间。
        ///// </summary>
        //public TimeSpan ConcurrentFailureDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 最大重试次数。
        /// </summary>
        public int MaxRetries
        {
            get { return _MaxRetries; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _MaxRetries = value;
            }
        }

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
        public int ConcurrentPagingCount { get; set; } = 8;

        #endregion

        #region 统计信息

        private long queryCounter = 0;
        private long queryTimeMs = 0;

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
            Logger.AcademicSearch.Enter(this, $"{expression}; ({offset},{offset + count}]");
            var requestUrl =
                $"{ServiceHostUrl}/evaluate?expr={expression}&model=latest&count={count}&offset={offset}&orderby={orderBy}&attributes={attributes ?? EvaluationDefaultAttributes}{QuerySuffix}";
            // 我们要保证查询字符串不要太长。
            Debug.Assert(requestUrl.Length < SearchExpressionBuilder.MaxQueryLength - 10);
            var request = WebRequest.Create(requestUrl);
            InitializeRequest(request, "GET");
            var result = await SendAsync<EvaluationResult>(request);
            Logger.AcademicSearch.Exit(this, $"{expression}; {result?.Entities?.Count} Entities");
            return result;
        }

        /// <summary>
        /// 异步计算查询表达式，并进行学术文献的检索。启用自动分页。
        /// </summary>
        public Task<ICollection<T>> EvaluateAsync<T>(string expression, int count, Func<EvaluationResult, Task<T>> callback)
        {
            return EvaluateAsync<T>(expression, count, null, null, callback);
        }

        /// <summary>
        /// 异步计算查询表达式，并进行学术文献的检索。启用自动分页。
        /// </summary>
        public Task<ICollection<T>> EvaluateAsync<T>(string expression, int count, string attributes, Func<EvaluationResult, Task<T>> callback)
        {
            return EvaluateAsync<T>(expression, count, null, attributes, callback);
        }

        /// <summary>
        /// 异步计算查询表达式，并进行学术文献的检索。启用自动分页。
        /// </summary>
        public async Task<ICollection<T>> EvaluateAsync<T>(string expression, int count, string orderBy, string attributes, Func<EvaluationResult, Task<T>> callback)
        {
            if (count < PagingSize)
            {
                var er = await EvaluateAsync(expression, count, 0, orderBy, attributes);
                if (er.Entities.Count > 0) await callback(er);
            }
#if TRACE
            Logger.AcademicSearch.Enter(this, $"{expression}, [1,{count}] Paged");
#endif
            var noMoreResults = false;
            int results = 0;
            var callbackResults = new List<T>(Math.Min(count/PagingSize + 1, 32));
            EvaluationResult[] pages = null;
            var offset = 0;
            var currentConcurrentPagingCount = 1;
            while (true)
            {
// sessions
                Task<T[]> callbackTask = null;
                // 如果有回调，先执行回调。
                if (pages != null)
                {
                    callbackTask = Task.WhenAll(pages.Where(er => er.Entities.Count > 0).Select(callback));
                    // 已经不需要再下载页面了。
                    // 等待前一页的回调函数返回。
                    if (noMoreResults || offset >= count)
                    {
                        callbackResults.AddRange(await callbackTask);
                        break;
                    }
                }
                // 顺便下载页面。
                var sessionPages = Math.Min((count - offset)/PagingSize, currentConcurrentPagingCount);
                if (sessionPages == 0)
                {
                    // 此时应有 offset < count
                    // 多读一页应该不会有问题吧。
                    sessionPages = 1;
                }
                var offset1 = offset; // Access to modified closure.
                // 开始下载页面。
                pages = await Task.WhenAll(Enumerable.Range(0, sessionPages).Select(i =>
                    EvaluateAsync(expression, PagingSize, offset1 + i*PagingSize, orderBy, attributes)));
                foreach (var page in pages)
                {
                    // 当前 session 已经包含最后一页了。
                    // 做个标记，然后交给下一次循环来处理。
                    if (page.Entities.Count < PagingSize)
                        noMoreResults = true;
                    results += page.Entities.Count;
                }
                offset += sessionPages*PagingSize;
                // 如果还有结果，那么下一次试着多下载几页。
                if (currentConcurrentPagingCount < ConcurrentPagingCount)
                    currentConcurrentPagingCount = Math.Min(currentConcurrentPagingCount*2, ConcurrentPagingCount);
                // 等待前一页的回调函数返回。
                if (callbackTask != null)
                    callbackResults.AddRange(await callbackTask);
            }
#if TRACE
            Logger.AcademicSearch.Exit(this, $"{expression}; {results} Entities in total. {(noMoreResults ? "" : " Truncated.")}");
#endif
            return callbackResults;
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
            Logger.AcademicSearch.Enter(this, expression);
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
            Logger.AcademicSearch.Exit(this, mid.ToString());
            return mid;
        }

        /// <summary>
        /// 异步判断某表达式的查询结果数量是否多于 rhs 指定的数量。
        /// </summary>
        public async Task<bool> IsEvaluationCountGreaterThanAsync(string expression, int rhs)
        {
            Logger.AcademicSearch.Enter(this, expression);
            var er = await EvaluateAsync(expression, 1, rhs, "");
            var result = er.Entities.Count > 0;
            Logger.AcademicSearch.Exit(this, (result ? ">" : "<=") + rhs);
            return result;
        }

        /// <summary>
        /// 异步判断某表达式是否有查询结果。
        /// </summary>
        public Task<bool> EvaluationHasResultAsync(string expression)
        {
            return IsEvaluationCountGreaterThanAsync(expression, 0);
        }

        /// <summary>
        /// 客户端预热。
        /// </summary>
        public async Task WarmUp()
        {
            await EvaluationHasResultAsync("Id=0");
        }

        private void InitializeRequest(WebRequest request, string method)
        {
            request.Headers[_subscriptionKeyName] = _subscriptionKey;
            var hwr = (request as HttpWebRequest);
            if (hwr != null)
            {
                hwr.UserAgent = UserAgent;
                hwr.Referer = Referer;
            }
            request.Method = method;
            // The Timeout property affects only synchronous requests made with the GetResponse method.
            //request.Timeout = (int) Timeout.TotalMilliseconds;
        }

        /// <summary>
        /// 获取调用统计信息。
        /// </summary>
        public string DumpStatistics()
        {
            // 注意，此处使用 queryTime.TotalSeconds 是没有意义的。因为查询有可能并行。
            var message = $"{queryCounter}次查询。平均{queryTimeMs/queryCounter}ms/次。";
            return message;
        }

        /// <summary>
        /// 向日志输出调用统计信息。
        /// </summary>
        public void LogStatistics()
        {
            Logger.AcademicSearch.Info(this, DumpStatistics());
        }
    }
}
