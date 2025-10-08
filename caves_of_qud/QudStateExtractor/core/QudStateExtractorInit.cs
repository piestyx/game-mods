using HarmonyLib;
using UnityEngine;
using System.IO;
using System.Reflection;
using QudStateExtractor.Core;

namespace QudStateExtractor
{
    [HarmonyPatch]
    public static class QudStateExtractorInit
    {
        public static void OnModInit()
        {
            Debug.Log("[Narrator] ========== QudStateExtractor Initializing ==========");
            
            // Force EnvHelper initialization early
            var testPath = EnvHelper.GetEnvPath("AGENT_FILE_PATH");
            Debug.Log($"[Narrator] Test path resolution: AGENT_FILE_PATH = {testPath}");
            
            if (EnvHelper.IsVerbose())
            {
                EnvHelper.DebugPrintAllVars();
                Debug.Log("[Narrator] Verbose logging enabled");
            }

            // Log DLL path and expected .env path
            string dllPath = Assembly.GetExecutingAssembly().Location;
            string envPath = Path.Combine(Path.GetDirectoryName(dllPath), ".env");

            Debug.Log($"[Narrator] DLL location: {dllPath}");
            Debug.Log($"[Narrator] .env expected at: {envPath}");

            if (File.Exists(envPath))
            {
                Debug.Log($"[Narrator] Found .env file");
                // Read and log first few non-comment lines
                var lines = File.ReadAllLines(envPath);
                int count = 0;
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
                    {
                        Debug.Log($"[Narrator] .env line: {line}");
                        if (++count >= 3) break;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[Narrator] No .env file found at: {envPath}");
            }

            var harmony = new Harmony("QudStateExtractor");
            harmony.PatchAll();  // Don't assign - it returns void
            Debug.Log($"[Narrator] Harmony.PatchAll() completed");
            
            // TEST: Write a simple file to verify paths work
            try
            {
                var exportTestPath = EnvHelper.GetEnvPath("AGENT_FILE_PATH");
                File.WriteAllText(exportTestPath, $"{{\"test\":\"init at {System.DateTime.Now}\"}}\n");
                Debug.Log($"[Narrator] Test file written to: {exportTestPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Narrator] Test file write failed: {ex.Message}");
            }
            
            Debug.Log("[Narrator] ========== QudStateExtractor Init Complete ==========");
        }
    }
}