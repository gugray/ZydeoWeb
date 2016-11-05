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
        /// <summary>
        /// ZDO mutation: HDD or CHD.
        /// </summary>
        public readonly Mutation Mut;
        /// <summary>
        /// Page language (two-letter code, or our funnies for simplified/traditional Chinese).
        /// </summary>
        public readonly string Lang;
        /// <summary>
        /// Relative URL.
        /// </summary>
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
        public IndexModel(Mutation mut, string lang, string rel, PageResult pr, string gaCode)
        {
            Mut = mut;
            Lang = lang;
            Rel = rel;
            PR = pr;
            GACode = gaCode;
        }

        /// <summary>
        /// Gets a localized display string by key.
        /// </summary>
        public string Str(string str)
        {
            return TextProvider.Instance.GetString(Lang, str);
        }

        /// <summary>
        /// Returns the class the body element must receive.
        /// </summary>
        public string BodyClass()
        {
            string cls = Mut == Mutation.CHD ? "chd" : "hdd";
            if (PR.Html != "") cls += " has-initial-content";
            return cls;
        }
    }
}
