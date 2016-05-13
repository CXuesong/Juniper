using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContestantServerTester.Contract
{
    class TestsContainer
    {
        public IList<Test> Tests { get; set; }
    }

    class Test
    {
        public long[] Challenge { get; set; }

        public IList<long[]> Paths { get; set; }
    }
}
