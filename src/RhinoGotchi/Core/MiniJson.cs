using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RhinoGotchi.Core
{
    /// <summary>
    /// Just enough JSON for one small API response, so the plugin needs no JSON package on .NET Framework.
    /// Objects become Dictionary&lt;string, object?&gt;, arrays List&lt;object?&gt;, numbers double.
    /// </summary>
    internal static class MiniJson
    {
        public static object? Parse(string json)
        {
            int i = 0;
            return Value(json, ref i);
        }

        public static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>Walks nested objects by key; null if any step is missing.</summary>
        public static object? Get(object? node, params string[] path)
        {
            foreach (var key in path)
            {
                if (!(node is Dictionary<string, object?> d) || !d.TryGetValue(key, out node))
                    return null;
            }
            return node;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object? Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end of JSON");
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object?>();
                i++;
                Skip(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Skip(s, ref i);
                    string key = String(s, ref i);
                    Skip(s, ref i);
                    if (s[i++] != ':') throw new FormatException("expected ':'");
                    d[key] = Value(s, ref i);
                    Skip(s, ref i);
                    char n = s[i++];
                    if (n == '}') return d;
                    if (n != ',') throw new FormatException("expected ',' or '}'");
                }
            }
            if (c == '[')
            {
                var list = new List<object?>();
                i++;
                Skip(s, ref i);
                if (s[i] == ']') { i++; return list; }
                while (true)
                {
                    list.Add(Value(s, ref i));
                    Skip(s, ref i);
                    char n = s[i++];
                    if (n == ']') return list;
                    if (n != ',') throw new FormatException("expected ',' or ']'");
                }
            }
            if (c == '"') return String(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }

            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new FormatException("unexpected character in JSON");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string String(string s, ref int i)
        {
            if (s[i++] != '"') throw new FormatException("expected a string");
            var sb = new StringBuilder();
            while (true)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
        }
    }
}
