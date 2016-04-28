using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            if (assertPathExists) Assert.AreNotEqual(0, paths.Count);
            foreach (var p in paths)
            {
                Assert.IsTrue(p.Length >= 2);   // 1-hop
                Assert.IsTrue(p.Length <= 4);   // 3-hop
                Assert.AreEqual(id1, p[0]);
                Assert.AreEqual(id2, p[p.Length - 1]);
            }
            return paths;
        }


        [TestMethod]
        public void AnalyzerEasyTest1()
        {
            // 2157025439: what do people ask their social networks and why a survey study of status message q a behavior
            // 2061503185: implicit feedback for inferring user preference a bibliography
            var paths = FindPaths(2157025439, 2061503185, true);
            // 1982462162: Jaime teevan
            Assert.IsTrue(paths.Any(p => p.Any(n => n.Id == 1982462162)));
        }
    }
}
