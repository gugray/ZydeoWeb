using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZD.Common
{
    public class EntryId
    {
        /// <summary>
        /// Helper for long<>Alpha10 conversion.
        /// </summary>
        private static char numToChar(int num)
        {
            if (num < 26) return (char)(num + 'a');
            if (num < 52) return (char)(num - 26 + 'A');
            throw new Exception("Invalid conversion to char: " + num.ToString());
        }

        /// <summary>
        /// Helper for long<>Alpha10 conversion.
        /// </summary>
        private static int charToNum(char c)
        {
            if (c >= 'a' && c <= 'z') return c - 'a';
            if (c >= 'A' && c <= 'Z') return c - 'A' + 26;
            throw new Exception("Invalid conversion to number: #" + ((int)c).ToString());
        }

        /// <summary>
        /// Converts ID to Alpha7.
        /// </summary>
        public static string IdToString(int id)
        {
            // x00xx0x
            char[] res = new char[7];
            int x = id;
            res[6] = numToChar(x % 52); x /= 52;
            res[5] = (char)((x % 10) + '0'); x /= 10;
            res[4] = numToChar(x % 52); x /= 52;
            res[3] = numToChar(x % 52); x /= 52;
            res[2] = (char)((x % 10) + '0'); x /= 10;
            res[1] = (char)((x % 10) + '0'); x /= 10;
            res[0] = numToChar(x % 52); x /= 52;
            if (x != 0) throw new Exception("Number cannot be represented: " + id.ToString());
            return new string(res);
        }

        /// <summary>
        /// Converts Alpha7 to long ID.
        /// </summary>
        public static int StringToId(string code)
        {
            // xx0x00xx0x
            if (code.Length != 7) throw new Exception("Invalid representation: " + code);
            int res = 0;
            res += charToNum(code[0]) * (10 * 10 * 52 * 52 * 10 * 52);
            res += (code[1] - '0') * (10 * 52 * 52 * 10 * 52);
            res += (code[2] - '0') * (52 * 52 * 10 * 52);
            res += charToNum(code[3]) * (52 * 10 * 52);
            res += charToNum(code[4]) * (10 * 52);
            res += (code[5] - '0') * (52);
            res += charToNum(code[6]);
            return res;
        }
    }
}
