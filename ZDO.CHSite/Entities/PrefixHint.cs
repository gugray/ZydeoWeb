namespace ZDO.CHSite.Entities
{
    /// <summary>
    /// One search hint based on query as prefix.
    /// </summary>
    public class PrefixHint
    {
        /// <summary>
        /// The search suggestion.
        /// </summary>
        public string Suggestion { get; set; }
        /// <summary>
        /// Length of query prefix in suggestion (for highliht in UI).
        /// </summary>
        public int PrefixLength { get; set; }
    }
}
