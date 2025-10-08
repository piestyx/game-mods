// Mods/QudStateExtractor/patches/DialogueAndPopupPatches.cs

/// <summary> 
/// Contains the Harmony patches and events used for hooking into dialogue and 
/// popup message events in the game
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using XRL.Core;
using Qud.UI;
using QudStateExtractor.Core;
using QudStateExtractor.Exporters;
using QudStateExtractor.Scrapers;
using static QudStateExtractor.Core.ReflectionHelpers;

namespace QudStateExtractor.Patches
{
    public static class DialogueAndPopupPatches
    {
        [HarmonyPatch(typeof(PopupMessage), nameof(PopupMessage.Show))]
        public static class Patch_PopupMessage_Show
        {
            static string TryReadTextLike(object root, params string[] names)
            {
                foreach (var n in names)
                {
                    var v = FP(root, n);
                    if (v == null) continue;

                    var textProp = v.GetType().GetProperty("text",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (textProp != null && textProp.PropertyType == typeof(string))
                        return (string)textProp.GetValue(v, null);

                    if (v is string s) return s;
                }
                return "";
            }

            static Dictionary<string, object> MenuItemToDict(object item)
            {
                var d = new Dictionary<string, object>();
                d["text"] = ReadString(item, "text") ?? "";
                d["command"] = ReadString(item, "command") ?? "";
                d["hotkey"] = ReadString(item, "hotkey") ?? "";

                foreach (var f in item.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                    if (f.FieldType == typeof(string) && !d.ContainsKey(f.Name))
                        d[f.Name] = f.GetValue(item) as string ?? "";

                foreach (var p in item.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    if (p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0 && !d.ContainsKey(p.Name))
                        try { d[p.Name] = p.GetValue(item, null) as string ?? ""; } catch { }

                return d;
            }

            static List<Dictionary<string, object>> ScrapeAnyQudMenuItems(object root)
            {
                var outList = new List<Dictionary<string, object>>();
                if (root == null) return outList;

                var tQudMenuItem = AccessTools.TypeByName("Qud.UI.QudMenuItem")
                                ?? AccessTools.TypeByName("QudMenuItem");
                if (tQudMenuItem == null) return outList;

                bool IsQudMenuItem(Type x) => tQudMenuItem.IsAssignableFrom(x);

                void HarvestFromEnumerable(object col)
                {
                    if (col is System.Collections.IEnumerable e)
                        foreach (var it in e)
                            if (it != null && IsQudMenuItem(it.GetType()))
                                outList.Add(MenuItemToDict(it));
                }

                IEnumerable<object> EnumerateCandidateCollections(object host)
                {
                    var results = new List<object>();
                    if (host == null) return results;
                    var ht = host.GetType();

                    string[] names = { "items", "menuData", "bottomItems", "buttons", "contextMenu", "footer", "options" };
                    foreach (var n in names)
                    {
                        var v = FP(host, n);
                        if (v != null) results.Add(v);
                    }

                    var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                    foreach (var f in ht.GetFields(flags))
                        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(f.FieldType))
                            results.Add(f.GetValue(host));

                    foreach (var p in ht.GetProperties(flags))
                    {
                        if (p.GetIndexParameters().Length == 0 &&
                            typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType))
                        {
                            try { 
                                var val = p.GetValue(host, null);
                                if (val != null) results.Add(val);
                            } catch { }
                        }
                    }
                    
                    return results;
                }

                foreach (var col in EnumerateCandidateCollections(root))
                    HarvestFromEnumerable(col);

                return outList;
            }

            [HarmonyPostfix]
            public static void Postfix(PopupMessage __instance)
            {
                try
                {
                    if (XRLCore.Core?.Game?.Player?.Body == null) return;
                    
                    var cleanMessage = __instance.Message?.text ?? "";

                    string title = "";
                    {
                        var ctrl = __instance.controller;
                        title = TryReadTextLike(ctrl, "titleText", "title", "header");
                    }

                    string[] optionTexts = Array.Empty<string>();
                    {
                        var itemsObj = FP(__instance.controller, "items")
                                    ?? FP(__instance.controller, "menuData")
                                    ?? FP(__instance.controller, "options");
                        var items = ScrapeAnyQudMenuItems(itemsObj);
                        if (items.Count > 0)
                            optionTexts = items.ConvertAll(i => i.TryGetValue("text", out var v) ? (string)v : "").ToArray();
                    }

                    var bottom = new List<Dictionary<string, object>>();
                    {
                        var ctrl = __instance.controller;
                        object bottomCtrl =
                            FP(ctrl, "bottomContextController") ??
                            FP(ctrl, "contextController") ??
                            FP(ctrl, "contextMenu") ??
                            FP(ctrl, "bottom") ??
                            FP(ctrl, "footer") ??
                            FP(ctrl, "buttons") ??
                            FP(ctrl, "menuData") ??
                            FP(ctrl, "items");

                        var primary = ScrapeAnyQudMenuItems(bottomCtrl);
                        if (primary.Count > 0) bottom = primary;
                        else
                        {
                            var go = (__instance as UnityEngine.Component)?.gameObject;
                            if (go != null)
                            {
                                var comps = go.GetComponentsInChildren<UnityEngine.Component>(true);
                                foreach (var c in comps)
                                {
                                    var tn = c.GetType().Name;
                                    if (!(tn.Contains("Context") || tn.Contains("Menu") || tn.Contains("Button") || tn.Contains("Footer")))
                                        continue;

                                    var found = ScrapeAnyQudMenuItems(c);
                                    if (found.Count > 0) { bottom = found; break; }
                                }
                            }
                            if (bottom.Count == 0 && ctrl != null)
                            {
                                var found = ScrapeAnyQudMenuItems(ctrl);
                                if (found.Count > 0) bottom = found;
                            }
                        }
                    }

                    {
                        bottom = MenuItemHelpers.DedupMenuItems(bottom);
                        var slim = BottomBarScraper.KeepTextAndHotkeyOnly(bottom.Cast<object>());
                        
                        // ONLY export equipment bottom if this popup is from the equipment screen
                        if (__instance.GetType().FullName == "Qud.UI.InventoryAndEquipmentStatusScreen")
                        {
                            slim = BottomBarScraper.FilterEquipmentBottom(slim);
                            MergeHelpers.ExportEquipmentBottom(slim);
                        }
                    }

                    var record = new Dictionary<string, object>
                    {
                        { "timestamp", DateTime.UtcNow.ToString("o") },
                        { "source", "PopupMessage.Show" },
                        { "title", title ?? "" },
                        { "text", cleanMessage },
                        { "options", optionTexts ?? Array.Empty<string>() },
                        { "bottom", bottom?.ToArray() ?? Array.Empty<object>() }
                    };

                    ExportWriter.WriteJson("POPUP_FILE_PATH", record, "Popup");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator][ERROR] PopupMessage.Show logging failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch]
        public static class Patch_PickGO_BeforeShow
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.PickGameObjectScreen");
                return AccessTools.Method(t, "BeforeShow", new Type[] { typeof(bool) });
            }

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    // First export the main screen data
                    var titleField = AccessTools.Field(__instance.GetType(), "titleText");
                    var titleObj = titleField?.GetValue(__instance);
                    var title = titleObj != null ? ReadString(titleObj, "text") : "";
                    
                    var rawGameObjectsField = AccessTools.Field(__instance.GetType(), "rawGameObjects");
                    var rawGameObjects = rawGameObjectsField?.GetValue(__instance) as System.Collections.IEnumerable;
                    
                    if (rawGameObjects != null)
                    {
                        var items = new List<XRL.World.GameObject>();
                        foreach (var item in rawGameObjects)
                        {
                            if (item is XRL.World.GameObject go)
                                items.Add(go);
                        }
                        MenuExporters.ExportPickItem(items, title);
                    }
                    
                    // Capture quickkeys
                    var keys = QuickKeyScraper.ExtractFromPickGameObjectScreen(__instance);
                    if (keys != null && keys.Count > 0)
                        MergeHelpers.ExportPickItemQuickKeys(keys);

                    // Debug: Log what we're getting from hotkeyBar
                    var hotkeyBar = FP(__instance, "hotkeyBar");
                    Debug.Log($"[Narrator] DEBUG: hotkeyBar = {hotkeyBar}");
                    
                    if (hotkeyBar != null)
                    {
                        var choices = FP(hotkeyBar, "choices");
                        Debug.Log($"[Narrator] DEBUG: hotkeyBar.choices count = {(choices as System.Collections.IList)?.Count ?? -1}");
                        
                        var clones = FP(hotkeyBar, "selectionClones");
                        Debug.Log($"[Narrator] DEBUG: hotkeyBar.selectionClones count = {(clones as System.Collections.IList)?.Count ?? -1}");
                    }

                    // Try to get menu options directly
                    var bottom = BottomBarScraper.ScrapeFromWindow(__instance as UnityEngine.Component);
                    Debug.Log($"[Narrator] DEBUG: Scraped bottom bar items: {bottom?.Count ?? 0}");
                    
                    if (bottom != null && bottom.Count > 0)
                    {
                        foreach (var item in bottom)
                        {
                            var text = item.TryGetValue("text", out var t) ? t : "";
                            var cmd = item.TryGetValue("command", out var c) ? c : "";
                            Debug.Log($"[Narrator] DEBUG: Bottom item - text: '{text}', command: '{cmd}'");
                        }
                        MergeHelpers.AppendBottomToFile("PICK_ITEM", bottom);
                    }
                    else
                    {
                        Debug.Log("[Narrator] DEBUG: No bottom bar items found!");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] PickGO BeforeShow failed: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        [HarmonyPatch]
        public static class Patch_PickGO_UpdateView
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.PickGameObjectScreen");
                return AccessTools.Method(t, "UpdateViewFromData", new Type[] { typeof(bool) });
            }

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    // Re-export the main screen data on updates
                    var titleField = AccessTools.Field(__instance.GetType(), "titleText");
                    var titleObj = titleField?.GetValue(__instance);
                    var title = titleObj != null ? ReadString(titleObj, "text") : "";
                    
                    var rawGameObjectsField = AccessTools.Field(__instance.GetType(), "rawGameObjects");
                    var rawGameObjects = rawGameObjectsField?.GetValue(__instance) as System.Collections.IEnumerable;
                    
                    if (rawGameObjects != null)
                    {
                        var items = new List<XRL.World.GameObject>();
                        foreach (var item in rawGameObjects)
                        {
                            if (item is XRL.World.GameObject go)
                                items.Add(go);
                        }
                        MenuExporters.ExportPickItem(items, title);
                    }
                    
                    var keys = QuickKeyScraper.ExtractFromPickGameObjectScreen(__instance);
                    if (keys != null && keys.Count > 0)
                        MergeHelpers.ExportPickItemQuickKeys(keys);

                    var bottom = BottomBarScraper.ScrapeFromWindow(WindowScraper.GetCurrentWindow());
                    if (bottom != null && bottom.Count > 0)
                        MergeHelpers.AppendBottomToFile("PICK_ITEM", bottom);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] PickGO UpdateView failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch]
        public static class Patch_PickItem_ShowPicker_NoRef
        {
            static MethodBase TargetMethod() =>
                AccessTools.Method(typeof(XRL.UI.PickItem), "ShowPicker", new Type[] {
                    typeof(IList<XRL.World.GameObject>), typeof(string),
                    typeof(XRL.UI.PickItem.PickItemDialogStyle),
                    typeof(XRL.World.GameObject), typeof(XRL.World.GameObject),
                    typeof(XRL.World.Cell), typeof(string), typeof(bool),
                    typeof(Func<List<XRL.World.GameObject>>), typeof(bool),
                    typeof(bool), typeof(bool)
                });

            static void Prefix(IList<XRL.World.GameObject> Items, string Title)
            {
                MenuExporters.ExportPickItem(Items, Title);
            }
        }

        [HarmonyPatch]
        public static class Patch_PickItem_ShowPicker_WithRef
        {
            static MethodBase TargetMethod() =>
                AccessTools.Method(typeof(XRL.UI.PickItem), "ShowPicker", new Type[] {
                    typeof(IList<XRL.World.GameObject>),
                    typeof(bool).MakeByRefType(),
                    typeof(string),
                    typeof(XRL.UI.PickItem.PickItemDialogStyle),
                    typeof(XRL.World.GameObject), typeof(XRL.World.GameObject),
                    typeof(XRL.World.Cell), typeof(string), typeof(bool),
                    typeof(Func<List<XRL.World.GameObject>>), typeof(bool),
                    typeof(bool), typeof(bool)
                });

            static void Prefix(IList<XRL.World.GameObject> Items, string Title)
            {
                MenuExporters.ExportPickItem(Items, Title);
            }
        }
    }
}