using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

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
            /// Placeholder for Chinese embeddings in the normalized form.
            /// </summary>
            public static readonly string Zho = "ZHO";

            /// <summary>
            /// The word's surface form, directly from the sense. Includes pipe if present in text.
            /// </summary>
            public readonly string Surf;
            /// <summary>
            /// The word's normalized form, for use in the index. "NNN" for numbers. No pipe; ß resolved as ss.
            /// </summary>
            public readonly string Norm;
            /// <summary>
            /// Start of surface form in Equiv text. (Length comes from Surf.)
            /// </summary>
            public readonly ushort Start;
            /// <summary>
            /// <para>Split position if token's parts are also to be indexed separately.</para>
            /// <para>Applies to <see cref="Surf"/>: position of pipe. 0 of no split.</para>
            /// </summary>
            public readonly ushort SplitPosSurf;
            /// <summary>
            /// Split position in <see cref="Norm"/>: start index of second half.
            /// </summary>
            public readonly ushort SplitPosNorm;

            /// <summary>
            /// Ctor: init immutable instance, no split position.
            /// </summary>
            public Token(string surf, string norm, ushort start)
            {
                Surf = surf;
                Norm = norm;
                Start = start;
                SplitPosSurf = SplitPosNorm = 0;
            }
            /// <summary>
            /// Ctor: init immutable instance, with split position.
            /// </summary>
            public Token(string surf, string norm, ushort start, ushort splitPosSurf, ushort splitPosNorm)
            {
                Surf = surf;
                Norm = norm;
                Start = start;
                SplitPosSurf = splitPosSurf;
                SplitPosNorm = splitPosNorm;
                if (SplitPosSurf != 0 && SplitPosSurf == Surf.Length - 1)
                    throw new ArgumentException("Split position(surf) can only be inside word: " + Surf);
                if (SplitPosNorm != 0 && SplitPosNorm == Norm.Length)
                    throw new ArgumentException("Split position (norm) can only be inside word: " + Surf);
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
                bool inHanzi = false;
                ushort splitPos = 0;
                for (i = 0; i != text.Length; ++i)
                {
                    char c = text[i];
                    // Cannot be in words: whitespace, or any trimmed punctuation; also ignore Hanzi
                    if (char.IsWhiteSpace(c) || trimPunctChars.Contains(c))
                    {
                        if (curr.Length != 0)
                        {
                            res.Add(makeToken(curr, i, splitPos));
                            curr.Clear(); splitPos = 0;
                        }
                    }
                    // Character is Hanzi: wrap up previous alfa token, or append to Hanzi embedding
                    else if (Utils.IsHanzi(c))
                    {
                        if (inHanzi) curr.Append(c);
                        else
                        {
                            if (curr.Length != 0)
                            {
                                res.Add(makeToken(curr, i, splitPos));
                                curr.Clear(); splitPos = 0;
                            }
                            curr.Append(c);
                            inHanzi = true;
                        }
                    }
                    // Character is alfa or pipe: wrap up previous Hanzi token, or append to current alfa token
                    else
                    {
                        if (!inHanzi)
                        {
                            if (c == '|') splitPos = (ushort)curr.Length;
                            curr.Append(c);
                        }
                        else
                        {
                            if (curr.Length != 0)
                            {
                                res.Add(makeToken(curr, i, splitPos));
                                curr.Clear(); splitPos = 0;
                            }
                            curr.Append(c);
                            inHanzi = false;
                        }
                    }
                }
                // Last token
                if (curr.Length != 0) res.Add(makeToken(curr, i, splitPos));
                // Done.
                return res;
            }

            /// <summary>
            /// Makes one token from finished surface form; creates normalized form.
            /// </summary>
            private Token makeToken(StringBuilder curr, int endPos, ushort splitPos)
            {
                // Surface form as is
                string surf = curr.ToString();
                // Token is Hanzi embedding
                if (Utils.IsHanzi(surf[0]))
                    return new Token(surf, Token.Zho, (ushort)(endPos - surf.Length));
                // Normalize: ß to ss; lower-case
                StringBuilder sbNorm = new StringBuilder(curr.Length + 4);
                ushort splitPosNorm = splitPos;
                for (int i = 0; i != curr.Length; ++i)
                {
                    if (curr[i] == '|') continue;
                    else if (curr[i] != 'ß') sbNorm.Append(curr[i]);
                    else
                    {
                        if (i < splitPos) ++splitPosNorm;
                        sbNorm.Append("ss");
                    }
                }
                string norm = sbNorm.ToString().ToLowerInvariant();
                //curr.Replace("ß", "ss");
                //string norm = curr.ToString().ToLowerInvariant();
                // If it's a number, surface form is NNN
                if (reNumbers.IsMatch(surf))
                {
                    norm = Token.Num;
                    splitPos = splitPosNorm = 0;
                }
                // Ze token
                return new Token(surf, norm, (ushort)(endPos - surf.Length), splitPos, splitPosNorm);
            }

            /// <summary>
            /// Strips diacritics from string. Used for forgiving prefix suggestions.
            /// </summary>
            /// <param name="word"></param>
            /// <returns></returns>
            public static string StripDiacritics(string word)
            {
                string decomp = word.Normalize(NormalizationForm.FormD);
                StringBuilder sb = new StringBuilder(word.Length);
                foreach (char c in decomp)
                {
                    UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);
                    if (uc != UnicodeCategory.NonSpacingMark) sb.Append(c);
                }
                return sb.ToString().Normalize(NormalizationForm.FormC);
            }

            /// <summary>
            /// Returns the word's stripped and lowercased 4-character prefix, encoded as long. First is highest order short.
            /// </summary>
            public static long Get4Prefix(string word)
            {
                word = word.ToLowerInvariant();
                string naked = StripDiacritics(word);
                long res = 0;
                for (int i = 0; i != 4; ++i)
                {
                    short s = (i < naked.Length ? ((short)naked[i]) : (short)0);
                    res <<= 16;
                    res += s;
                }
                return res;
            }
        }
    }
}
