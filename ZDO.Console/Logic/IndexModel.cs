using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Encodings.Web;

namespace ZDO.Console.Logic
{
    public class IndexModel
    {
        public SiteConfig[] Sites;
        public string Rel;

        public string GetClass(int i)
        {
            if (Sites[i].ShortName == Rel) return "site selected";
            else return "site";
        }
    }
}
