using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using XRL;
using XRL.UI;
using XRL.Core;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Conversations;
using XRL.World.Parts.Mutation;
using Qud.API;
using MiniJSON;

namespace QudStateExtractor
{
    public static class QudStateExtractor
    {
        static void WriteJsonRecord(string path, object record, string label)
        {
            var json = Json.Serialize(record);
            
            using (StreamWriter writer = new StreamWriter(path, append: true))
                writer.WriteLine(json);

            if (EnvHelper.IsVerbose())
                Debug.Log($"[Narrator] {label} appended to {path}");
        }

        public static void ExportAgentState()
        {
            string path = EnvHelper.GetEnvPath("AGENT_FILE_PATH");

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

                var record = new Dictionary<string, object>
                {
                    { "timestamp", DateTime.UtcNow.ToString("o") },
                    { "data", agent }
                };

                WriteJsonRecord(path, record, "Agent State");

            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Logging failed: {ex.Message}");
            }
        }

        public static void ExportWorldState()
        {
            string path = EnvHelper.GetEnvPath("WORLD_FILE_PATH");

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

                    if (hostile || obj.Brain != null || obj.IsCombatObject())
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

                var record = new Dictionary<string, object>
                {
                    { "timestamp", DateTime.UtcNow.ToString("o") },
                    { "data", world }
                };

                WriteJsonRecord(path, record, "World State");

            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Logging failed: {ex.Message}");
            }
        }

        public static void ExportQuests()
        {
            string path = EnvHelper.GetEnvPath("QUESTS_FILE_PATH");

            try
            {
                var game = XRLCore.Core?.Game;
                if (game == null)
                {
                    UnityEngine.Debug.LogWarning("[Narrator] Game not found.");
                    return;
                }

                var active = new List<object>();
                var finished = new List<object>();

                foreach (var quest in game.Quests.Values)
                {
                    var steps = new List<object>();
                    foreach (var step in quest.StepsByID.Values.OrderBy(s => s.Ordinal))
                    {
                        if (step.Hidden) continue;

                        steps.Add(new Dictionary<string, object>
                            {
                                { "name", step.Name },
                                { "text", step.Text },
                                { "optional", step.Optional },
                                { "finished", step.Finished },
                                { "failed", step.Failed }
                            });
                    }

                    active.Add(new Dictionary<string, object>
                        {
                            { "id", quest.ID },
                            { "name", quest.DisplayName },
                            { "steps", steps }
                        });
                }

                foreach (var quest in game.FinishedQuests.Values)
                {
                    finished.Add(new Dictionary<string, object>
                        {
                            { "id", quest.ID },
                            { "name", quest.DisplayName }
                        });
                }

                var record = new Dictionary<string, object>
                    {
                        { "timestamp", DateTime.UtcNow.ToString("o") },
                        { "active", active },
                        { "finished", finished }
                    };

                WriteJsonRecord(path, record, "Quests");

            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Logging failed: {ex.Message}");
            }
        }

        public static void ExportJournal()
        {
            string path = EnvHelper.GetEnvPath("JOURNAL_FILE_PATH");

            try
            {
                var record = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["recipes"] = JournalAPI.RecipeNotes
                        .Where(r => r.Revealed)
                        .Select(r => new Dictionary<string, object>
                        {
                            ["name"] = r.Recipe.GetDisplayName(),
                            ["ingredients"] = r.Recipe.GetIngredients(),
                            ["description"] = r.Recipe.GetDescription()
                        }).ToList(),

                    ["observations"] = JournalAPI.Observations
                        .Where(o => o.Revealed)
                        .Select(o => new Dictionary<string, object> { ["text"] = o.Text })
                        .ToList(),

                    ["general_notes"] = JournalAPI.GeneralNotes
                        .Where(n => n.Revealed)
                        .Select(o => new Dictionary<string, object> { ["text"] = o.Text })
                        .ToList(),

                    ["sultan_notes"] = JournalAPI.SultanNotes
                        .Where(n => n.Revealed)
                        .Select(o => new Dictionary<string, object> { ["text"] = o.Text })
                        .ToList(),

                    ["village_notes"] = JournalAPI.VillageNotes
                        .Where(n => n.Revealed)
                        .Select(o => new Dictionary<string, object> { ["text"] = o.Text })
                        .ToList(),

                    ["map_notes"] = JournalAPI.MapNotes
                        .Where(n => n.Revealed)
                        .Select(o => new Dictionary<string, object> { ["text"] = o.Text })
                        .ToList()
                };

                WriteJsonRecord(path, record, "Journal");

            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Logging failed: {ex.Message}");
            }
        }
        
        public static void ExportDialogue()
        {
            string path = EnvHelper.GetEnvPath("DIALOGUE_FILE_PATH");
            
            try
            {
                string dialoguePath = EnvHelper.GetEnvPath("DIALOGUE_FILE_PATH");

                var data = new Dictionary<string, object>
                {
                    ["speaker"] = ConversationUI.Speaker?.DisplayName ?? "(unknown)",
                    ["listener"] = ConversationUI.Listener?.DisplayName ?? "(unknown)",
                    ["current_node"] = ConversationUI.CurrentNode?.ID ?? "(none)",
                    ["text"] = ConversationUI.CurrentNode?.GetDisplayText(true) ?? "(no text)",
                    ["last_choice"] = ConversationUI.LastChoice?.ID ?? "(no choice)"
                };

                var choices = new List<object>();
                if (ConversationUI.CurrentChoices != null)
                {
                    foreach (var choice in ConversationUI.CurrentChoices)
                    {
                        choices.Add(new Dictionary<string, object>
                        {
                            ["id"] = choice.ID,
                            ["text"] = choice.GetDisplayText(true),
                            ["selected"] = ConversationUI.CurrentChoices.IndexOf(choice) == ConversationUI.SelectedChoice
                        });
                    }
                }

                data["choices"] = choices;

                var record = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["data"] = data
                };

                WriteJsonRecord(path, record, "Dialogue");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Narrator] Logging failed: {ex.Message}");
            }
        }
    };
}

