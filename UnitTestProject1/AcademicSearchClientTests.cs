using System;
using System.Diagnostics;
using System.Linq;
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
            Trace.WriteLine($"Entries: [{result.Entities.Length}]");
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
            var client = GlobalServices.ASClient;
            var result = TestUtility.AwaitSync(
                client.EvaluateAsync("Composite(AA.AuN=='jaime teevan')", 10, 0,
                    GlobalServices.DebugASEvaluationAttributes));
            Assert.IsTrue(result.Entities.Any());
            DumpEvaluationResult(result);
        }

        [TestMethod]
        public void ASClientTestMethod2()
        {
            var client = GlobalServices.ASClient;
            var result = TestUtility.AwaitSync(
                client.EvaluateAsync("Composite(AA.AuN=='meredith ringel morris')", 10, 0,
                    GlobalServices.DebugASEvaluationAttributes));
            Assert.IsTrue(result.Entities.Any());
            DumpEvaluationResult(result);
        }
    }
}
