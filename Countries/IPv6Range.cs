using System.Runtime.InteropServices;

namespace Countries
{
    /// <summary>
    /// Represents one IPv6 range, and its associated country ID.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IPv6Range
    {
        /// <summary>
        /// First address in range: HI part
        /// </summary>
        public UInt128 RangeFirst;
        /// <summary>
        /// Last address in range
        /// </summary>
        public UInt128 RangeLast;
        /// <summary>
        /// ID of country for this range
        /// </summary>
        public byte CountryId;
    }
}
