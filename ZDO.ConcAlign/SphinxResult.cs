using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.ConcAlign
{
    public class SphinxResult
    {
        public List<int> SurfSegPositions = new List<int>();
        public List<KeyValuePair<int, string>> StemmedSegs = new List<KeyValuePair<int, string>>();
        public string StemmedQuery;
        public int TotalCount = 0;
        public float PerlInnerElapsed;
        public float PerlOuterElapsed;
    }
}
