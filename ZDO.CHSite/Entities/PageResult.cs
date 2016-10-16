using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.CHSite.Entities
{
    public class PageResult
    {
        /// <summary>
        /// If true, page must include "noindex" meta tag.
        /// </summary>
        public bool NoIndex;
        /// <summary>
        /// The page's normalized relative URL.
        /// </summary>
        public string RelNorm;
        /// <summary>
        /// The requested page's title.
        /// </summary>
        public string Title;
        /// <summary>
        /// Keywords to include in header for page.
        /// </summary>
        public string Keywords;
        /// <summary>
        /// Description to include in header for page.
        /// </summary>
        public string Description;
        /// <summary>
        /// The HTML content (shown within single-page app's content element).
        /// </summary>
        public string Html;
        /// <summary>
        /// Any additional data return for the receiving page's script.
        /// </summary>
        public object Data;
    }
}
