using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.ConcAlign.Controllers
{
    public class SearchHit
    {
        public List<string> SrcTokens = new List<string>();
        public List<string> TrgTokens = new List<string>();
        public List<int[]> Map = new List<int[]>();
    }
}
