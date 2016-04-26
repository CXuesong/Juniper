using System;
using System.Collections.Generic;
using Microsoft.Contests.Bop.Participants.Magik.Analyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class GraphTests
    {
        [TestMethod]
        public void GraphTestMethod1()
        {
            var g = new Graph<int>();
            for (var i = 0; i < 10; i++)
                g.Add(i);
            g.Add(1, 2);
            g.Add(2, 3);
            //g.Add(2, 3);
            g.Add(9, 3);
            g.Add(7, 8);
            Assert.AreEqual(10, g.VerticesCount);
            Assert.AreEqual(4, g.EdgesCount);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GraphTestMethod2()
        {
            var g = new Graph<int>();
            for (var i = 0; i < 10; i++)
                g.Add(i);
            g.Add(7);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GraphTestMethod3()
        {
            var g = new Graph<int>();
            for (var i = 0; i < 10; i++)
                g.Add(i);
            g.Add(1, 5);
            g.Add(1, 1);
        }

        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void GraphTestMethod4()
        {
            var g = new Graph<int>();
            for (var i = 0; i < 10; i++)
                g.Add(i);
            g.Add(1, 5);
            g.Add(1, 30);
        }

    }
}
