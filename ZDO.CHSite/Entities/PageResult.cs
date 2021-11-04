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
        public bool NoIndex { get; set; }
        /// <summary>
        /// The page's normalized relative URL.
        /// </summary>
        public string RelNorm { get; set; }
        /// <summary>
        /// The requested page's title.
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// Keywords to include in header for page.
        /// </summary>
        public string Keywords { get; set; }
        /// <summary>
        /// Description to include in header for page.
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// The HTML content (shown within single-page app's content element).
        /// </summary>
        public string Html { get; set; }
        /// <summary>
        /// Any additional data return for the receiving page's script.
        /// </summary>
        public object Data { get; set; }
    }
}
