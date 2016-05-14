using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Contests.Bop.Participants.Magik;
using Microsoft.Contests.Bop.Participants.Magik.Academic;
using Microsoft.Contests.Bop.Participants.Magik.Academic.Contract;
using System.Collections.Generic;

namespace UnitTestProject1
{
    [TestClass]
    public class AcademicSearchClientTests
    {
        private void DumpEvaluationResult(EvaluationResult result)
        {
            Assert.IsNotNull(result);
            Trace.WriteLine($"Expression: {result.Expression}");
            Trace.WriteLine($"Entries: [{result.Entities.Count}]");
            foreach (var entity in result.Entities)
            {
                Trace.WriteLine($"Entity: {entity}");
                Trace.WriteLine($"Authors: [{entity.Authors.Length}]");
                Trace.Indent();
                foreach (var author in entity.Authors) Trace.WriteLine(author);
                Trace.Unindent();
                Trace.WriteLine($"Conference: {entity.Conference}");
                Trace.WriteLine($"Journal: {entity.Journal}");
                if (entity.FieldsOfStudy == null)
                    Trace.WriteLine($"Field of Study: -");
                else
                {
                    Trace.WriteLine($"Field of Study: [{entity.FieldsOfStudy.Length}]");
                    Trace.Indent();
                    foreach (var fos in entity.FieldsOfStudy) Trace.WriteLine(fos);
                    Trace.Unindent();
                }
                Trace.WriteLine($"References:[{entity.ReferenceIds.Length}] {string.Join(", ", entity.ReferenceIds)}");
            }
        }

        private void DumpCalcHistogram(CalcHistogramResult result)
        {
            Assert.IsNotNull(result);
            Trace.WriteLine($"Expression: {result.Expression}");
            Trace.WriteLine($"Histograms: [{result.Histograms.Count}]");
            foreach (var hist in result.Histograms)
            {
                Trace.WriteLine($"Histogram: {hist.Attribute}");
                Trace.WriteLine($"Entries: {hist.EntryCount}");
                Trace.Indent();
                foreach (var e in hist.Entries)
                    Trace.WriteLine(e);
                if (hist.EntryCount > hist.Entries.Count)
                    Trace.WriteLine("已截断");
                Trace.Unindent();
            }
        }

        [TestMethod]
        public void ASClientTestMethod1()
        {
            var client = GlobalServices.CreateASClient();
            var result = TestUtility.AwaitSync(
                client.EvaluateAsync("Composite(AA.AuN=='jaime teevan')", 10, 0,
                    GlobalServices.DebugASEvaluationAttributes));
            Assert.IsTrue(result.Entities.Any());
            DumpEvaluationResult(result);
        }

        [TestMethod]
        public void ASClientTestMethod2()
        {
            var client = GlobalServices.CreateASClient();
            var result = TestUtility.AwaitSync(
                client.EvaluateAsync("Composite(AA.AuN=='meredith ringel morris')", 10, 0,
                    GlobalServices.DebugASEvaluationAttributes));
            Assert.IsTrue(result.Entities.Any());
            DumpEvaluationResult(result);
        }

        [TestMethod]
        public void ASClientTestMethod3()
        {
            var client = GlobalServices.CreateASClient();
            var result = new List<EvaluationResult>();
            client.EvaluateAsync("Composite(AA.AfN=='microsoft')", 5000, null, "Id")
                .PartitionContinueWith(er =>
                {
                    result.Add(er);
                    return Task.CompletedTask;
                }).WhenCompleted().Wait();
            Assert.IsTrue(result.Any());
            Trace.WriteLine($"{result.Count} Entities.");
            foreach (var entity in result)
            {
                Trace.WriteLine(entity);
            }
        }

        [TestMethod]
        public void ASClientTestMethod4EstimateCount()
        {
            const int maxCount = 100000;
            var client = GlobalServices.CreateASClient();
            var est = TestUtility.AwaitSync(client.EstimateEvaluationCountAsync(
                "Composite(AA.AfN=='microsoft')", maxCount, 0.01f));
            Trace.WriteLine($"{est} Entities.");
            Assert.IsTrue(est <= maxCount);
            Assert.IsTrue(est >= 23200);
        }

        [TestMethod]
        public void ASClientTestMethod5()
        {
            const int queryCount = 100;
            var client = GlobalServices.CreateASClient();
            var result = TestUtility.AwaitSync(client.CalcHistogramAsync(
                "Composite(AA.AuN=='jaime teevan')", queryCount, 0, "AA.AfN"));
            DumpCalcHistogram(result);
            //if (result.Aborted) Assert.Inconclusive("查询被取消。");
            Assert.AreEqual(result.Histograms.Count, 1);
            Assert.AreEqual(result.Histograms[0].Attribute, "AA.AfN");
            Assert.IsTrue(result.EntityCount >= result.Histograms[0].EntryCount);
            if (result.Histograms[0].EntryCount > queryCount)
                Assert.Inconclusive("返回直方图的分类太多。");
            Assert.AreEqual(result.Histograms[0].EntityCount, result.Histograms[0].Entries.Sum(e => e.Count));
        }

        [TestMethod]
        public void ASClientTestMethod6()
        {
            var client = GlobalServices.CreateASClient();
            client.PagingSize = 20;
            var result = new List<CalcHistogramResult>();
            // 注意：我们得到的其实是 co-author affiliations
            client.CalcHistogramAsync("Composite(AA.AuN=='jaime teevan')", "AA.AfN")
                .PartitionContinueWith(hr =>
                {
                    result.Add(hr);
                    return Task.CompletedTask;
                }).WhenCompleted().Wait();
            foreach (var hr in result)
            {
                Trace.WriteLine(" ============= PAGE =============");
                DumpCalcHistogram(hr);
            }
            Assert.IsTrue(result.Sum(r => r.EntityCount) >= result.Sum(r => r.Histograms[0].EntryCount));
            Assert.AreEqual(result[0].Histograms[0].EntityCount,
                result.Sum(hr => hr.Histograms[0].Entries.Sum(e => e.Count)));
        }
    }
}
