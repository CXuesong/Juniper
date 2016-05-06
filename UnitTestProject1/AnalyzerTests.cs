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
        private ICollection<KgNode[]> FindPaths(long id1, long id2, bool assertPathExists)
        {
            var asc = GlobalServices.CreateASClient();
            var a = new Analyzer(asc);
            var paths = TestUtility.AwaitSync(a.FindPathsAsync(id1, id2));
            Trace.WriteLine(asc.DumpStatistics());
            Trace.WriteLine(a.DumpStatistics());
            //a.TraceGraph();
            Trace.WriteLine($"路径 {id1} -> {id2} [{paths.Length}]");
            Trace.Indent();
            foreach (var p in paths)
            {
                Trace.WriteLine(string.Join("\n\t->", (IEnumerable<KgNode>) p));
            }
            Trace.Unindent();
            if (assertPathExists) Assert.AreNotEqual(0, paths.Length);
            foreach (var p in paths)
            {
                Assert.IsTrue(p.Length >= 2); // >= 1-hop
                Assert.IsTrue(p.Length <= 4); // <= 3-hop
                Assert.AreEqual(id1, p[0].Id);
                Assert.AreEqual(id2, p[p.Length - 1].Id);
            }
            return paths;
        }

        private void AssertPathExists(ICollection<KgNode[]> paths, params long[] idPath)
        {
            Assert.IsTrue(paths.Any(p => p.Select(n => n.Id).SequenceEqual(idPath)),
                $"在路径集合[{paths.Count}]中找不到路径 {string.Join(" -> ", idPath)} 。");
        }

        /// <summary>
        /// 1-Hop Id - AuId
        /// </summary>
        [TestMethod]
        public void AnalyzerTestPreliminary1()
        {
            // 2157025439: what do people ask their social networks and why a survey study of status message q a behavior
            // 1982462162: Jaime Teevan
            var paths = FindPaths(2157025439, 1982462162, true);
            // 1/2 hop + 3-hop
            Assert.IsTrue(paths.Count >= 2 + 258);
            AssertPathExists(paths, 2157025439, 1982462162);
        }

        /// <summary>
        /// 2-Hop Id - Id
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethodEasy1()
        {
            // 2157025439: what do people ask their social networks and why a survey study of status message q a behavior
            // 2061503185: implicit feedback for inferring user preference a bibliography
            var paths = FindPaths(2157025439, 2061503185, true);
            // 1/2 hop + 3-hop
            Assert.IsTrue(paths.Count >= 3 + 239);
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
            // 1982462162: Jaime Teevan
            // 676500258: Susan T Dumais
            var paths = FindPaths(1982462162, 676500258, true);
            Assert.IsTrue(paths.Count >= 39 + 50);
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
            // 2128366083: resolution limit in community detection
            // 2112090702: collective dynamics of small world network
            var paths = FindPaths(2128366083, 2112090702, true);
            Assert.IsTrue(paths.Count >= 4 + 1524);
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
            var paths = FindPaths(1502768748, 2122841972, true);
            Assert.IsTrue(paths.Count >= 190);
        }

        /// <summary>
        /// Elecky 的测试样例。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod2595()
        {
            var paths = FindPaths(2126125555, 2153635508, true);
            Assert.IsTrue(paths.Count >= 2595);
        }

        /// <summary>
        /// Benzhong 的测试样例。
        /// Elecky 表示有 5598 条。
        /// 但我觉得有 5616 条。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestMethod5616()
        {
            var paths = FindPaths(2126125555, 2060367530, true);
            Assert.IsTrue(paths.Count >= 5616);
        }

        /// <summary>
        /// BOP 5-5 放出的样例1。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestBop1()
        {
            var paths = FindPaths(2251253715, 2180737804, true);
            Assert.AreEqual(14, paths.Count);
        }

        /// <summary>
        /// BOP 5-5 放出的样例2。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestBop2()
        {
            var paths = FindPaths(2147152072, 189831743, true);
            Assert.AreEqual(18, paths.Count);
        }

        /// <summary>
        /// BOP 5-5 放出的样例2。
        /// </summary>
        [TestMethod]
        public void AnalyzerTestBop3()
        {
            var paths = FindPaths(2332023333, 2310280492, true);
            Assert.AreEqual(1, paths.Count);
        }
    }
}
