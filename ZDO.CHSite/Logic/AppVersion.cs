using System;
using System.Reflection;

namespace ZDO.CHSite.Logic
{
    public static class AppVersion
    {
        public static readonly string VerStr;

        static AppVersion()
        {
            Assembly a = typeof(AppVersion).GetTypeInfo().Assembly;
            string fn = a.FullName;
            int ix1 = fn.IndexOf("Version=") + "Version=".Length;
            int ix2 = fn.IndexOf('.', ix1);
            int ix3 = fn.IndexOf('.', ix2 + 1);
            string strMajor = fn.Substring(ix1, ix2 - ix1);
            string strMinor = fn.Substring(ix2 + 1, ix3 - ix2 - 1);
            VerStr = strMajor + "." + strMinor;
        }
    }
}

