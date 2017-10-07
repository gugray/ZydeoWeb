using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.ConcAlign
{
    public class SphinxResult
    {
        public List<int> SegPositionsZh;
        public List<int> SegPositionsTrgLo;
        public List<int> SegPositionsTrgStem;
        public string ActualQuery;
        public int TotalCount;
    }
}
