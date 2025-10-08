// Mods/QudStateExtractor/core/EnvHelper.cs

/// <summary> 
/// File to read .env file and provide environment variable access
/// </summary>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace QudStateExtractor.Core
{
    public static class EnvHelper
    {
        private static Dictionary<string, string> _envVars;
        private static bool _initialized = false;

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                
                string dllPath = Assembly.GetExecutingAssembly().Location;
                string dllDir = Path.GetDirectoryName(dllPath);  // Gets ModAssemblies/
                string gameDir = Path.GetDirectoryName(dllDir);  // Gets CavesOfQud/
                string modsDir = Path.Combine(gameDir, "Mods");
                string modRoot = Path.Combine(modsDir, "QudStateExtractor");
                string envPath = Path.Combine(modRoot, ".env");

                Debug.Log($"[Narrator] DLL at: {dllPath}");
                Debug.Log($"[Narrator] Mod root: {modRoot}");
                Debug.Log($"[Narrator] Looking for .env at: {envPath}");

                if (!File.Exists(envPath))
                {
                    Debug.LogWarning($"[Narrator] .env file not found at: {envPath}");
                    return;
                }

                var lines = File.ReadAllLines(envPath);
                
                // First pass: load all raw values
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;

                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    _envVars[key] = value;
                }

                // Second pass: expand variables
                foreach (var key in new List<string>(_envVars.Keys))
                {
                    _envVars[key] = ExpandVariables(_envVars[key]);
                }

                Debug.Log($"[Narrator] Successfully loaded .env from: {envPath}");
                Debug.Log($"[Narrator] BASE_FILE_PATH resolved to: {GetEnvVar("BASE_FILE_PATH")}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] Failed to load .env: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static string ExpandVariables(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var regex = new Regex(@"\$\{([^}]+)\}");
            var result = value;

            var matches = regex.Matches(value);
            foreach (Match match in matches)
            {
                var varName = match.Groups[1].Value;
                if (_envVars.TryGetValue(varName, out var varValue))
                {
                    varValue = ExpandVariables(varValue);
                    result = result.Replace(match.Value, varValue);
                }
                else
                {
                    Debug.LogWarning($"[Narrator] Environment variable not found: {varName}");
                }
            }

            return result;
        }

        public static string GetEnvVar(string key, string defaultValue = "")
        {
            Initialize();
            return _envVars.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public static string GetEnvPath(string key)
        {
            Initialize();
            var path = GetEnvVar(key);
            
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"[Narrator] Environment variable '{key}' not found or empty in .env file");
                Debug.LogError($"[Narrator] Available keys: {string.Join(", ", _envVars.Keys)}");
                return "";
            }
            
            Debug.Log($"[Narrator] GetEnvPath({key}) = {path}");
            
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    Debug.Log($"[Narrator] Created directory: {dir}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] Failed to create directory {dir}: {ex.Message}");
                }
            }

            return path;
        }

        public static void DebugPrintAllVars()
        {
            Initialize();
            Debug.Log($"[Narrator] Loaded {_envVars.Count} environment variables:");
            foreach (var kvp in _envVars)
            {
                Debug.Log($"[Narrator]   {kvp.Key} = {kvp.Value}");
            }
        }

        public static bool IsVerbose()
        {
            Initialize();
            var verbose = GetEnvVar("ENABLE_VERBOSE_LOGS", "false");
            return verbose.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}