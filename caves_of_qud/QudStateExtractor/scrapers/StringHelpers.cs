// Mods/QudStateExtractor/scrapers/StringHelpers.cs

/// <summary> 
/// Methods that can be utilised to strip formatting from text during
/// export instead of leaving raw Qud markup in place and stripping with Python later
/// </summary>

using System;
using System.Text;

namespace QudStateExtractor.Scrapers
{
    public static class StringHelpers
    {
        public static string StripQudMarkup(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            int depth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (i + 1 < s.Length && s[i] == '{' && s[i + 1] == '{') { depth++; i++; continue; }
                if (i + 1 < s.Length && s[i] == '}' && s[i + 1] == '}') { depth = Math.Max(0, depth - 1); i++; continue; }
                if (depth == 0 && !char.IsControl(s[i])) sb.Append(s[i]);
            }
            return sb.ToString().Trim();
        }

        public static string StripQudMarkupKeepText(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (i + 1 < s.Length && s[i] == '{' && s[i + 1] == '{')
                {
                    int start = i + 2;
                    int end = s.IndexOf("}}", start, StringComparison.Ordinal);
                    if (end >= 0)
                    {
                        var inner = s.Substring(start, end - start);
                        int pipe = inner.LastIndexOf('|');
                        var text = pipe >= 0 ? inner.Substring(pipe + 1) : inner;
                        if (!string.IsNullOrEmpty(text))
                            sb.Append(text);
                        i = end + 1;
                        continue;
                    }
                }

                if (!char.IsControl(s[i]))
                    sb.Append(s[i]);
            }
            return sb.ToString().Trim();
        }
    }
}