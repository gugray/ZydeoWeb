using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.CHSite.Entities
{
    public class NewEntryVerifyFullResult
    {
        public bool Passed;
        public List<string> Errors;
        public string Preview = null;
    }
}
