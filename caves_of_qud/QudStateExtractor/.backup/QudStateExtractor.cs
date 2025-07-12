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

namespace QudStateExtractor
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
                string terrainPath = Path.Combine(basePath, "terrain.json");
                string entitiesPath = Path.Combine(basePath, "entities.json");
                string positionPath = Path.Combine(basePath, "position.json");
                string statsPath = Path.Combine(basePath, "stats.json");
                string mutationsPath = Path.Combine(basePath, "mutations.json");
                string abilitiesPath = Path.Combine(basePath, "abilities.json");
                string factionsPath = Path.Combine(basePath, "factions.json");
                string timePath = Path.Combine(basePath, "time.json");
                string weatherPath = Path.Combine(basePath, "weather.json");

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
                ExportTerrain(terrainPath);
                ExportEntities(entitiesPath);
                ExportPosition(positionPath);
                ExportStats(statsPath);
                ExportMutations(mutationsPath);
                ExportAbilities(abilitiesPath);
                ExportFactions(factionsPath);
                ExportTime(timePath);
                ExportWeather(weatherPath);
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

        static void ExportTerrain(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                var zone = player?.CurrentZone;
                if (zone == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] Zone not found.");
                    return;
                }

                var terrainObject = zone.GetTerrainObject();

                var terrainTags = new List<string>();
                if (terrainObject != null && terrainObject.Blueprint != null)
                {
                    var blueprint = GameObjectFactory.Factory.Blueprints[terrainObject.Blueprint];
                    if (blueprint?.Tags != null)
                        terrainTags = new List<string>(blueprint.Tags.Keys);
                }

                var terrain = new Dictionary<string, object>
                {
                    { "name", zone.GetTerrainDisplayName() ?? "" },
                    { "blueprint", terrainObject?.Blueprint ?? "" },
                    { "tags", terrainTags },
                    { "region", zone.GetTerrainRegion() ?? "" }
                };

                string json = Json.Serialize(terrain);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Terrain export failed: {ex.Message}");
            }
        }

        static void ExportEntities(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                var zone = player?.CurrentZone;
                if (zone == null || player == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] Zone or player not found.");
                    return;
                }

                var entities = new List<object>();

                foreach (var obj in zone.GetObjects(o => o != player))
                {
                    if (obj.CurrentCell == null || !obj.CurrentCell.IsVisible())
                        continue;
                    var hpStat = obj.Statistics.TryGetValue("Hitpoints", out var hp) ? hp : null;

                    var tags = new List<string>();
                    if (!string.IsNullOrEmpty(obj.Blueprint))
                    {
                        var blueprint = GameObjectFactory.Factory.Blueprints[obj.Blueprint];
                        if (blueprint?.Tags != null)
                            tags = new List<string>(blueprint.Tags.Keys);
                    }

                    entities.Add(new Dictionary<string, object>
                        {
                            { "name", obj.DisplayName },
                            { "hp", hpStat?.Value ?? 0 },
                            { "max_hp", hpStat?.BaseValue ?? 0 },
                            { "hostile", obj.IsHostileTowards(player) },
                            { "npc", !obj.IsHostileTowards(player) }, // simple heuristic
                            { "distance", player.DistanceTo(obj) },
                            { "direction", player.GetDirectionToward(obj)?.ToString().ToLower() ?? "" },
                            { "tags", tags }
                        });
                }

                string json = Json.Serialize(entities);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Entities export failed: {ex.Message}");
            }
        }

        static void ExportPosition(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                var cell = player?.CurrentCell;

                if (player == null || cell == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] Player or cell not found.");
                    return;
                }

                var pos = new Dictionary<string, object>
                {
                    { "x", cell.X },
                    { "y", cell.Y }
                };

                string json = Json.Serialize(pos);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Position export failed: {ex.Message}");
            }
        }

        static void ExportAll(string basePath)
        {
            try
            {
                string logPath = Path.Combine(basePath, "message_log.txt");
                string hpPath = Path.Combine(basePath, "hp_status.json");
                string effectsPath = Path.Combine(basePath, "status_effects.json");
                string inventoryPath = Path.Combine(basePath, "inventory.json");
                string zonePath = Path.Combine(basePath, "zone.json");
                string terrainPath = Path.Combine(basePath, "terrain.json");
                string entitiesPath = Path.Combine(basePath, "entities.json");
                string positionPath = Path.Combine(basePath, "position.json");

                Directory.CreateDirectory(basePath);

                if (File.Exists(logPath))
                    File.WriteAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [System] Log reset on export.\n");

                ExportHPStatus(hpPath);
                ExportInventory(inventoryPath);
                ExportEffects(effectsPath);
                ExportZone(zonePath);
                ExportTerrain(terrainPath);
                ExportEntities(entitiesPath);
                ExportPosition(positionPath);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Full export failed: {ex.Message}");
            }
        }
        static void ExportStats(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] Player not found.");
                    return;
                }

                var stats = new Dictionary<string, object>();

                foreach (var kvp in player.Statistics)
                {
                    stats[kvp.Key] = kvp.Value?.Value ?? 0;
                }

                string json = Json.Serialize(stats);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Stats export failed: {ex.Message}");
            }
        }

        static void ExportMutations(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] Player not found.");
                    return;
                }

                var mutationsPart = player.GetPart<XRL.World.Parts.Mutations>();
                if (mutationsPart == null || mutationsPart.ActiveMutationList == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] No active mutations found.");
                    return;
                }

                var mutations = new List<object>();

                foreach (var m in mutationsPart.ActiveMutationList)
                {
                    mutations.Add(new Dictionary<string, object>
                    {
                        { "name", m.DisplayName },
                        { "level", m.Level }
                    });
                }

                string json = Json.Serialize(mutations);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Mutations export failed: {ex.Message}");
            }
        }

        static void ExportAbilities(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] Player not found.");
                    return;
                }

                var abilitiesPart = player.GetPart<XRL.World.Parts.ActivatedAbilities>();
                if (abilitiesPart == null || abilitiesPart.AbilityByGuid == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] No activated abilities found.");
                    return;
                }

                var abilities = new List<object>();

                foreach (var kvp in abilitiesPart.AbilityByGuid)
                {
                    var ability = kvp.Value;

                    abilities.Add(new Dictionary<string, object>
                    {
                        { "name", ability.DisplayName },
                        { "cooldown", ability.Cooldown }
                    });
                }

                string json = Json.Serialize(abilities);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Abilities export failed: {ex.Message}");
            }
        }

        static void ExportFactions(string path)
        {
            try
            {
                var factions = new List<object>();

                foreach (var faction in Factions.Loop())
                {
                    if (!faction.Visible || faction.Name.Contains("villagers"))
                        continue;

                    factions.Add(new Dictionary<string, object>
                    {
                        { "id", faction.Name },
                        { "name", faction.DisplayName },
                        { "rep", Faction.PlayerReputation.Get(faction.Name) }
                    });
                }

                string json = Json.Serialize(factions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Factions export failed: {ex.Message}");
            }
        }

        static void ExportTime(string path)
        {
            try
            {
                var timeTicks = XRLCore.Core?.Game?.TimeTicks ?? 0;

                var time = new Dictionary<string, object>
                {
                    { "time_ticks", timeTicks }
                };

                string json = Json.Serialize(time);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Time export failed: {ex.Message}");
            }
        }
        
        static void ExportWeather(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                var zone = player?.CurrentZone;

                if (zone == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] Zone not found.");
                    return;
                }

                var weatherData = new Dictionary<string, object>
                {
                    { "has_weather", zone.HasWeather },
                    { "wind_speed", zone.WindSpeed },
                    { "wind_directions", zone.WindDirections },
                    { "wind_duration", zone.WindDuration },
                    { "current_wind_speed", zone.CurrentWindSpeed },
                    { "current_wind_direction", zone.CurrentWindDirection },
                    { "next_wind_change", zone.NextWindChange }
                };

                string json = Json.Serialize(weatherData);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Weather export failed: {ex.Message}");
            }
        }
    }
}

