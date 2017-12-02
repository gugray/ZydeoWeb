using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.ConcAlign.Controllers
{
    public class SearchResult
    {
        public List<string> SrcSegs = new List<string>();
        public List<string> TrgSegs = new List<string>();
        public int TotalCount;
        public string ActualQuery = "";
    }
}
