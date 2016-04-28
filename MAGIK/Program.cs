using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;

namespace Microsoft.Contests.Bop.Participants.Magik
{
    class Program
    {

        internal static void Main(string[] args)
        {
            Task.WaitAll(MainAsync());
        }

        private static async Task MainAsync()
        {
            var client = GlobalServices.ASClient;
            var result = await client.EvaluateAsync("Composite(AA.AuN=='jaime teevan')", 10, 0);
        }
    }
}
