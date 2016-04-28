using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class KgNodeTests
    {
        private void AssertNodeExists(IEnumerable<KgNodePair> nodes, long id)
        {
            Assert.IsTrue(nodes.Any(n => n.Node1.Id == id || n.Node2.Id == id),
                $"在节点集合[{nodes.Count()}]中找不到 Id 为 {id} 的节点。");
        }

        private void AssertNodeExists(IEnumerable<KgNodePair> nodes, params long[] ids)
        {
            foreach (var id in ids)
            {
                AssertNodeExists(nodes, id);
            }
        }

        /// <summary>
        /// 对 PaperNode 进行测试。
        /// </summary>
        [TestMethod]
        public void KgNodeTestMethod1()
        {
            // 2157025439: what do people ask their social networks and why a survey study of status message q a behavior
            // 2061503185: implicit feedback for inferring user preference a bibliography
            var paper1 = new PaperNode(2157025439, "what do people ask ...");
            var paper2 = new PaperNode(2061503185, "implicit feedback for inferring ...");
            var adj1 = TestUtility.AwaitSync(paper1.GetAdjacentNodesAsync());
            var adj2 = TestUtility.AwaitSync(paper2.GetAdjacentNodesAsync());
            // Author + Conference/Journal + Field of Study + References
            Assert.AreEqual(3*2 + 1*2 + 3*2 + 26, adj1.Count);
            // Authors
            AssertNodeExists(adj1, 2123314761, 1982462162, 2063838112);
            // FoS
            AssertNodeExists(adj1, 86256295, 521815418, 41008148);
            // References
            AssertNodeExists(adj1, 2134746982, 2139398774, 2124142520,
                2140173168, 2122841972, 1980580900, 2038631615,
                2135555017, 2102958620, 2162209053, 2064522604,
                1114905064, 2126104150, 2163475640, 2135342207,
                1980908438, 2071373254, 2233354937, 2111141603,
                2026569904, 1517685083, 2077150935, 1967873612,
                2049614562, 2027253226, 1975410736);
            Assert.AreEqual(2*2 + 1*2 + 3*2 + 31, adj2.Count);
            // Authors
            AssertNodeExists(adj2, 2180501019, 1982462162);
            // FoS
            AssertNodeExists(adj2, 99016210, 23123220, 41008148);
            // References
            AssertNodeExists(adj2, 2138621811, 2270907722, 1999047234,
                2162077280, 2074680184, 2012516036, 2142094977,
                2066590388, 2006551346, 2031636842, 1968397045,
                2005405160, 2170541075, 1508511232, 2152725628,
                1522744294, 2085408650, 2141110299, 2143471387,
                2170406741, 2094577819, 2099716451, 2095797281,
                1966042082, 1600518742, 1881727220, 1979229249,
                2132809512, 24452059, 2143321903, 2004461062);
        }

        /// <summary>
        /// 对 AuthorNode 进行测试。
        /// </summary>
        [TestMethod]
        public void KgNodeTestMethod2()
        {
            // 2123314761: Meredith Ringel Morris
            // 1982462162: Jaime Teevan
            var author1 = new AuthorNode(2123314761, "Meredith Ringel Morris");
            var author2 = new AuthorNode(1982462162, "Jaime Teevan");
            var adj1 = TestUtility.AwaitSync(author1.GetAdjacentNodesAsync());
            var adj2 = TestUtility.AwaitSync(author2.GetAdjacentNodesAsync());
            // 150篇论文 + 4机构
            Assert.IsTrue(adj1.Count/2 >= 150 + 4);
            // 128篇论文 + 3机构
            Assert.IsTrue(adj2.Count/2 >= 128 + 3);
        }

        /// <summary>
        /// 对 AffiliationNode 进行测试。
        /// </summary>
        [TestMethod]
        public void KgNodeTestMethod3()
        {
            // 1290206253: Microsoft
            // 63966007: Massachusetts Institute Of Technology
            var affiliation1 = new AffiliationNode(1290206253, "Microsoft");
            var affiliation2 = new AffiliationNode(63966007, "Massachusetts Institute Of Technology");
            var adj1 = TestUtility.AwaitSync(affiliation1.GetAdjacentNodesAsync());
            var adj2 = TestUtility.AwaitSync(affiliation2.GetAdjacentNodesAsync());
            Assert.IsTrue(adj1.Count/2 >= 1000);
            Assert.IsTrue(adj2.Count/2 >= 1000);
        }
    }
}
