#define CACHE_TEST_ENABLED
//#define SKIP_HUGE_TEST_CASES

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik;
using Microsoft.Contests.Bop.Participants.Magik.Academic;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class AnalyzerTests
    {
        private IReadOnlyCollection<KgNode[]> FindPaths(long id1, long id2, bool assertPathExists)
        {
            var asc = GlobalServices.CreateASClient();
            var a = new Analyzer(asc);
            var paths = TestUtility.AwaitSync(a.FindPathsAsync(id1, id2));
            Trace.WriteLine(a.DumpStatistics());
            Trace.WriteLine(asc.DumpStatistics());
            //a.TraceGraph();
            var hop1Count = paths.Count(p => p.Length == 2);
            var hop2Count = paths.Count(p => p.Length == 3);
            var hop3Count = paths.Count(p => p.Length == 4);
            Trace.WriteLine($"路径 {id1} -> {id2} [{hop1Count} + {hop2Count} + {hop3Count} = {paths.Count}]");
            Trace.Indent();
            foreach (var p in paths)
            {
                Trace.WriteLine(string.Join("\n\t->", (IEnumerable<KgNode>) p));
            }
            Trace.Unindent();
            if (assertPathExists) Assert.AreNotEqual(0, paths.Count);
            foreach (var p in paths)
            {
                Assert.IsTrue(p.Length >= 2); // >= 1-hop
                Assert.IsTrue(p.Length <= 4); // <= 3-hop
                Assert.AreEqual(id1, p[0].Id);
                Assert.AreEqual(id2, p[p.Length - 1].Id);
            }
            TraceAsJson(paths);
#if CACHE_TEST_ENABLED
            var sw = Stopwatch.StartNew();
            var paths2 = TestUtility.AwaitSync(a.FindPathsAsync(id1, id2));
            sw.Stop();
            Trace.WriteLine("Cached: " + sw.Elapsed);
            TraceAsJson(paths2);
            Assert.AreEqual(paths.Count, paths2.Count);
            Assert.IsTrue(paths.SequenceEqual(paths2, ArrayEqualityComparer<KgNode>.Default));
            Trace.WriteLine(a.DumpStatistics());
            Trace.WriteLine(asc.DumpStatistics());
#endif
            return paths;
        }

        private void TraceAsJson(IReadOnlyCollection<KgNode[]> paths)
        {
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
            Trace.WriteLine(resultBuilder.ToString());
        }

        private void AssertPathsCount(IReadOnlyCollection<KgNode[]> paths, int countAtLeast)
        {
            if (paths.Count < countAtLeast)
                Assert.Fail("路径数量不足。期望：{0}，实际：{1}。", countAtLeast, paths.Count);
            else if (paths.Count > countAtLeast)
                Assert.Inconclusive("路径数量超过期望值。期望：{0}，实际：{1}。", countAtLeast, paths.Count);
        }

        private void AssertPathExists(IReadOnlyCollection<KgNode[]> paths, params long[] idPath)
        {
            Assert.IsTrue(paths.Any(p => p.Select(n => n.Id).SequenceEqual(idPath)),
                $"在路径集合[{paths.Count}]中找不到路径 {string.Join(" -> ", idPath)} 。");
        }

        /// <summary>
        /// 在单元测试函数体内部声明此测试是一个大型测试用例。
        /// </summary>
        [Conditional("SKIP_HUGE_TEST_CASES")]
        private void DeclareHugeTestCase()
        {
            Assert.Inconclusive("已跳过对大型测试用例的测试。");
        }

        /// <summary>
        /// 1-Hop Id - AuId
        /// </summary>
        [TestMethod]
        public void AnalyzerTestPreliminary1()
        {
            // 5-10: 260 -> 261
            // 2157025439: what do people ask their social networks and why a survey study of status message q a behavior
            // 1982462162: Jaime Teevan
            var paths = FindPaths(2157025439, 1982462162, true);
            //TraceAsJson(paths);
            // 1/2 hop + 3-hop
            AssertPathsCount(paths, 2 + 259);
            AssertPathExists(paths, 2157025439, 1982462162);
        }

        /// <summary>
        /// 2-Hop Id - Id
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethodEasy1()
        {
            // 5-10: 244 -> 255
            // 2157025439: what do people ask their social networks and why a survey study of status message q a behavior
            // 2061503185: implicit feedback for inferring user preference a bibliography
            var paths = FindPaths(2157025439, 2061503185, true);
            // 1/2 hop + 3-hop
            AssertPathsCount(paths, 3 + 252);
            AssertPathExists(paths, 2157025439, 2122841972, 2061503185);
            // 1982462162: Jaime Teevan
            AssertPathExists(paths, 2157025439, 1982462162, 2061503185);
            // 41008148: Computer Science
            AssertPathExists(paths, 2157025439, 41008148, 2061503185);
        }

        /// <summary>
        /// 2-Hop AuId - AuId
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethodEasy2()
        {
            // 5-10: 147 -> 181
            // 1982462162: Jaime Teevan
            // 676500258: Susan T Dumais
            var paths = FindPaths(1982462162, 676500258, true);
            AssertPathsCount(paths, 47 + 134);
            // 2057034832: Understanding Temporal Query Dynamics
            AssertPathExists(paths, 1982462162, 2057034832, 676500258);
            // 1290206253: Microsoft
            AssertPathExists(paths, 1982462162, 1290206253, 676500258);
        }

        /// <summary>
        /// 2-Hop Id - Id （被多次引用）
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethodMedium1()
        {
            // 5-10: 1528 -> 1431
            // 2128366083: resolution limit in community detection
            // 2112090702: collective dynamics of small world network
            var paths = FindPaths(2128366083, 2112090702, true);
            AssertPathsCount(paths, 4 + 1431);
            // 2164928285: uncovering the overlapping community structure of complex networks in nature and society
            AssertPathExists(paths, 2128366083, 2164928285, 2112090702);
            // Other papers
            AssertPathExists(paths, 2128366083, 2148606196, 2112090702);
            AssertPathExists(paths, 2128366083, 2153624566, 2112090702);
            AssertPathExists(paths, 2128366083, 2018045523, 2112090702);
        }

        /// <summary>
        /// 罱橪朗壌 的测试样例。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod190()
        {
            // 5-10: 190 -> 204
            var paths = FindPaths(1502768748, 2122841972, true);
            AssertPathsCount(paths, 204);
        }

        /// <summary>
        /// Elecky 的测试样例。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod2595()
        {
            // 5-10: 2595 -> 3592
            var paths = FindPaths(2126125555, 2153635508, true);
            AssertPathsCount(paths, 3592);
        }

        /// <summary>
        /// Benzhong 的测试样例。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod5616()
        {
            // 5-10 从 5616 变成了 5903 。
            // [PaperNode:2126125555] Cross Domain Activity Recognition Via Transfer Learning
            // [PaperNode:2060367530] Statistical Learning Theory
            var paths = FindPaths(2126125555, 2060367530, true);
            AssertPathsCount(paths, 5903);
        }

        /// <summary>
        /// Welthy 的测试样例。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod83()
        {
            // 5-10: 83 -> 122
            // [AuthorNode:2175015405] Xiaolei Li
            // [AuthorNode:2121939561] Jiawei Han
            var paths = FindPaths(2175015405, 2121939561, true);
            AssertPathsCount(paths, 122);
        }

        /// <summary>
        /// 钟泽轩 的测试样例。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod448()
        {
            // 5-10: 448 -> 501
            // [AuthorNode:2145115012] Yu Zheng
            // [AuthorNode:2125800575] Xing Xie
            var paths = FindPaths(2145115012, 2125800575, true);
            AssertPathsCount(paths, 501);
        }

        /// <summary>
        /// 陈楷予 的测试样例。
        /// 反向的 BOP 样例2。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod2470()
        {
            // 5-10: 2470 -> 2708
            // [PaperNode:189831743] Preparing For The Use Of Classification In Online Cataloging Systems And In Online Catalogs
            // [PaperNode:2147152072] Indexing By Latent Semantic Analysis
            var paths = FindPaths(189831743, 2147152072, true);
            AssertPathsCount(paths, 2708);
        }

        /// <summary>
        /// Welthy 的测试样例。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod38()
        {
            // 05-10 38 -> 19
            // [AuthorNode:621499171] Ulrich K Laemmli
            // [PaperNode:2100837269] Cleavage Of Structural Proteins During The Assembly Of The Head Of Bacteriophage T4
            var paths = FindPaths(621499171, 2100837269, true);
            AssertPathExists(paths, 621499171, 2034796909, 1968806977, 2100837269);
            AssertPathExists(paths, 621499171, 2034796909, 1970107317, 2100837269);
            // 05-10 新增断言。
            AssertPathExists(paths, 621499171, 1315039220, 258089896, 2100837269);
            // 注意这是一个环。
            // 05-10 这个环消失了……
            //AssertPathExists(paths, 621499171, 1315039220, 621499171, 2100837269);
            AssertPathsCount(paths, 19);
        }

        /// <summary>
        /// 陈楷予 的测试样例。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod62110()
        {
            DeclareHugeTestCase();
            // [PaperNode:2107710616] Selected reaction monitoring for quantitative proteomics: a tutorial
            // [PaperNode:2128635872] A rapid and sensitive method for the quantitation of microgram quantities of protein utilizing the principle of protein-dye binding.
            var paths = FindPaths(2107710616, 2128635872, true);
            AssertPathsCount(paths, 62110);
        }

        /// <summary>
        /// 万里云间 的测试样例。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod49()
        {
            // [AuthorNode:2146007994] Paul E Goss
            // [PaperNode:2100837269] Cleavage Of Structural Proteins During The Assembly Of The Head Of Bacteriophage T4
            var paths = FindPaths(2146007994, 2100837269, true);
            AssertPathsCount(paths, 49);
        }

        /// <summary>
        /// Welthy 的测试样例。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod2708()
        {
            // [AuthorNode:2146007994] Paul E Goss
            // [PaperNode:2100837269] Cleavage Of Structural Proteins During The Assembly Of The Head Of Bacteriophage T4
            var paths = FindPaths(189831743, 2147152072, true);
            AssertPathsCount(paths, 2708);
        }

        /// <summary>
        /// 金帆 的测试样例。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod595()
        {
            var paths = FindPaths(2018949714, 2105005017, true);
            AssertPathsCount(paths, 595);
        }

        [TestMethod]
        public void AnalyzerTestMethodX1()
        {
            var paths = FindPaths(2088905367, 2033660646, true);
            TraceAsJson(paths);
            AssertPathsCount(paths, 116);
        }

        [TestMethod]
        public void AnalyzerTestMethodX2()
        {
            var paths = FindPaths(2100837269, 621499171, true);
            AssertPathsCount(paths, 10);
        }

        [TestMethod]
        public void AnalyzerTestMethodX3()
        {
            var paths = FindPaths(2008785686, 56455408, true);
            AssertPathsCount(paths, 135);
        }

        //[TestMethod]
        public void AnalyzerTestMethodX4()
        {
            // 05-15    2292217923 消失了……
            var paths = FindPaths(1912875929, 2292217923, true);
            AssertPathsCount(paths, 35);
        }

        /// <summary>
        /// BOP 5-5 放出的样例1。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestBop1()
        {
            // [AuthorNode:2251253715] Raquel Pau
            // [PaperNode:2180737804] Cloudmdsql Querying Heterogeneous Cloud Data Stores With A Common Language
            var paths = FindPaths(2251253715, 2180737804, true);
            AssertPathsCount(paths, 14);
        }

        /// <summary>
        /// BOP 5-5 放出的样例2。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestBop2()
        {
            // [PaperNode:2147152072] Indexing By Latent Semantic Analysis
            // [PaperNode:189831743] Preparing For The Use Of Classification In Online Cataloging Systems And In Online Catalogs
            var paths = FindPaths(2147152072, 189831743, true);
            AssertPathsCount(paths, 18);
        }

        /// <summary>
        /// BOP 5-5 放出的样例3。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestBop3()
        {
            // [PaperNode:2332023333] Recovering Transparent Shape From Time Of Flight Distortion
            // [PaperNode:2310280492] A Robust Multilinear Model Learning Framework For 3D Faces
            var paths = FindPaths(2332023333, 2310280492, true);
            AssertPathsCount(paths, 1);
        }

        /// <summary>
        /// BOP 5-11 放出的样例4。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestBop4()
        {
            // [PaperNode:2332023333] Recovering Transparent Shape From Time Of Flight Distortion
            // [AuthorNode:57898110] Timo Bolkart
            var paths = FindPaths(2332023333, 57898110, true);
            AssertPathExists(paths, 2332023333, 1158167855, 2310280492, 57898110);
            AssertPathsCount(paths, 1);
        }

        /// <summary>
        /// BOP 5-11 放出的样例5。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestBop5()
        {
            // [AuthorNode:57898110] Timo Bolkart
            // [AuthorNode:2014261844] Stefanie Wuhrer
            var paths = FindPaths(57898110, 2014261844, true);
            AssertPathsCount(paths, 26);
        }

        /// <summary>
        /// BOP 放出的样例组合。
        /// </summary>
        //[TestMethod]
        public void AnalyzerTestBop()
        {
            AnalyzerTestBop1();
            AnalyzerTestBop2();
            AnalyzerTestBop3();
            AnalyzerTestBop4();
            AnalyzerTestBop5();
            Trace.WriteLine("GC Count " + string.Join(",", new[] {0,1,2}.Select(GC.CollectionCount)));
        }
    }
}
