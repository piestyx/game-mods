// Mods/QudStateExtractor/exporters/MergeHelpers.cs

/// <summary> 
/// Helper functions and tasks for merging the UI specific data that is scraped
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using QudStateExtractor.Core;
using QudStateExtractor.Scrapers;
using static QudStateExtractor.Core.ReflectionHelpers;

namespace QudStateExtractor.Exporters
{
    public static class MergeHelpers
    {
        public static void ExportAbilityMenuHotkeys(List<Dictionary<string, object>> bindings)
        {
            try
            {
                ExportWriter.MergeAndWrite("ABILITY_MENU", root =>
                {
                    var cmdToQuick = new Dictionary<string, string>(StringComparer.Ordinal);
                    var nameToQuick = new Dictionary<string, string>(StringComparer.Ordinal);

                    if (bindings != null)
                    {
                        foreach (var b in bindings)
                        {
                            if (b == null) continue;
                            var quick = b.TryGetValue("quickKey", out var qv) ? qv as string : null;
                            if (string.IsNullOrEmpty(quick)) continue;

                            var cmd = b.TryGetValue("command", out var cv) ? cv as string : null;
                            if (!string.IsNullOrEmpty(cmd))
                                cmdToQuick[cmd] = quick;

                            var name = b.TryGetValue("name", out var nv) ? nv as string : null;
                            if (!string.IsNullOrEmpty(name))
                                nameToQuick[name] = quick;
                        }
                    }

                    if (root.TryGetValue("data", out var dataObj) && dataObj is List<object> dataList)
                    {
                        foreach (var rowObj in dataList)
                        {
                            if (rowObj is Dictionary<string, object> row)
                            {
                                string quick = null;

                                if (row.TryGetValue("command", out var rc) && rc is string rcmd && !string.IsNullOrEmpty(rcmd))
                                    cmdToQuick.TryGetValue(rcmd, out quick);

                                if (string.IsNullOrEmpty(quick) &&
                                    row.TryGetValue("name", out var rn) && rn is string rname && !string.IsNullOrEmpty(rname))
                                    nameToQuick.TryGetValue(rname, out quick);

                                row["id"] = quick ?? "";
                                row.Remove("command");
                                row.Remove("binding");
                            }
                        }
                    }

                    root.Remove("keys");
                    root["source"] = "AbilityManagerScreen";
                }, "Ability Menu (merged hotkeys)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportAbilityMenuHotkeys failed: {ex.Message}");
            }
        }

        public static void ExportEquipmentBottom(List<Dictionary<string, object>> bottom)
        {
            try
            {
                ExportWriter.MergeAndWrite("EQUIPMENT_SCREEN", root =>
                {
                    var slim = new List<Dictionary<string, object>>(bottom.Count);
                    foreach (var d in bottom)
                    {
                        if (d == null) continue;
                        var text = d.TryGetValue("text", out var tv) ? tv as string : "";
                        var hotkey = d.TryGetValue("hotkey", out var hv) ? hv as string : "";

                        if (string.IsNullOrEmpty(hotkey) &&
                            d.TryGetValue("command", out var cv) && cv is string cmd && !string.IsNullOrEmpty(cmd))
                        {
                            hotkey = MenuItemHelpers.GetBindingDisplayForCommand(cmd) ?? "";
                        }

                        if (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(hotkey))
                            slim.Add(new Dictionary<string, object> { ["text"] = text ?? "", ["hotkey"] = hotkey ?? "" });
                    }

                    slim = BottomBarScraper.FilterEquipmentBottom(slim);
                    root["bottom"] = slim;
                    root["source"] = "InventoryAndEquipmentStatusScreen";
                }, "Equipment bottom");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportEquipmentBottom failed: {ex.Message}");
            }
        }

        public static void ExportEquipmentQuickKeys(List<Dictionary<string, object>> keys)
        {
            try
            {
                ExportWriter.MergeAndWrite("EQUIPMENT_SCREEN", root =>
                {
                    root["source"] = "InventoryAndEquipmentStatusScreen";
                    if (!root.ContainsKey("data")) root["data"] = new List<object>();

                    root["keys"] = keys ?? new List<Dictionary<string, object>>();

                    var byGoid = new Dictionary<string, string>(StringComparer.Ordinal);
                    var byNameSlot = new Dictionary<(string name, string slot), string>();
                    var byBp = new Dictionary<string, string>(StringComparer.Ordinal);

                    foreach (var k in keys ?? new List<Dictionary<string, object>>())
                    {
                        if (k == null) continue;
                        var quick = k.TryGetValue("quickKey", out var qv) ? qv as string : null;
                        if (string.IsNullOrEmpty(quick)) continue;

                        Debug.Log($"[Narrator] Processing key: {quick}, goid={k.GetValueOrDefault("goid")}, name={k.GetValueOrDefault("name")}, bp={k.GetValueOrDefault("bp")}");

                        var goid = k.TryGetValue("goid", out var gv) ? gv as string : null;
                        if (!string.IsNullOrEmpty(goid)) byGoid[goid] = quick;

                        var name = k.TryGetValue("name", out var nv) ? nv as string : null;
                        var slot = k.TryGetValue("slot", out var sv) ? sv as string : "";
                        if (!string.IsNullOrEmpty(name)) byNameSlot[(name, slot ?? "")] = quick;

                        var bp = k.TryGetValue("bp", out var bv) ? bv as string : null;
                        if (!string.IsNullOrEmpty(bp)) byBp[bp] = quick;
                    }

                    if (root["data"] is List<object> rows)
                    {
                        foreach (var rowObj in rows)
                        {
                            if (rowObj is Dictionary<string, object> row)
                            {
                                string id = null;
                                
                                var rgoid = row.TryGetValue("goid", out var rg) ? rg : null;
                                Debug.Log($"[Narrator] Row goid type: {rgoid?.GetType().Name}, value: {rgoid}");
                                
                                if (rgoid is string rgoidStr && !string.IsNullOrEmpty(rgoidStr))
                                {
                                    byGoid.TryGetValue(rgoidStr, out id);
                                    Debug.Log($"[Narrator] Lookup result for {rgoidStr}: {id ?? "NOT FOUND"}");
                                }
                                
                                row["id"] = id ?? "";
                            }
                        }
                    }

                    root.Remove("keys");
                    // if (root["data"] is List<object> cleanRows)
                    // {
                    //     foreach (var rowObj in cleanRows)
                    //     {
                    //         if (rowObj is Dictionary<string, object> row)
                    //         {
                    //             row.Remove("bp");
                    //             row.Remove("goid");
                    //         }
                    //     }
                    // }

                    if (root.TryGetValue("bottom", out var btmObj) && btmObj is IEnumerable<object> btm)
                    {
                        var slim = BottomBarScraper.KeepTextAndHotkeyOnly(btm);
                        root["bottom"] = BottomBarScraper.FilterEquipmentBottom(slim);
                    }
                }, "Equipment Screen (merged keys)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportEquipmentQuickKeys failed: {ex.Message}");
            }
        }

        public static void ExportPickItemQuickKeys(List<Dictionary<string, object>> keys)
        {
            try
            {
                ExportWriter.MergeAndWrite("PICK_ITEM", root =>
                {
                    root["source"] = "PickGameObjectScreen";
                    if (!root.ContainsKey("data")) root["data"] = new List<object>();

                    var byGoid = new Dictionary<string, string>(StringComparer.Ordinal);
                    var byBp = new Dictionary<string, string>(StringComparer.Ordinal);
                    var byName = new Dictionary<string, string>(StringComparer.Ordinal);

                    foreach (var k in keys ?? new List<Dictionary<string, object>>())
                    {
                        if (k == null) continue;
                        var quick = k.TryGetValue("quickKey", out var qv) ? qv as string : null;
                        if (string.IsNullOrEmpty(quick)) continue;

                        var goid = k.TryGetValue("goid", out var gv) ? gv as string : null;
                        if (!string.IsNullOrEmpty(goid)) byGoid[goid] = quick;

                        var bp = k.TryGetValue("bp", out var bv) ? bv as string : null;
                        if (!string.IsNullOrEmpty(bp)) byBp[bp] = quick;

                        var name = k.TryGetValue("name", out var nv) ? nv as string : null;
                        if (!string.IsNullOrEmpty(name)) byName[name] = quick;
                    }

                    if (root["data"] is List<object> rows)
                    {
                        foreach (var rowObj in rows)
                        {
                            if (rowObj is Dictionary<string, object> row)
                            {
                                string id = null;

                                // Try GOID first (most reliable)
                                if (row.TryGetValue("goid", out var rg) && rg is string rgoid && !string.IsNullOrEmpty(rgoid))
                                    byGoid.TryGetValue(rgoid, out id);

                                // Try blueprint second
                                if (string.IsNullOrEmpty(id) && row.TryGetValue("blueprint", out var rb) && rb is string rbp && !string.IsNullOrEmpty(rbp))
                                    byBp.TryGetValue(rbp, out id);

                                // Try name as last resort
                                if (string.IsNullOrEmpty(id) && row.TryGetValue("name", out var rn) && rn is string rname && !string.IsNullOrEmpty(rname))
                                    byName.TryGetValue(rname, out id);

                                row["id"] = id ?? "";
                            }
                        }
                    }

                    if (root.TryGetValue("bottom", out var btmObj) && btmObj is IEnumerable<object> btm)
                    {
                        var slim = BottomBarScraper.KeepTextAndHotkeyOnly(btm);
                        root["bottom"] = slim;
                    }
                }, "PickItem Screen (merged keys)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportPickItemQuickKeys failed: {ex.Message}");
            }
        }

        public static void ExportTinkeringQuickKeys(List<Dictionary<string, object>> keys)
        {
            try
            {
                ExportWriter.MergeAndWrite("TINKERING_SCREEN", root =>
                {
                    root["source"] = "TinkeringScreen";
                    if (!root.ContainsKey("data")) root["data"] = new List<object>();

                    var byName = new Dictionary<string, string>(StringComparer.Ordinal);
                    var byBlueprint = new Dictionary<string, string>(StringComparer.Ordinal);

                    foreach (var k in keys ?? new List<Dictionary<string, object>>())
                    {
                        if (k == null) continue;
                        var quick = k.TryGetValue("quickKey", out var qv) ? qv as string : null;
                        if (string.IsNullOrEmpty(quick)) continue;

                        var name = k.TryGetValue("name", out var nv) ? nv as string : null;
                        if (!string.IsNullOrEmpty(name)) byName[name] = quick;

                        var bp = k.TryGetValue("blueprint", out var bv) ? bv as string : null;
                        if (!string.IsNullOrEmpty(bp)) byBlueprint[bp] = quick;
                    }

                    if (root["data"] is List<object> rows)
                    {
                        foreach (var rowObj in rows)
                        {
                            if (rowObj is Dictionary<string, object> row)
                            {
                                string id = null;

                                var name = row.TryGetValue("name", out var rn) ? rn as string : null;
                                if (!string.IsNullOrEmpty(name))
                                    byName.TryGetValue(name, out id);

                                if (string.IsNullOrEmpty(id))
                                {
                                    var bp = row.TryGetValue("blueprint", out var rb) ? rb as string : null;
                                    if (!string.IsNullOrEmpty(bp))
                                        byBlueprint.TryGetValue(bp, out id);
                                }

                                row["id"] = id ?? "";
                            }
                        }
                    }

                    if (root.TryGetValue("bottom", out var btmObj) && btmObj is IEnumerable<object> btm)
                    {
                        var slim = BottomBarScraper.KeepTextAndHotkeyOnly(btm);
                        root["bottom"] = BottomBarScraper.FilterStatusBottom(slim);
                    }
                }, "Tinkering Screen (merged keys)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportTinkeringQuickKeys failed: {ex.Message}");
            }
        }

        public static void ExportTradeQuickKeys(List<Dictionary<string, object>> keys, int side)
        {
            try
            {
                ExportWriter.MergeAndWrite("TRADE_SCREEN", root =>
                {
                    root["source"] = "TradeScreen";
                    
                    string sideKey = side == 0 ? "left_side" : "right_side";
                    if (!root.ContainsKey(sideKey)) root[sideKey] = new List<object>();

                    var byGoid = new Dictionary<string, string>(StringComparer.Ordinal);
                    var byBp = new Dictionary<string, string>(StringComparer.Ordinal);

                    foreach (var k in keys ?? new List<Dictionary<string, object>>())
                    {
                        if (k == null) continue;
                        var quick = k.TryGetValue("quickKey", out var qv) ? qv as string : null;
                        if (string.IsNullOrEmpty(quick)) continue;

                        var goid = k.TryGetValue("goid", out var gv) ? gv as string : null;
                        if (!string.IsNullOrEmpty(goid)) byGoid[goid] = quick;

                        var bp = k.TryGetValue("bp", out var bv) ? bv as string : null;
                        if (!string.IsNullOrEmpty(bp)) byBp[bp] = quick;
                    }

                    if (root[sideKey] is List<object> rows)
                    {
                        foreach (var rowObj in rows)
                        {
                            if (rowObj is Dictionary<string, object> row)
                            {
                                string id = null;

                                if (row.TryGetValue("goid", out var rg) && rg is string rgoid && !string.IsNullOrEmpty(rgoid))
                                    byGoid.TryGetValue(rgoid, out id);

                                if (string.IsNullOrEmpty(id) && row.TryGetValue("blueprint", out var rb) && rb is string rbp && !string.IsNullOrEmpty(rbp))
                                    byBp.TryGetValue(rbp, out id);

                                row["id"] = id ?? "";
                            }
                        }
                    }
                }, $"Trade Screen (merged keys - side {side})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] ExportTradeQuickKeys failed: {ex.Message}");
            }
        }

        public static void AppendKeysToFile(string envKey, List<Dictionary<string, object>> keys)
        {
            try
            {
                ExportWriter.MergeAndWrite(envKey, root =>
                {
                    if (!root.ContainsKey("data"))
                        root["data"] = new List<object>();

                    root["keys"] = keys ?? new List<Dictionary<string, object>>();
                }, $"Append keys -> {envKey}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Narrator] AppendKeysToFile({envKey}) failed: {ex.Message}");
            }
        }

        public static void AppendBottomToFile(string envKey, List<Dictionary<string, object>> bottom)
{
    try
    {
        ExportWriter.MergeAndWrite(envKey, root =>
        {
            if (envKey == "ABILITY_MENU" || envKey == "EQUIPMENT_SCREEN" || envKey == "STATUS_SCREEN" || envKey == "PICK_ITEM" || envKey == "TRADE_SCREEN")
            {
                var slim = BottomBarScraper.KeepTextAndHotkeyOnly(bottom);
                if (envKey == "EQUIPMENT_SCREEN")
                    slim = BottomBarScraper.FilterEquipmentBottom(slim);
                if (envKey == "STATUS_SCREEN")
                    slim = BottomBarScraper.FilterStatusBottom(slim);
                bottom = slim;
            }

            root["bottom"] = bottom ?? new List<Dictionary<string, object>>();
        }, $"Append bottom -> {envKey}");
    }
    catch (Exception ex)
    {
        Debug.LogError($"[Narrator] AppendBottomToFile({envKey}) failed: {ex.Message}");
    }
}
    }
}