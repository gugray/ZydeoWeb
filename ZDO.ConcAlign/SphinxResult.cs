using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.ConcAlign
{
    public class SphinxResult
    {
        public List<int> SegPositionsZh = new List<int>();
        public List<int> SegPositionsTrgLo = new List<int>();
        public List<int> SegPositionsTrgStem = new List<int>();
        public string ActualQuery;
        public int TotalCount = 0;
    }
}
