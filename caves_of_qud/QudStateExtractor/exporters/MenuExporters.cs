// Mods/QudStateExtractor/exporters/MenuExporters.cs

/// <summary> 
/// Exports the Menu screen UI data to JSON and defines what is shown
/// in the export. This includes ability menu, equipment menu, skills & powers,
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;
using XRL.World.Conversations;
using XRL.World.Parts;
using XRL.World.Skills;
using Qud.API;
using Qud.UI;
using HarmonyLib;
using QudStateExtractor.Core;
using QudStateExtractor.Scrapers;
using static QudStateExtractor.Core.ReflectionHelpers;
using GO = XRL.World.GameObject;

namespace QudStateExtractor.Exporters
{
    public static class MenuExporters
    {
        public static void ExportAbilityMenu()
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null) return;

                var aa = player.GetPart<ActivatedAbilities>();
                var rows = new List<object>();

                if (aa != null)
                {
                    foreach (var kv in aa.AbilityByGuid)
                    {
                        var e = kv.Value;
                        rows.Add(new Dictionary<string, object>
                        {
                            ["id"] = e.ID.ToString(),
                            ["name"] = e.DisplayName,
                            ["cooldown"] = e.Cooldown,
                            ["class"] = e.Class,
                            ["command"] = e.Command,
                            ["toggleable"] = e.Toggleable,
                            ["toggleState"] = e.ToggleState,
                            ["activeToggle"] = e.ActiveToggle,
                            ["isAttack"] = e.IsAttack,
                            ["description"] = e.Description,
                        });
                    }
                }

                var bottom = BuildBottomFromDefaultMenuOptions("Qud.UI.AbilityManagerScreen");
                bottom = BottomBarScraper.KeepTextAndHotkeyOnly(bottom);

                var record = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["source"] = "AbilityManagerScreen",
                    ["data"] = rows,
                    ["bottom"] = bottom
                };

                ExportWriter.WriteJson("ABILITY_MENU", record, "Ability Menu");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportAbilityMenu failed: {ex.Message}");
            }
        }

        public static void ExportEquipmentMenu()
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null) return;

                var rows = new List<object>();
                var inventoryItems = player.GetInventoryAndEquipment();
                if (inventoryItems != null)
                {
                    foreach (var item in inventoryItems)
                    {
                        var slot = item.EquippedOn();
                        var goid = item.ID;
                        
                        Debug.Log($"[Narrator] Exporting item: name={item.DisplayName}, goid={goid}, bp={item.Blueprint}");
                        
                        rows.Add(new Dictionary<string, object>
                        {
                            ["id"] = "",
                            ["name"] = item.DisplayName,
                            ["equipped"] = slot != null,
                            ["slot"] = slot?.Type ?? "",
                            ["count"] = item.Count,
                            ["weight"] = item.GetStat("Weight")?.Value ?? item.Weight,
                            ["bp"] = item.Blueprint,
                            ["goid"] = goid
                        });
                    }
                }

                var record = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["source"] = "InventoryAndEquipmentStatusScreen",
                    ["data"] = rows,
                    ["bottom"] = new List<Dictionary<string, object>>()
                };

                ExportWriter.WriteJson("EQUIPMENT_SCREEN", record, "Equipment Menu");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportEquipmentMenu failed: {ex.Message}");
            }
        }

        public static void ExportSkillsAndPowers()
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null) return;

                SkillsAndPowersScreen.BuildNodes(player, true);

                var nodes = SkillsAndPowersScreen.Nodes;
                var data = new List<Dictionary<string, object>>();

                foreach (var n in nodes)
                {
                    if (n == null || !n.Visible) continue;

                    if (n.Skill != null)
                    {
                        var s = n.Skill;
                        data.Add(new Dictionary<string, object>
                        {
                            ["type"] = "skill",
                            ["name"] = s.Name,
                            ["class"] = s.Class,
                            ["cost"] = s.Cost,
                            ["owned"] = player.HasSkill(s.Class),
                            ["desc"] = n.Description ?? ""
                        });
                    }
                    else if (n.Power != null)
                    {
                        var p = n.Power;
                        data.Add(new Dictionary<string, object>
                        {
                            ["type"] = "power",
                            ["name"] = p.Name,
                            ["skill"] = p.ParentSkill?.Name ?? "",
                            ["class"] = p.Class,
                            ["cost"] = p.Cost,
                            ["owned"] = player.HasPart(p.Class),
                            ["requires"] = p.Requires ?? "",
                            ["exclusion"] = p.Exclusion ?? "",
                            ["desc"] = n.Description ?? ""
                        });
                    }
                }

                var record = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["sp"] = player.Stat("SP", 0),
                    ["data"] = data,
                    ["bottom"] = BottomBarScraper.ScrapeFromWindow(WindowScraper.GetCurrentWindow())
                };

                ExportWriter.WriteJson("SKILLS_POWERS", record, "Skills & Powers");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportSkillsAndPowers failed: {ex.Message}");
            }
        }

        public static void ExportStatusScreen()
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null) return;

                var stats = new Dictionary<string, object>();
                foreach (var kv in player.Statistics)
                    stats[kv.Key] = kv.Value?.Value ?? 0;

                var rows = new Dictionary<string, object>
                {
                    ["stats"] = stats,
                    ["lvl"] = player.Statistics.TryGetValue("Level", out var L) ? L?.Value : null,
                    ["xp"] = player.Statistics.TryGetValue("XP", out var X) ? X?.Value : null,
                    ["str"] = player.Statistics.TryGetValue("Strength", out var S) ? S?.Value : null,
                    ["agi"] = player.Statistics.TryGetValue("Agility", out var A) ? A?.Value : null,
                    ["tough"] = player.Statistics.TryGetValue("Toughness", out var T) ? T?.Value : null
                };

                // Scrape bottom from the active CharacterStatusScreen's horizNav
                var bottom = new List<Dictionary<string, object>>();
                var charStatusScreen = AccessTools.TypeByName("Qud.UI.CharacterStatusScreen");
                if (charStatusScreen != null)
                {
                    // Find the active instance
                    var activeScreens = UnityEngine.Object.FindObjectsOfType(charStatusScreen);
                    if (activeScreens != null && activeScreens.Length > 0)
                    {
                        var instance = activeScreens[0];
                        var horizNav = FP(instance, "horizNav");
                        if (horizNav != null)
                        {
                            var menuOpts = FP(horizNav, "menuOptionDescriptions") as System.Collections.IEnumerable;
                            bottom.AddRange(MenuItemHelpers.HarvestMenuItems(menuOpts));
                        }
                    }
                }

                var record = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["source"] = "StatusScreen",
                    ["data"] = rows,
                    ["bottom"] = new List<Dictionary<string, object>>() // Will be filled by patch
                };

                ExportWriter.WriteJson("STATUS_SCREEN", record, "Status Screen");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportStatusScreen failed: {ex.Message}");
            }
        }

        public static void ExportTinkeringMenu()
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null) return;

                var recipes = JournalAPI.RecipeNotes
                    .Where(r => r.Revealed)
                    .Select(r => new Dictionary<string, object>
                    {
                        ["id"] = "",  // Will be filled by merge
                        ["name"] = r.Recipe.GetDisplayName(),
                        ["ingredients"] = r.Recipe.GetIngredients(),
                        ["description"] = r.Recipe.GetDescription()
                    }).ToList();

                int bits = Convert.ToInt32(player.GetLongProperty("TinkeringBits", 0L));
                
                var record = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["source"] = "TinkeringScreen",
                    ["bits"] = bits,
                    ["data"] = recipes,
                    ["bottom"] = new List<Dictionary<string, object>>()
                };

                ExportWriter.WriteJson("TINKERING_SCREEN", record, "Tinkering Menu");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportTinkeringMenu failed: {ex.Message}");
            }
        }

        public static void ExportPickItem(IList<XRL.World.GameObject> items, string title)
        {
            try
            {
                var list = new List<Dictionary<string, object>>(items?.Count ?? 0);
                if (items != null)
                {
                    foreach (var it in items)
                    {
                        list.Add(new Dictionary<string, object>
                        {
                            ["id"] = "",  // Will be filled by merge
                            ["name"] = StringHelpers.StripQudMarkup(it?.DisplayName ?? "(unknown)"),
                            ["blueprint"] = it?.Blueprint ?? "",
                            ["weight"] = it?.Weight ?? 0,
                            ["count"] = it?.Count ?? 1,
                            ["takeable"] = it?.IsTakeable() ?? false,
                            ["owned_by_player"] = it?.OwnedByPlayer ?? false,
                            ["goid"] = it?.ID ?? ""
                        });
                    }
                }

                var record = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["source"] = "PickGameObjectScreen",
                    ["title"] = title ?? "",
                    ["data"] = list,
                    ["bottom"] = new List<Dictionary<string, object>>()
                };

                ExportWriter.WriteJson("PICK_ITEM", record, "Pick Item");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportPickItem failed: {ex.Message}");
            }
        }

        public static void ExportTradeScreen()
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null) return;

                var tradeScreenType = AccessTools.TypeByName("Qud.UI.TradeScreen");
                if (tradeScreenType == null) return;

                // Get the base class SingletonWindowBase<TradeScreen>
                var baseType = tradeScreenType.BaseType;
                if (baseType == null) return;

                // Get instance from the base class
                var instanceProp = AccessTools.Property(baseType, "instance");
                object tradeScreen = null;
                
                if (instanceProp == null)
                {
                    var instanceField = AccessTools.Field(baseType, "instance");
                    if (instanceField == null) return;
                    tradeScreen = instanceField.GetValue(null);
                }
                else
                {
                    tradeScreen = instanceProp.GetValue(null, null);
                }
                
                if (tradeScreen == null) return;

                var traderField = AccessTools.Field(tradeScreenType, "Trader");
                var trader = traderField?.GetValue(null) as GO;
                
                // Get cost multiple
                var costMultipleField = AccessTools.Field(tradeScreenType, "CostMultiple");
                var costMultiple = costMultipleField != null ? (float)costMultipleField.GetValue(null) : 1f;
                
                // Get the TradeUI.GetValue method
                var tradeUIType = AccessTools.TypeByName("XRL.UI.TradeUI");
                var getValueMethod = tradeUIType != null 
                    ? AccessTools.Method(tradeUIType, "GetValue", new[] { typeof(GO), typeof(bool?) })
                    : null;
                
                // Get trade entries
                var tradeEntries = FP(tradeScreen, "tradeEntries") as object[];
                
                // Export both sides of the trade screen
                var leftSide = new List<object>();
                var rightSide = new List<object>();
                
                // Left side (trader's items)
                if (tradeEntries?[0] is System.Collections.IEnumerable leftEntries)
                {
                    foreach (var entry in leftEntries)
                    {
                        var go = FP(entry, "GO") as GO;
                        if (go == null) continue;
                        
                        double value = 0;
                        if (getValueMethod != null)
                        {
                            try
                            {
                                var result = getValueMethod.Invoke(null, new object[] { go, true });
                                value = result != null ? Convert.ToDouble(result) : 0;
                            }
                            catch { }
                        }
                        
                        leftSide.Add(new Dictionary<string, object>
                        {
                            ["id"] = "",
                            ["name"] = go.DisplayName,
                            ["category"] = go.GetInventoryCategory(false),
                            ["value"] = value,
                            ["price"] = value * costMultiple,
                            ["weight"] = go.Weight,
                            ["count"] = go.Count,
                            ["blueprint"] = go.Blueprint,
                            ["goid"] = go.ID
                        });
                    }
                }
                
                // Right side (player's items)
                if (tradeEntries?[1] is System.Collections.IEnumerable rightEntries)
                {
                    foreach (var entry in rightEntries)
                    {
                        var go = FP(entry, "GO") as GO;
                        if (go == null) continue;
                        
                        double value = 0;
                        if (getValueMethod != null)
                        {
                            try
                            {
                                var result = getValueMethod.Invoke(null, new object[] { go, false });
                                value = result != null ? Convert.ToDouble(result) : 0;
                            }
                            catch { }
                        }
                        
                        rightSide.Add(new Dictionary<string, object>
                        {
                            ["id"] = "",
                            ["name"] = go.DisplayName,
                            ["category"] = go.GetInventoryCategory(false),
                            ["value"] = value,
                            ["price"] = value * costMultiple,
                            ["weight"] = go.Weight,
                            ["count"] = go.Count,
                            ["blueprint"] = go.Blueprint,
                            ["goid"] = go.ID
                        });
                    }
                }

                var record = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["source"] = "TradeScreen",
                    ["trader_name"] = trader?.DisplayName ?? "",
                    ["trader_drams"] = trader?.GetFreeDrams("water", null, null, null, false) ?? 0,
                    ["player_drams"] = player.GetFreeDrams("water", null, null, null, false),
                    ["cost_multiple"] = costMultiple,
                    ["left_side"] = leftSide,
                    ["right_side"] = rightSide,
                    ["bottom"] = new List<Dictionary<string, object>>()
                };

                ExportWriter.WriteJson("TRADE_SCREEN", record, "Trade Screen");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportTradeScreen failed: {ex.Message}");
            }
        }

        public static void ExportTradeCompletion(bool success, string result, List<object> itemsReceived, List<object> itemsGiven, int waterDifference = 0)
        {
            try
            {
                var player = XRLCore.Core?.Game?.Player?.Body;
                if (player == null) return;

                var record = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["source"] = "TradeCompletion",
                    ["success"] = success,
                    ["result"] = result,
                    ["water_difference"] = waterDifference, // positive = player paid, negative = player received
                    ["items_received"] = itemsReceived ?? new List<object>(),
                    ["items_given"] = itemsGiven ?? new List<object>(),
                    ["player_drams_after"] = player.GetFreeDrams("water", null, null, null, false)
                };

                ExportWriter.WriteJson("TRADE_COMPLETION", record, "Trade Completed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportTradeCompletion failed: {ex.Message}");
            }
        }

        public static void ExportNearbyItems()
        {
            try
            {
                var windowType = AccessTools.TypeByName("Qud.UI.NearbyItemsWindow");
                if (windowType == null) return;

                // Get the base class SingletonWindowBase<NearbyItemsWindow>
                var baseType = windowType.BaseType;
                if (baseType == null) return;

                // Get instance from the base class
                var instanceProp = AccessTools.Property(baseType, "instance");
                if (instanceProp == null)
                {
                    // Try getting it as a field instead
                    var instanceField = AccessTools.Field(baseType, "instance");
                    if (instanceField == null) return;
                    
                    var window = instanceField.GetValue(null);
                    if (window == null) return;

                    ExportNearbyItemsFromInstance(window);
                }
                else
                {
                    var window = instanceProp.GetValue(null, null);
                    if (window == null) return;

                    ExportNearbyItemsFromInstance(window);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportNearbyItems failed: {ex.Message}");
            }
        }

        private static void ExportNearbyItemsFromInstance(object window)
        {
            var items = new List<object>();
            
            // Get the _objects field and currentObjectCount
            var objectsField = AccessTools.Field(window.GetType(), "_objects");
            var countField = AccessTools.Field(window.GetType(), "currentObjectCount");
            
            if (objectsField != null && countField != null)
            {
                var objects = objectsField.GetValue(window) as System.Collections.IList;
                var count = (int)(countField.GetValue(window) ?? 0);
                
                if (objects != null)
                {
                    for (int i = 0; i < Math.Min(count, objects.Count); i++)
                    {
                        var item = objects[i];
                        if (item == null) continue;
                        
                        var go = FP(item, "go") as GO;
                        if (go == null) continue;
                        
                        items.Add(new Dictionary<string, object>
                        {
                            ["id"] = "",
                            ["name"] = go.DisplayName,
                            ["weight"] = go.Weight,
                            ["takeable"] = go.IsTakeable(),
                            ["direction"] = ReadString(item, "PrefixText") ?? "",
                            ["blueprint"] = go.Blueprint,
                            ["goid"] = go.ID
                        });
                    }
                }
            }

            var record = new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["source"] = "NearbyItemsWindow",
                ["data"] = items
            };

            ExportWriter.WriteJson("NEARBY_ITEMS", record, "Nearby Items");
        }

        private static List<Dictionary<string, object>> BuildBottomFromDefaultMenuOptions(string typeName)
        {
            var t = AccessTools.TypeByName(typeName);
            if (t == null) return new List<Dictionary<string, object>>();

            object list = AccessTools.Field(t, "defaultMenuOptions")?.GetValue(null)
                       ?? AccessTools.Property(t, "defaultMenuOptions")?.GetValue(null, null);
            if (list == null) return new List<Dictionary<string, object>>();

            var outList = new List<Dictionary<string, object>>();
            if (list is System.Collections.IEnumerable en)
            {
                foreach (var it in en)
                {
                    if (it == null) continue;

                    string text = TryGetString(it, "Description", "Text", "text", "label", "name");
                    string cmd = TryGetString(it, "InputCommand", "Command", "command", "id");
                    string hot = MenuItemHelpers.GetBindingDisplayForCommand(cmd);

                    outList.Add(new Dictionary<string, object> {
                        ["text"] = text ?? "",
                        ["hotkey"] = hot ?? ""
                    });
                }
            }
            return outList;
        }

        private static string TryGetString(object obj, params string[] names)
        {
            foreach (var n in names)
            {
                var f = AccessTools.Field(obj.GetType(), n);
                if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(obj);
                var p = AccessTools.Property(obj.GetType(), n);
                if (p != null && p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
                    return p.GetValue(obj, null) as string;
            }
            return obj.ToString();
        }
    }
}