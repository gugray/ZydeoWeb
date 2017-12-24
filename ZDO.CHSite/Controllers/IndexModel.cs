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
        /// Base URL
        /// </summary>
        public readonly string BaseUrl;
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
        /// Application version (X.Y)
        /// </summary>
        public readonly string VerStr;
        /// <summary>
        /// Site key for Google reCaptchas.
        /// </summary>
        public readonly string CaptchaSiteKey;

        /// <summary>
        /// Ctor: init immutable instance.
        /// </summary>
        public IndexModel(Mutation mut, string baseUrl, string lang, string rel, 
            PageResult pr, string gaCode, string verStr, string captchaSiteKey)
        {
            Mut = mut;
            BaseUrl = baseUrl;
            Lang = lang;
            Rel = rel;
            PR = pr;
            GACode = gaCode;
            VerStr = "v" + verStr;
            CaptchaSiteKey = captchaSiteKey;
        }

        /// <summary>
        /// Gets a localized display string by key.
        /// </summary>
        public string Str(string str)
        {
            return TextProvider.Instance.GetString(Lang, str);
        }

        /// <summary>
        /// Gets the class the body element must receive.
        /// </summary>
        public string BodyClass
        {
            get
            {
                string cls = Mut == Mutation.CHD ? "chd" : "hdd";
                if (PR.Html != "") cls += " has-initial-content";
                return cls;
            }
        }

        public string HdrSearchClass
        {
            get
            {
                if (Rel == "/") return "hdrSearch hdrAlt on welcome";
                else return "hdrSearch hdrAlt on";
            }
        }

        public string DynPageClass
        {
            get
            {
                if (Rel == "") return "nosubmenu";
                else if (Rel.StartsWith("search/")) return "search";
                else return "";
            }
        }

        /// <summary>
        /// Gets the language to be marked up on the HTML element.
        /// </summary>
        public string DocLang
        {
            get
            {
                if (Lang == "jian") return "zh-TW";
                else if (Lang == "fan") return "zh-CN";
                else return Lang;
            }
        }

        /// <summary>
        /// Current year, for HDD's copyright notice.
        /// </summary>
        public string Year
        {
            get { return DateTime.UtcNow.Year.ToString(); }
        }

        /// <summary>
        /// CHD's entire copyright notice.
        /// </summary>
        public string BottomCopy
        {
            get
            {
                string res = "©";
                if (DateTime.UtcNow.Year == 2017) res += "2017";
                else res += "2017-" + DateTime.UtcNow.Year.ToString();
                if (Lang == "hu") res += " Ugray Gábor";
                else res += " Gábor L Ugray";
                return res;
            }
        }

        public string TeaserImageUrl
        {
            get { return BaseUrl + "static/chdict-teaser.png"; }
        }
    }
}
