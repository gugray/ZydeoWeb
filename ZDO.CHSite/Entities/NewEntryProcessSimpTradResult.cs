using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.CHSite.Entities
{
    public class NewEntryProcessSimpTradResult
    {
        public List<List<string>> Pinyin = new List<List<string>>();
        public bool IsKnownHeadword = false;
    }
}
