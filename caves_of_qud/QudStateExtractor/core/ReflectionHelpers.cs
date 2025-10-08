// Mods/QudStateExtractor/core/ReflectionHelpers.cs

/// <summary> 
/// Shared Reflection helper tasks for scraping and indexing Qud data
/// </summary>

using System;
using System.Reflection;

namespace QudStateExtractor.Core
{
    public static class ReflectionHelpers
    {
        public static object GetFieldOrProp(object obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name)) return null;
            var t = obj.GetType();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            var f = t.GetField(name, F);
            if (f != null) return f.GetValue(obj);

            var p = t.GetProperty(name, F);
            if (p != null && p.GetIndexParameters().Length == 0)
                try { return p.GetValue(obj, null); } catch { }

            return null;
        }

        public static object FP(object o, string n) => GetFieldOrProp(o, n);

        public static string ReadString(object obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name)) return null;
            var t = obj.GetType();

            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(string))
                try { return f.GetValue(obj) as string; } catch { }

            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
                try { return p.GetValue(obj, null) as string; } catch { }

            return null;
        }

        public static string ReadStringCaseInsensitive(object obj, params string[] candidates)
        {
            if (obj == null) return null;
            var t = obj.GetType();

            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                foreach (var c in candidates)
                    if (string.Equals(c, p.Name, StringComparison.OrdinalIgnoreCase) && p.PropertyType == typeof(string))
                        try { return p.GetValue(obj, null) as string ?? ""; } catch { }
            }

            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var c in candidates)
                    if (string.Equals(c, f.Name, StringComparison.OrdinalIgnoreCase) && f.FieldType == typeof(string))
                        try { return f.GetValue(obj) as string ?? ""; } catch { }
            }

            return null;
        }

        public static bool? ReadBool(object o, string n)
        {
            var v = GetFieldOrProp(o, n);
            if (v is bool b) return b;
            return null;
        }

        public static string CallString(object host, string method, params object[] args)
        {
            if (host == null) return null;
            var mi = host.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return null;
            var r = mi.Invoke(host, args);
            return r?.ToString();
        }
    }
}