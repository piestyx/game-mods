// Mods/QudStateExtractor/core/ExportWriters.cs

/// <summary> 
/// Contains methods and shared classes for all file I/O operations
/// </summary>

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using MiniJSON;

namespace QudStateExtractor.Core
{
    public static class ExportWriter
    {
        public static void WriteJson(string envKey, object record, string label = null)
        {
            var path = EnvHelper.GetEnvPath(envKey);
            var json = Json.Serialize(record);
            using (var writer = new StreamWriter(path, append: false))
                writer.WriteLine(json);

            if (EnvHelper.IsVerbose())
                Debug.Log($"[Narrator] {label ?? envKey} written to {path}");
        }

        public static Dictionary<string, object> LoadOrNew(string envKey)
        {
            var path = EnvHelper.GetEnvPath(envKey);
            try
            {
                if (File.Exists(path))
                {
                    var text = File.ReadAllText(path);
                    return Json.Deserialize(text) as Dictionary<string, object>;
                }
            }
            catch { }
            return new Dictionary<string, object>();
        }

        public static void MergeAndWrite(string envKey, Action<Dictionary<string, object>> mutator, string label = null)
        {
            var root = LoadOrNew(envKey);
            root["timestamp"] = DateTime.UtcNow.ToString("o");
            mutator(root);
            WriteJson(envKey, root, label);
        }
    }
}