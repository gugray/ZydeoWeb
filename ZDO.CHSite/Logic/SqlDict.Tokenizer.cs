using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;

using ZD.Common;

namespace ZDO.CHSite.Logic
{
    partial class SqlDict
    {
        /// <summary>
        /// One indexable token in a sense's Equiv field.
        /// </summary>
        public class Token
        {
            /// <summary>
            /// Placeholder for numbers in the normalized form.
            /// </summary>
            public static readonly string Num = "NNN";

            /// <summary>
            /// The word's surface form, directly from the sense.
            /// </summary>
            public readonly string Surf;
            /// <summary>
            /// The word's normalized form, for use in the index. "NNN" for numbers.
            /// </summary>
            public readonly string Norm;
            /// <summary>
            /// Start of surface form in Equiv text. (Length comes from Surf.)
            /// </summary>
            public readonly ushort Start;

            /// <summary>
            /// Ctor: init immutable instance.
            /// </summary>
            public Token(string surf, string norm, ushort start)
            {
                Surf = surf;
                Norm = norm;
                Start = start;
            }
        }

        /// <summary>
        /// Provides functionality for tokenizing Equiv strings.
        /// </summary>
        public class Tokenizer
        {
            /// <summary>
            /// Punctuation that we trim from start and end of words.
            /// </summary>
            static char[] trimPunctChars = new char[] { ',', ';', ':', '.', '?', '!', '-', '/', '\'', '"', '(', ')', '[', ']' };

            /// <summary>
            /// Definition of "number", i.e., numerical entity that we don't index as a content word.
            /// </summary>
            private readonly Regex reNumbers = new Regex(@"^([0-9\-\.\:\,\^\%]+|[0-9]+(th|nd|rd|st|s|m))$");

            /// <summary>
            /// Reused stringbuilder for accumulating surface forms.
            /// </summary>
            private readonly StringBuilder curr = new StringBuilder();

            /// <summary>
            /// Tokenizes a sense's Equiv, or a query string.
            /// </summary>
            public List<Token> Tokenize(string text)
            {
                List<Token> res = new List<Token>();
                curr.Clear();
                int i;
                for (i = 0; i != text.Length; ++i)
                {
                    char c = text[i];
                    // Cannot be in words: whitespace, or any trimmed punctuation; also ignore Hanzi
                    if (char.IsWhiteSpace(c) || trimPunctChars.Contains(c) || Utils.IsHanzi(c))
                    {
                        if (curr.Length != 0)
                        {
                            res.Add(makeToken(curr, i));
                            curr.Clear();
                        }
                    }
                    // Otherwise, append to current token
                    else curr.Append(c);
                }
                // Last token
                if (curr.Length != 0) res.Add(makeToken(curr, i));
                // Done.
                return res;
            }

            /// <summary>
            /// Makes one token from finished surface form; creates normalized form.
            /// </summary>
            private Token makeToken(StringBuilder curr, int i)
            {
                // Surface form as is
                string surf = curr.ToString();
                // Normalize: ß to ss; lower-case
                curr.Replace("ß", "ss");
                string norm = curr.ToString().ToLowerInvariant();
                // If it's a number, surface form is NNN
                if (reNumbers.IsMatch(surf)) norm = Token.Num;
                // Ze token
                return new Token(surf, norm, (ushort)(i - surf.Length));
            }
        }
    }
}
