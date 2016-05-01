using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik;

namespace ServerLimitsTester
{
    /// <summary>
    /// 此应用程序对服务器的一些极限参数进行测试。
    /// </summary>
    static class Program
    {
        private const string DestUrl = "https://oxfordhk.azure-api.net/academic/v1.0/evaluate";

        private static HttpClient client = new HttpClient();

        static void Main(string[] args)
        {
            client.DefaultRequestHeaders.Add("User-Agent", "ServerLimitsTester/1.0 (Windows)");
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", AcademicSearchSubscriptionKey);
            client.DefaultRequestHeaders.Referrer = new Uri("https://studentclub.msra.cn/bop2016/", UriKind.Absolute);
            Task.WaitAll(MainAsync());
        }

        internal static string AcademicSearchSubscriptionKey
        {
            get
            {
                return (string) typeof (GlobalServices)
                    .GetProperty("AcademicSearchSubscriptionKey", BindingFlags.Static | BindingFlags.NonPublic)
                    .GetValue(null);
            }
        }

        private static async Task MainAsync()
        {
            //Console.WriteLine("Max query length: {0}",
            //    await MaxQueryLengthTestAsync(DestUrl,
            //        $"?subscription-key={AcademicSearchSubscriptionKey}&expr=", 4000));
            Console.WriteLine("Max concurrent connections: {0}",
                await MaxConcurrentConnectionsTestAsync(
                    DestUrl + $"?subscription-key={AcademicSearchSubscriptionKey}&expr=Composite(F.FN='biology')&offset=10000&count=100&attributes=Id,Ti,AA.AuId,AA.AuN,AA.AfId,AA.AfN,RId",
                    80));
        }

        /// <summary>
        /// 测试允许的最大查询字符串长度。
        /// </summary>
        private static async Task<int> MaxQueryLengthTestAsync(string url, string prefix, int maxAdditionalLength)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));
            if (prefix == null) throw new ArgumentNullException(nameof(prefix));
            if (maxAdditionalLength < 1) throw new ArgumentOutOfRangeException(nameof(maxAdditionalLength));
            int min = 1, max = maxAdditionalLength;
            var mid = (min + max)/2;
            while (max - min > 2)
            {
                var str = new string('A', mid);
                using (var response = await client.GetAsync(url + prefix + str))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        max = mid; // Too Long
                    else
                        min = mid;
                    mid = (min + max)/2;
                }
            }
            return prefix.Length + mid;
        }

        /// <summary>
        /// 测试允许的最大并发连接数量。
        /// </summary>
        private static async Task<int> MaxConcurrentConnectionsTestAsync(string url, int maxConnections)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));
            if (maxConnections < 1) throw new ArgumentOutOfRangeException(nameof(maxConnections));
            int min = 1, max = maxConnections;
            var mid = (min + max) / 2;
            while (max - min > 2)
            {
                await Task.Delay(2000);
                var tasks = Enumerable.Range(0, mid)
                    .Select(i => client.GetAsync(url));
                var responses = await Task.WhenAll(tasks);
                try
                {
                    if (responses.Any(r => r.StatusCode == HttpStatusCode.BadGateway))
                        max = mid; // Too Many
                    else
                        min = mid;
                }
                finally
                {
                    if (responses != null)
                        foreach (var r in responses) r.Dispose();
                }
                mid = (min + max) / 2;
            }
            return mid;
        }
    }
}
