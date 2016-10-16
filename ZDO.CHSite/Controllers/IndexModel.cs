using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Encodings.Web;

using ZDO.CHSite.Entities;

namespace ZDO.CHSite.Controllers
{
    public class IndexModel
    {
        public readonly string Lang;
        public readonly string Rel;
        /// <summary>
        /// Dynamic content to show.
        /// </summary>
        public readonly PageResult PR;
        /// <summary>
        /// Google Analytics code.
        /// </summary>
        public readonly string GACode;

        /// <summary>
        /// Ctor: init immutable instance.
        /// </summary>
        public IndexModel(string lang, string rel, PageResult pr, string gaCode)
        {
            Lang = lang;
            Rel = rel;
            PR = pr;
            GACode = gaCode;
        }

        public string Str(string str)
        {
            return TextProvider.Instance.GetString(Lang, str);
        }
    }
}
