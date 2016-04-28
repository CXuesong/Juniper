using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class AnalyzerTests
    {
        private ICollection<KgNode[]> FindPaths(long id1, long id2, bool assertPathExists)
        {
            var a = new Analyzer();
            var paths = TestUtility.AwaitSync(a.FindPathsAsync(id1, id2));
            Trace.WriteLine($"Paths {id1} -> {id2} [{paths.Count}]");
            Trace.Indent();
            foreach (var p in paths)
            {
                Trace.WriteLine(string.Join("\n\t->", (IEnumerable<KgNode>) p));
            }
            Trace.Unindent();
            a.TraceStatistics();
            a.TraceGraph();
            if (assertPathExists) Assert.AreNotEqual(0, paths.Count);
            foreach (var p in paths)
            {
                Assert.IsTrue(p.Length >= 2);   // >= 1-hop
                Assert.IsTrue(p.Length <= 4);   // <= 3-hop
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

        [TestInitialize]
        public void OnTestInitialize()
        {
            // 这样可以更新调用计数器。
            GlobalServices.ASClient = GlobalServices.CreateASClient();
        }

        [TestCleanup]
        public void OnTestCleanup()
        {
            GlobalServices.ASClient.TraceStatistics();
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
            // 2057034832: Understanding Temporal Query Dynamics
            AssertPathExists(paths, 1982462162, 2057034832, 676500258);
            // 1290206253: Microsoft
            AssertPathExists(paths, 1982462162, 1290206253, 676500258);
        }
    }
}
