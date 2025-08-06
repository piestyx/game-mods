using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

public static class EnvHelper
{
    private static readonly Dictionary<string, string> env = new Dictionary<string, string>();

    // Qud compiles mods into ~/.config/unity3d/.../ModAssemblies/
    // This breaks Assembly.GetExecutingAssembly().Location
    // so I hardcoded the path to the source mod folder instead.
    private static readonly string ModDir = Path.Combine(
        Environment.GetEnvironmentVariable("HOME") ?? "/tmp",
        ".config", "unity3d", "Freehold Games", "CavesOfQud", "Mods", "QudStateExtractor"
    );
    private static readonly string HomeDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

    static EnvHelper()
    {
        string path = Path.Combine(ModDir, ".env");

        if (File.Exists(path))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                string cleanLine = line.Split('#')[0]; // ← Strip inline comments
                if (string.IsNullOrWhiteSpace(cleanLine)) continue;

                var parts = cleanLine.Split(new[] { '=' }, 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                value = value.Replace("${HOME}", HomeDir).Replace("~", HomeDir);

                // Resolve other ${VAR} in value
                foreach (var kv in env)
                {
                    value = value.Replace($"${{{kv.Key}}}", kv.Value);
                }
                env[key] = value;
            }
        }
    }

    public static string GetEnv(string key, string fallback = "")
    {
        return env.TryGetValue(key, out var value) ? value : fallback;
    }

    public static string GetEnvPath(string key)
    {
        var raw = GetEnv(key, "").Trim();
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException($"[EnvHelper] Missing or empty env key: {key}");

        return raw.Replace("${HOME}", HomeDir).Replace("\\ ", " ");
    }

    public static bool IsVerbose()
    {
        return GetEnv("ENABLE_VERBOSE_LOGS", "false").ToLower() == "true";
    }
}
