using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    partial class Analyzer
    {
        private async Task<IEnumerable<KgNode[]>> FindHop3PathsAsync(KgNode node1, KgNode node2)
        {
            Debug.Assert(node1 != null);
            Debug.Assert(node2 != null);
            await Task.WhenAll(ExploreAsync(node1.Id), ExploreAsync(node2.Id));
            var paths = new List<KgNode[]>();

            // TODO 这才是重头戏啊。也是难点。

            return paths;
        }
    }
}
