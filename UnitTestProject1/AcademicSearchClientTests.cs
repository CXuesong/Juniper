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
            var client = new AcademicSearchClient(Utility.AcademicSearchSubscriptionKey);
            var result = TestUtility.AwaitSync(
                client.EvaluateAsync("Composite(AA.AuN=='jaime teevan')", 10, 0,
                    Utility.DebugASEvaluationAttributes));
            Assert.IsNotNull(result);
            Trace.WriteLine($"Expression: {result.Expression}");
            foreach (var entity in result.Entities)
            {
                Trace.WriteLine($"Entity: {entity}");
                Trace.Indent();
                foreach (var author in entity.Authors)
                    Trace.WriteLine($"Author: {author}");
                Trace.WriteLine($"Conference: {entity.Conference}");
                Trace.WriteLine($"Journal: {entity.Journal}");
                Trace.WriteLine($"References: {string.Join(", ", entity.ReferenceIds)}");
                Trace.Unindent();
            }
            Assert.IsTrue(result.Entities.Any());
        }

    }
}
