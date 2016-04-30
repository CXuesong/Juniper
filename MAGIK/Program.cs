using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;

namespace Microsoft.Contests.Bop.Participants.Magik
{
    class Program
    {

        internal static void Main(string[] args)
        {
            try
            {
                Task.WaitAll(MainAsync());
            }
            catch (AggregateException ex)
            {
                // 展开内部异常。
                if (ex.InnerExceptions.Count == 1)
                    ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
                throw;
            }
        }

        private static async Task MainAsync()
        {
            var client = GlobalServices.ASClient;
            var expr = SearchExpressionBuilder.AuthorIdContains(1982462162);
            var e = SearchExpressionBuilder.Or(expr, expr);
            for (int i = 0; i < 53; i++)
            {
                e = SearchExpressionBuilder.Or(expr, e);
            }
            var result = await client.EvaluateAsync(e, 10, 0);
            //var result = await client.EvaluateAsync("Composite(AA.AuN=='jaime teevan')", 10, 0);
            client.TraceStatistics();
        }
    }
}
