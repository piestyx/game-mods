using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using XRL;
using XRL.Core;
using XRL.World;
using MiniJSON;

namespace QudLogExporter
{
    [HarmonyPatch]
    public static class Patch_MessageQueue_AddPlayerMessage
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                "XRL.Messages.MessageQueue:AddPlayerMessage",
                new Type[] { typeof(string), typeof(string), typeof(bool) }
            );
        }

        static void Postfix(string Message, string Color, bool Capitalize)
        {
            try
            {
                string basePath = EnvHelper.GetEnvPath("BASE_FILE_PATH");
                string logPath = Path.Combine(basePath, "message_log.txt");
                string hpPath = Path.Combine(basePath, "hp_status.json");
                string effectsPath = Path.Combine(basePath, "status_effects.json");
                string inventoryPath = Path.Combine(basePath, "inventory.json");
                string zonePath = Path.Combine(basePath, "zone.json");

                Directory.CreateDirectory(basePath);

                var maxSizeStr = EnvHelper.GetEnv("LOG_FILE_MAX_SIZE", "0");
                if (int.TryParse(maxSizeStr, out int maxSize) && maxSize > 0 && File.Exists(logPath))
                {
                    long size = new FileInfo(logPath).Length;
                    if (size > maxSize)
                    {
                        File.WriteAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [System] Log reset due to file size.\n");
                        UnityEngine.Debug.Log($"[Narrator] Log file exceeded {maxSize} bytes and was reset.");
                    }
                }

                if (Message == "You died.")
                {
                    File.WriteAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [System] Log reset on player death.\n");
                    UnityEngine.Debug.Log("[Narrator] Log file reset due to death message.");
                    return;
                }

                using (StreamWriter writer = new StreamWriter(logPath, append: true))
                {
                    writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] {Message}");
                }

                ExportHPStatus(hpPath);
                ExportInventory(inventoryPath);
                ExportEffects(effectsPath);
                ExportZone(zonePath);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Logging failed: {ex.Message}");
            }
        }

        static void ExportHPStatus(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] Player object not found.");
                    return;
                }

                var stat = player.Statistics["Hitpoints"];
                int currentHP = stat?.Value ?? 0;
                int maxHP = stat?.BaseValue ?? 0;
                int penalty = stat?.Penalty ?? 0;

                var hp = new Dictionary<string, object>
                {
                    { "current", currentHP },
                    { "max", maxHP },
                    { "penalty", penalty }
                };

                string json = Json.Serialize(hp);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] HP export failed: {ex.Message}");
            }
        }

        static void ExportInventory(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] Player object not found.");
                    return;
                }

                var inventory = player.Inventory?.GetObjects();
                var items = new List<object>();

                if (inventory != null)
                {
                    foreach (var item in inventory)
                    {
                        string name = item.DisplayName;
                        int count = item.Count;
                        int weight = item.GetStat("Weight")?.Value ?? item.Weight;
                        bool equipped = item.EquippedOn() != null;

                        items.Add(new Dictionary<string, object>
                        {
                            { "name", name },
                            { "count", count },
                            { "weight", weight },
                            { "equipped", equipped }
                        });
                    }
                }

                string json = Json.Serialize(items);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Inventory export failed: {ex.Message}");
            }
        }

        static void ExportEffects(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] Player object not found.");
                    return;
                }

                var effects = player.Effects;
                var exported = new List<object>();

                foreach (var fx in effects)
                {
                    exported.Add(new Dictionary<string, object>
                    {
                        { "name", fx.DisplayNameStripped },
                        { "duration", fx.Duration },
                        { "description", fx.GetDescription() },
                        { "negative", fx.IsOfTypes(0x2000000) },
                        { "class", fx.ClassName }
                    });
                }

                string json = Json.Serialize(exported);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Effect export failed: {ex.Message}");
            }
        }

        static void ExportZone(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                var zone = player?.CurrentZone;
                if (zone == null) return;

                var zoneInfo = new Dictionary<string, object>
                {
                    { "name", zone.DisplayName },
                    { "zone_id", zone.ZoneID },
                    { "position", new Dictionary<string, object>
                        {
                            { "x", zone.X },
                            { "y", zone.Y },
                            { "z", zone.Z }
                        }
                    }
                };

                string json = Json.Serialize(zoneInfo);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Zone export failed: {ex.Message}");
            }
        }
    }
}

