using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Contests.Bop.Participants.Magik;
using Microsoft.Contests.Bop.Participants.Magik.Academic;

namespace UnitTestProject1
{
    [TestClass]
    public class AcademicSearchClientTests
    {
        [TestMethod]
        public void ASClientTestMethod1()
        {
            var client = GlobalServices.ASClient;
            var result = TestUtility.AwaitSync(
                client.EvaluateAsync("Composite(AA.AuN=='jaime teevan')", 10, 0,
                    GlobalServices.DebugASEvaluationAttributes));
            Assert.IsNotNull(result);
            Trace.WriteLine($"Expression: {result.Expression}");
            foreach (var entity in result.Entities)
            {
                Trace.WriteLine($"Entity: {entity}");
                Trace.WriteLine($"Authors: [{entity.Authors.Length}]");
                Trace.Indent();
                foreach (var author in entity.Authors) Trace.WriteLine(author);
                Trace.Unindent();
                Trace.WriteLine($"Conference: {entity.Conference}");
                Trace.WriteLine($"Journal: {entity.Journal}");
                Trace.WriteLine($"Field of Study: [{entity.FieldsOfStudy.Length}]");
                Trace.Indent();
                foreach (var fos in entity.FieldsOfStudy) Trace.WriteLine(fos);
                Trace.Unindent();
                Trace.WriteLine($"References:[{entity.ReferenceIds.Length}] {string.Join(", ", entity.ReferenceIds)}");
            }
            Assert.IsTrue(result.Entities.Any());
        }

    }
}
