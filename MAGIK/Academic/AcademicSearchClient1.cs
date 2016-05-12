// Some portion of the code is derived from Microsoft.ProjectOxford.Vision
//
// Copyright (c) Microsoft Corporation
// All rights reserved.
//
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic.Contract;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.Contests.Bop.Participants.Magik.Academic
{
    partial class AcademicSearchClient
    {
        // 我们假定这两个类型是线程安全的。
        // http://stackoverflow.com/questions/36186276/is-the-json-net-jsonserializer-threadsafe
        private static readonly JsonSerializerSettings jsonSerializerSettings =
            new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        private static readonly JsonSerializer jsonSerializer =
            JsonSerializer.CreateDefault(jsonSerializerSettings);

        #region the json client

        private async Task<T> SendAsync<T>(WebRequest request)
            where T : class
        {
            Interlocked.Increment(ref queryCounter);
            var sw = new Stopwatch();
            try
            {
                sw.Restart();
                var retries = 0;
                RETRY:
                var responseTask = request.GetResponseAsync();
                using (var timeoutCancellation = new CancellationTokenSource(Timeout))
                {
                    var timeoutTask = Task.Delay(Timeout, timeoutCancellation.Token);
                    if (await Task.WhenAny(responseTask, timeoutTask) == timeoutTask)
                    {
                        if (!responseTask.IsCompleted)
                        {
                            //To time out asynchronous requests, use the Abort method.
                            try
                            {
                                request.Abort();
                            }
                            catch (WebException ex)
                            {
                                Debug.Assert(ex.Status == WebExceptionStatus.RequestCanceled);
                            }
                            retries++;
                            Logger.AcademicSearch.Warn(this, EventId.RequestTimeout,
                                "Timeout(x{0}): {1}", retries, request.RequestUri);
                            //timeoutTask.Dispose();
                            if (retries > MaxRetries) throw new TimeoutException();
                            // Before retry, copy the request.
                            var newRequest = WebRequest.Create(request.RequestUri);
                            newRequest.Method = request.Method;
                            //newRequest.Timeout = request.Timeout; of no use.
                            var hwr = request as HttpWebRequest;
                            var nhwr = newRequest as HttpWebRequest;
                            if (hwr != null)
                            {
                                Debug.Assert(nhwr != null);
                                nhwr.UserAgent = hwr.UserAgent;
                                nhwr.Referer = hwr.Referer;
                            }
                            request = newRequest;
                            goto RETRY;
                        }
                    }
                    // 请求正常完成。
                    timeoutCancellation.Cancel();
                }
                // 这里使用 await 而不是 responseTask.Result 其实也是为了展开异常。
                // 不然扔出来的很可能是 AggregateException 。
                var result = (HttpWebResponse) await responseTask;
                Logger.AcademicSearch.Trace(this, EventId.RequestOk, "{0}[{1}]({2}ms): {3}",
                    (int) result.StatusCode, result.StatusDescription, sw.ElapsedMilliseconds, request.RequestUri);
                return ProcessAsyncResponseAsync<T>(result);
            }
            catch (Exception e)
            {
                Logger.AcademicSearch.Error(this, "{0}({1}ms): {2}", Utility.ExpandErrorMessage(e),
                    sw.ElapsedMilliseconds, request.RequestUri);
                HandleException(e);
                return default(T);
            }
            finally
            {
                Interlocked.Add(ref queryTimeMs, sw.ElapsedMilliseconds);
            }
        }

        private T ProcessAsyncResponseAsync<T>(HttpWebResponse webResponse)
            where T : class
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
                                using (var reader = new StreamReader(stream))
                                using (var jreader = new JsonTextReader(reader))
                                {
                                    //return ContractSerializer.Deserialize<T>(reader);
                                    return jsonSerializer.Deserialize<T>(jreader);
                                }
                            }
                        }
                    }
                }
            }
            return default(T);
        }

        /// <summary>
        /// Process the exception happened on rest call.
        /// </summary>
        /// <param name="exception">Exception object.</param>
        private void HandleException(Exception exception)
        {
            var webException = exception as WebException;
            if (webException?.Response != null
                && webException.Response.ContentType.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Stream stream = null;
                try
                {
                    stream = webException.Response.GetResponseStream();
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var errorCollection = (ClientErrorContainer)
                                jsonSerializer.Deserialize(reader, typeof(ClientErrorContainer));
                            if (errorCollection != null)
                                throw new ClientException(errorCollection.Error);
                        }
                    }
                }
                finally
                {
                    stream?.Dispose();
                }
            }
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
        #endregion
    }
}
