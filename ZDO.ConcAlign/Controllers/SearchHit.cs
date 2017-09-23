using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.ConcAlign.Controllers
{
    public class TrgHi
    {
        public int Start;
        public int Len;
        public int Score;
    }

    public class SearchHit
    {
        public string Source;
        public int SrcHiStart;
        public int SrcHiLen;
        public string Target;
        public List<TrgHi> TrgHilights = new List<TrgHi>();
    }
}
