using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class GraphTests
    {
        [TestMethod]
        public void GraphTestMethod1()
        {
            var g = new DirectedGraph<int>();
            for (var i = 0; i < 10; i++)
                g.Add(i);
            Assert.IsTrue(g.Add(1, 2));
            Assert.IsTrue(g.Add(2, 3));
            Assert.IsFalse(g.Add(2, 3));
            Assert.IsTrue(g.Add(9, 3));
            Assert.IsTrue(g.Add(7, 8));
            Assert.AreEqual(10, g.VerticesCount);
            Assert.AreEqual(4, g.EdgesCount);
        }

        [TestMethod]
        public void GraphTestMethod2()
        {
            var g = new DirectedGraph<int>();
            for (var i = 0; i < 10; i++)
                Assert.IsTrue(g.Add(i));
            Assert.IsFalse(g.Add(7));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GraphTestMethod3()
        {
            var g = new DirectedGraph<int>();
            for (var i = 0; i < 10; i++)
                Assert.IsTrue(g.Add(i));
            Assert.IsTrue(g.Add(1, 5));
            g.Add(1, 1);
        }

        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void GraphTestMethod4()
        {
            var g = new DirectedGraph<int>();
            for (var i = 0; i < 10; i++)
                Assert.IsTrue(g.Add(i));
            Assert.IsTrue(g.Add(1, 5));
            Assert.IsTrue(g.Add(1, 30));
            Assert.IsTrue(g.Vertices.Contains(30));
            Assert.IsFalse(g.Add(1, 30));
            g.Add(1, 20, true);
            Assert.AreEqual(11, g.VerticesCount);
            Assert.AreEqual(3, g.EdgesCount);
        }

    }
}
