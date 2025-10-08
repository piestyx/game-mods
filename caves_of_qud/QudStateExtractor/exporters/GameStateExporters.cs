// Mods/QudStateExtractor/exporters/GameStateExporters.cs

/// <summary> 
/// Exports the current Game State data to JSON and defines what is included
/// in the export. This includes agent state, world state, quests, journal,
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Qud.API;
using XRL;
using XRL.Core;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;
using XRL.World.Conversations;
using XRL.UI;
using QudStateExtractor.Core;
using QudStateExtractor.Scrapers;
using GO = XRL.World.GameObject;

namespace QudStateExtractor.Exporters
{
    public static class GameStateExporters
    {
        public static void ExportAgentState()
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
                var inventoryItems = player.GetInventoryAndEquipment();
                if (inventoryItems != null)
                {
                    foreach (var item in inventoryItems)
                    {
                        inventory.Add(new Dictionary<string, object>
                        {
                            { "name", item.DisplayName },
                            { "weight", item.GetStat("Weight")?.Value ?? item.Weight },
                            { "equipped", item.EquippedOn() != null },
                            { "equippedSlot", item.EquippedOn()?.Type ?? null }
                        });
                    }
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
                    { "time_ticks", XRLCore.Core?.Game?.TimeTicks ?? 0 }
                };

                var record = new Dictionary<string, object>
                {
                    { "timestamp", DateTime.UtcNow.ToString("o") },
                    { "data", agent }
                };

                ExportWriter.WriteJson("AGENT_FILE_PATH", record, "Agent State");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportAgentState failed: {ex.Message}");
            }
        }

        public static void ExportWorldState()
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                var zone = player?.CurrentZone;
                if (zone == null || player == null) return;

                var zoneInfo = new Dictionary<string, object>
                {
                    { "name", zone.DisplayName },
                    { "zone_id", zone.ZoneID },
                    { "position", new Dictionary<string, object> { { "x", zone.X }, { "y", zone.Y }, { "z", zone.Z } } }
                };

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

                var entitiesDetailed = new List<object>();
                var cosmeticCounts = new Dictionary<string, int>();

                foreach (var obj in zone.GetObjects(o => o != player))
                {
                    if (obj.CurrentCell == null || !obj.CurrentCell.IsVisible()) continue;

                    var hpStat = obj.Statistics.TryGetValue("Hitpoints", out var hp) ? hp : null;
                    bool hostile = obj.IsHostileTowards(player);

                    var tags = new List<string>();
                    if (!string.IsNullOrEmpty(obj.Blueprint))
                    {
                        var bp = GameObjectFactory.Factory.Blueprints[obj.Blueprint];
                        if (bp?.Tags != null)
                            tags = new List<string>(bp.Tags.Keys);
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
                        cosmeticCounts[name] = cosmeticCounts.TryGetValue(name, out var c) ? c + 1 : 1;
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

                ExportWriter.WriteJson("WORLD_FILE_PATH", record, "World State");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportWorldState failed: {ex.Message}");
            }
        }

        public static void ExportQuests()
        {
            try
            {
                var game = XRLCore.Core?.Game;
                if (game == null) return;

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

                ExportWriter.WriteJson("QUESTS_FILE_PATH", record, "Quests");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportQuests failed: {ex.Message}");
            }
        }

        public static void ExportJournal()
        {
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
                        .Select(o => new Dictionary<string, object> { ["text"] = o.Text }).ToList(),

                    ["general_notes"] = JournalAPI.GeneralNotes
                        .Where(n => n.Revealed)
                        .Select(o => new Dictionary<string, object> { ["text"] = o.Text }).ToList(),

                    ["sultan_notes"] = JournalAPI.SultanNotes
                        .Where(n => n.Revealed)
                        .Select(o => new Dictionary<string, object> { ["text"] = o.Text }).ToList(),

                    ["village_notes"] = JournalAPI.VillageNotes
                        .Where(n => n.Revealed)
                        .Select(o => new Dictionary<string, object> { ["text"] = o.Text }).ToList(),

                    ["map_notes"] = JournalAPI.MapNotes
                        .Where(n => n.Revealed)
                        .Select(o => new Dictionary<string, object> { ["text"] = o.Text }).ToList()
                };

                ExportWriter.WriteJson("JOURNAL_FILE_PATH", record, "Journal");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportJournal failed: {ex.Message}");
            }
        }

        public static void ExportDialogue()
        {
            try
            {
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

                var record = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["data"] = data,
                    ["choices"] = choices
                };

                ExportWriter.WriteJson("DIALOGUE_FILE_PATH", record, "Dialogue");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportDialogue failed: {ex.Message}");
            }
        }

        public static void ExportPointsOfInterest()
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                var zone = player?.CurrentZone;
                if (player == null || zone == null) return;

                var poiEvent = PooledEvent<GetPointsOfInterestEvent>.FromPool();
                poiEvent.Actor = player;
                poiEvent.Zone = zone;
                poiEvent.List.Clear();
                zone.HandleEvent(poiEvent);

                var pois = new List<object>();
                char key = 'a';

                foreach (var poi in poiEvent.List)
                {
                    var name = poi.GetDisplayName(player);
                    var location = poi.Location;

                    pois.Add(new Dictionary<string, object>
                    {
                        { "key", key.ToString() },
                        { "name", name },
                        { "x", location?.X ?? -1 },
                        { "y", location?.Y ?? -1 }
                    });

                    if (key < 'z') key++;
                }

                var record = new Dictionary<string, object>
                {
                    { "timestamp", DateTime.UtcNow.ToString("o") },
                    { "data", pois }
                };

                ExportWriter.WriteJson("POINTS_FILE_PATH", record, "Points of Interest");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportPointsOfInterest failed: {ex.Message}");
            }
        }
    }
}
