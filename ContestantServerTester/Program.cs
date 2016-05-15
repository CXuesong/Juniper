//#define LEARNING_MODE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ContestantServerTester.Contract;
using ContestantServerTester.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace ContestantServerTester
{
    class Program
    {
        static string Input(string prompt, string defaultValue)
        {
            Console.WriteLine(prompt);
            if (defaultValue != null) Console.Write("[{0}]", defaultValue);
            Console.Write(" >");
            var l = Console.ReadLine();
            if (l == "") return defaultValue ?? "";
            return l;
        }

        static void Main(string[] args)
        {
            BEGIN:
            var endPointUri = Input("键入终结点 URI。", Settings.Default.LastEndpoint);
            var tc = LoadTests("Tests.json");
            Console.WriteLine();
#if LEARNING_MODE
            BuildTestsAsync(endPointUri, tc.Tests).Wait();
            SaveTests("Tests.json", tc);
#else
            PerformTestsAsync(endPointUri, tc.Tests).Wait();
#endif
            Settings.Default.LastEndpoint = endPointUri;
            Settings.Default.Save();
            goto BEGIN;
        }

        static TestsContainer LoadTests(string path)
        {
            return JsonConvert.DeserializeObject<TestsContainer>(File.ReadAllText(path),
                new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
        }

        static void SaveTests(string path, TestsContainer container)
        {
            File.WriteAllText(path,
                JsonConvert.SerializeObject(container, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                }));
        }

        static async Task BuildTestsAsync(string endPoint, ICollection<Test> tests)
        {
            var queryFormat = endPoint + "?id2={1}&id1={0}";
            var client = new HttpClient();
            foreach (var t in tests)
            {
                if (t.Paths == null)
                {
                    Console.WriteLine("{0} - {1}", t.Challenge[0], t.Challenge[1]);
                    try
                    {
                        var result =
                            await client.GetStringAsync(string.Format(queryFormat, t.Challenge[0], t.Challenge[1]));
                        var jresult = JArray.Parse(result);
                        t.Paths = jresult.Select(path => path.Select(node => (long) node).ToArray()).ToArray();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        static async Task PerformTestsAsync(string endPoint, ICollection<Test> tests)
        {
            var counter = 0;
            var client = new HttpClient();
            var queryFormat = endPoint + "?id2={1}&id1={0}";
            var performance = new List<PerformanceEntry>();
            foreach (var t in tests)
            {
                counter++;
                Console.WriteLine("测试 {0}/{1}： {2} - {3}", counter, tests.Count, t.Challenge[0], t.Challenge[1]);
                var sw = Stopwatch.StartNew();
                var acceptedRatio = 0.0;
                try
                {
                    var downloadTask = client.GetStringAsync(string.Format(queryFormat, t.Challenge[0], t.Challenge[1]));
                    var acceptedResults = new HashSet<long[]>(t.Paths, ArrayEqualityComparer<long>.Default);
                    var result = await downloadTask;
                    sw.Stop();
                    var jresult = JArray.Parse(result);
                    var results = jresult.Select(path => path.Select(node => (long) node).ToArray()).ToArray();
                    acceptedResults.IntersectWith(results);
                    Console.WriteLine("路径：EXP:{0}, ACC:{1}, TOT:{2}", t.Paths.Count, acceptedResults.Count, results.Length);
                    acceptedRatio = (double) acceptedResults.Count/Math.Max(t.Paths.Count, results.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    Console.WriteLine("用时： {0} ms", sw.ElapsedMilliseconds);
                    var timeRatio = (double) sw.ElapsedMilliseconds/1000/300;
                    var score = 10.0/99*(Math.Pow(100, acceptedRatio) - 1)*(1 - timeRatio);
                    Console.WriteLine("得分： {0}", score);
                    performance.Add(new PerformanceEntry(sw.ElapsedMilliseconds, score, acceptedRatio));
                }
                Console.WriteLine();
            }
            Console.WriteLine("小结");
            Console.WriteLine("用时\t正确率\t得分");
            foreach (var p in performance)
                Console.WriteLine("{0}\t{1:p1}\t{2}", p.ElapsedMilliseconds, p.AcceptedRatio, p.Score);
            Console.WriteLine("----------------------");
            Console.WriteLine("{0}\t{1}", performance.Sum(p => p.ElapsedMilliseconds), performance.Sum(p => p.Score));
            Console.WriteLine();
        }
    }

    class PerformanceEntry
    {
        public PerformanceEntry(long elapsedMilliseconds, double score, double acceptedRatio)
        {
            ElapsedMilliseconds = elapsedMilliseconds;
            Score = score;
            AcceptedRatio = acceptedRatio;
        }

        public long ElapsedMilliseconds { get; }

        public double Score { get; }

        public double AcceptedRatio { get; }
    }
}
