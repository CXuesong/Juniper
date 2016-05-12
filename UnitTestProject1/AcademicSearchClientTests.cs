﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Contests.Bop.Participants.Magik;
using Microsoft.Contests.Bop.Participants.Magik.Academic;
using Microsoft.Contests.Bop.Participants.Magik.Academic.Contract;

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
            var result = TestUtility.AwaitSync(client.EvaluateAsync(
                "Composite(AA.AfN=='microsoft')", 5000, "Id",
                page => Task.FromResult(page.Entities)))
                .SelectMany(page => page)
                .ToList();
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
    }
}
