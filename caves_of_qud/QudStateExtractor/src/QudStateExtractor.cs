using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using XRL;
using XRL.Core;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;
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
                string agentPath = EnvHelper.GetEnvPath("AGENT_FILE_PATH");
                string worldPath = EnvHelper.GetEnvPath("WORLD_FILE_PATH");

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

                ExportAgentState(agentPath);
                ExportWorldState(worldPath);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Logging failed: {ex.Message}");
            }
        }

        static void ExportAgentState(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null) return;

                var hpStat = player.Statistics["Hitpoints"];
                var hp = new Dictionary<string, object>
                {
                    { "current", hpStat?.Value ?? 0 },
                    { "max", hpStat?.BaseValue ?? 0 },
                    { "penalty", hpStat?.Penalty ?? 0 }
                };

                var inventory = new List<object>();
                foreach (XRL.World.GameObject item in player.Inventory?.GetObjects() ?? new List<XRL.World.GameObject>())
                {
                    inventory.Add(new Dictionary<string, object>
                    {
                        { "name", item.DisplayName },
                        { "count", item.Count },
                        { "weight", item.GetStat("Weight")?.Value ?? item.Weight },
                        { "equipped", item.EquippedOn() != null }
                    });
                }

                var effects = new List<object>();
                foreach (var fx in player.Effects)
                {
                    effects.Add(new Dictionary<string, object>
                    {
                        { "name", fx.DisplayNameStripped },
                        { "duration", fx.Duration },
                        { "description", fx.GetDescription() },
                        { "negative", fx.IsOfTypes(0x2000000) },
                        { "class", fx.ClassName }
                    });
                }

                var pos = new Dictionary<string, object>
                {
                    { "x", player.CurrentCell.X },
                    { "y", player.CurrentCell.Y }
                };

                var stats = new Dictionary<string, object>();
                foreach (var kvp in player.Statistics)
                    stats[kvp.Key] = kvp.Value?.Value ?? 0;

                var mutationsPart = player.GetPart<Mutations>();
                var mutations = new List<object>();
                foreach (var m in mutationsPart?.ActiveMutationList ?? new List<BaseMutation>())
                {
                    mutations.Add(new Dictionary<string, object>
                    {
                        { "name", m.GetDisplayName() },
                        { "level", m.Level }
                    });
                }

                var abilitiesPart = player.GetPart<ActivatedAbilities>();
                var abilities = new List<object>();
                foreach (var kvp in abilitiesPart?.AbilityByGuid ?? new Dictionary<Guid, ActivatedAbilityEntry>())
                {
                    abilities.Add(new Dictionary<string, object>
                    {
                        { "name", kvp.Value.DisplayName },
                        { "cooldown", kvp.Value.Cooldown }
                    });
                }

                var factions = new List<object>();
                foreach (var faction in Factions.Loop())
                {
                    if (!faction.Visible || faction.Name.Contains("villagers")) continue;
                    factions.Add(new Dictionary<string, object>
                    {
                        { "id", faction.Name },
                        { "name", faction.DisplayName },
                        { "rep", Faction.PlayerReputation.Get(faction.Name) }
                    });
                }

                var time = XRLCore.Core?.Game?.TimeTicks ?? 0;

                var agent = new Dictionary<string, object>
                {
                    { "hp", hp },
                    { "inventory", inventory },
                    { "status_effects", effects },
                    { "position", pos },
                    { "stats", stats },
                    { "mutations", mutations },
                    { "abilities", abilities },
                    { "factions", factions },
                    { "time_ticks", time }
                };

                File.WriteAllText(path, Json.Serialize(agent));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Agent state export failed: {ex.Message}");
            }
        }

        static void ExportWorldState(string path)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                var zone = player?.CurrentZone;
                if (zone == null || player == null) return;

                // Zone
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

                // Terrain
                var terrainObj = zone.GetTerrainObject();
                var terrainTags = new List<string>();
                if (terrainObj != null && terrainObj.Blueprint != null)
                {
                    var blueprint = GameObjectFactory.Factory.Blueprints[terrainObj.Blueprint];
                    if (blueprint?.Tags != null)
                        terrainTags = new List<string>(blueprint.Tags.Keys);
                }

                var terrain = new Dictionary<string, object>
                {
                    { "name", zone.GetTerrainDisplayName() ?? "" },
                    { "blueprint", terrainObj?.Blueprint ?? "" },
                    { "tags", terrainTags },
                    { "region", zone.GetTerrainRegion() ?? "" }
                };

                // Entities
                var entitiesDetailed = new List<object>();
                var cosmeticCounts = new Dictionary<string, int>();

                foreach (XRL.World.GameObject obj in zone.GetObjects(o => o != player))
                {
                    if (obj.CurrentCell == null || !obj.CurrentCell.IsVisible()) continue;

                    var hpStat = obj.Statistics.TryGetValue("Hitpoints", out var hp) ? hp : null;
                    bool hostile = obj.IsHostileTowards(player);

                    var tags = new List<string>();
                    if (!string.IsNullOrEmpty(obj.Blueprint))
                    {
                        var blueprint = GameObjectFactory.Factory.Blueprints[obj.Blueprint];
                        if (blueprint?.Tags != null)
                            tags = new List<string>(blueprint.Tags.Keys);
                    }

                    if (hostile || obj.pBrain != null || obj.IsCombatObject())
                    {
                        entitiesDetailed.Add(new Dictionary<string, object>
                        {
                            { "name", obj.DisplayName },
                            { "hp", hpStat?.Value ?? 0 },
                            { "max_hp", hpStat?.BaseValue ?? 0 },
                            { "hostile", hostile },
                            { "npc", !hostile },
                            { "distance", player.DistanceTo(obj) },
                            { "direction", player.GetDirectionToward(obj)?.ToString().ToLower() ?? "" },
                            { "tags", tags }
                        });
                    }
                    else
                    {
                        string name = obj.DisplayName;
                        if (!cosmeticCounts.ContainsKey(name))
                            cosmeticCounts[name] = 0;
                        cosmeticCounts[name]++;
                    }
                }

                var groupedCosmetics = new List<object>();
                foreach (var kvp in cosmeticCounts)
                {
                    groupedCosmetics.Add(new Dictionary<string, object>
                    {
                        { "name", kvp.Key },
                        { "count", kvp.Value }
                    });
                }

                var world = new Dictionary<string, object>
                {
                    { "zone", zoneInfo },
                    { "terrain", terrain },
                    { "entities", entitiesDetailed },
                    { "cosmetic_entities", groupedCosmetics },
                    { "weather", new Dictionary<string, object>
                        {
                            { "has_weather", zone.HasWeather },
                            { "wind_speed", zone.WindSpeed },
                            { "wind_directions", zone.WindDirections },
                            { "wind_duration", zone.WindDuration },
                            { "current_wind_speed", zone.CurrentWindSpeed },
                            { "current_wind_direction", zone.CurrentWindDirection },
                            { "next_wind_change", zone.NextWindChange }
                        }
                    }
                };

                File.WriteAllText(path, Json.Serialize(world));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] World state export failed: {ex.Message}");
            }
        }
    }
}
