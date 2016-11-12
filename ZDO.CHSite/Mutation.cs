using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZDO.CHSite
{
    /// <summary>
    /// Mutations of the app: HanDeDict or CHDICT.
    /// </summary>
    public enum Mutation
    {
        /// <summary>
        /// HanDeDict
        /// </summary>
        HDD,
        /// <summary>
        /// CHDICT
        /// </summary>
        CHD,
    }

    public enum HostingEnv
    {
        Development,
        Staging,
        Production,
    }
}
