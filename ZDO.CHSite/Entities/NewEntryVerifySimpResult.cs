using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.CHSite.Entities
{
    public class NewEntryVerifySimpResult
    {
        public bool Passed { get; set; }
        public List<string> Errors { get; set; } = null;
    }
}
