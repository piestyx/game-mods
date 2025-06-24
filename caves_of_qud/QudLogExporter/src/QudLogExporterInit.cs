using HarmonyLib;
using UnityEngine;
using System.IO;
using System.Reflection;

namespace QudLogExporter
{
    [HarmonyPatch]
    public static class QudLogExporterInit
    {
        public static void OnModInit()
        {
            if (EnvHelper.IsVerbose())
            {
                Debug.Log("[Narrator] QudLogExporter mod loaded (verbose enabled)");
            }

            // Log DLL path and expected .env path
            string dllPath = Assembly.GetExecutingAssembly().Location;
            string envPath = Path.Combine(Path.GetDirectoryName(dllPath), ".env");

            if (File.Exists(envPath))
            {
                Debug.Log($"[Narrator] Found .env at: {envPath}");
            }
            else
            {
                Debug.LogWarning($"[Narrator] No .env file found at: {envPath}");
            }

            var harmony = new Harmony("QudLogExporter");
            harmony.PatchAll();
        }
    }
}
