using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.CHSite.Entities
{
    public class NewEntryProcessSimpResult
    {
        public List<List<string>> Trad { get; set; } = new List<List<string>>();
        public List<List<string>> Pinyin { get; set; } = new List<List<string>>();
        public bool IsKnownHeadword { get; set; } = false;
    }
}
