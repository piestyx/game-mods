// Mods/QudStateExtractor/patches/MenuScreenPatches.cs

/// <summary> 
/// Contains the Harmony patches and events used for hooking into dialogue and 
/// popup message events in the game
/// </summary>


using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using XRL.UI;
using XRL.Core;
using Qud.UI;
using QudStateExtractor.Core;
using QudStateExtractor.Exporters;
using QudStateExtractor.Scrapers;
using static QudStateExtractor.Core.ReflectionHelpers;

namespace QudStateExtractor.Patches
{
    public static class MenuScreenPatches
    {
        // ===== Ability Manager =====
        [HarmonyPatch]
        public static class Patch_AbilityManager_BeforeShow
        {
            static MethodBase FindTarget()
            {
                var t = AccessTools.TypeByName("Qud.UI.AbilityManagerScreen");
                if (t == null) return null;

                var candidates = new (string name, Type[] sig)[]
                {
                    ("BeforeShow", new[] { typeof(bool) }),
                    ("BeforeShow", Type.EmptyTypes),
                    ("BeforeShowScreen", new[] { typeof(bool) }),
                    ("Prepare", Type.EmptyTypes),
                    ("ShowScreen", new[] { typeof(bool) }),
                };

                foreach (var (name, sig) in candidates)
                {
                    var m = AccessTools.Method(t, name, sig);
                    if (m != null) return m;
                }
                return null;
            }

            [HarmonyPrepare]
            public static bool Prepare() => FindTarget() != null;

            static MethodBase TargetMethod() => FindTarget();

            [HarmonyPostfix]
            static void Postfix()
            {
                try
                {
                    var keys = QuickKeyScraper.ExtractFromCurrentWindow();
                    if (keys != null && keys.Count > 0)
                        MergeHelpers.ExportAbilityMenuHotkeys(keys);
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(AbilityManager), nameof(AbilityManager.Show))]
        public static class Patch_AbilityManager_Show
        {
            [HarmonyPrefix] 
            static void Prefix() => MenuExporters.ExportAbilityMenu();

            [HarmonyPostfix]
            static void Postfix()
            {
                try
                {
                    var keys = QuickKeyScraper.ExtractFromCurrentWindow();
                    if (keys != null && keys.Count > 0)
                        MergeHelpers.ExportAbilityMenuHotkeys(keys);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] AbilityManager hotkey/bottom scrape failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch]
        public static class Patch_AbilityManager_ListUpdates
        {
            static readonly (string name, Type[] sig)[] CANDIDATES = new[]
            {
                ("Refresh", new[] { typeof(bool) }),
                ("BeforeShow", Type.EmptyTypes),
                ("UpdateViewFromData", new[] { typeof(bool) }),
            };

            static MethodBase _target;
            static MethodBase FindTarget()
            {
                var t = AccessTools.TypeByName("Qud.UI.AbilityManagerScreen");
                if (t == null) return null;
                foreach (var (name, sig) in CANDIDATES)
                {
                    var m = AccessTools.Method(t, name, sig);
                    if (m != null) return m;
                }
                return null;
            }

            [HarmonyPrepare]
            public static bool Prepare()
            {
                _target = FindTarget();
                return _target != null;
            }

            static MethodBase TargetMethod() => _target;

            [HarmonyPostfix]
            static void Postfix()
            {
                try
                {
                    var keys = QuickKeyScraper.ExtractFromCurrentWindow();
                    if (keys != null && keys.Count > 0)
                        MergeHelpers.AppendKeysToFile("ABILITY_MENU", keys);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] AbilityManager list updates scrape failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch]
        public static class Patch_AbilityManagerLine_Optional
        {
            static MethodBase _target;

            static MethodBase FindTarget()
            {
                var t = AccessTools.TypeByName("Qud.UI.AbilityManagerLine");
                if (t == null) return null;

                var candidates = new (string name, Type[] sig)[]
                {
                    ("UpdateHotkey", Type.EmptyTypes),
                    ("Refresh", Type.EmptyTypes),
                    ("Update", Type.EmptyTypes),
                    ("OnDataChanged", Type.EmptyTypes),
                };

                foreach (var (name, sig) in candidates)
                {
                    var m = AccessTools.Method(t, name, sig);
                    if (m != null) return m;
                }
                return null;
            }

            [HarmonyPrepare]
            public static bool Prepare()
            {
                _target = FindTarget();
                return _target != null;
            }

            static MethodBase TargetMethod() => _target;

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    var ctx = FP(__instance, "context");
                    var data = FP(ctx, "data");
                    if (data == null) return;

                    string quick = (FP(data, "quickKey") ?? "").ToString();
                    string hk = ReadString(data, "hotkeyDescription") ?? "";

                    if (string.IsNullOrEmpty(quick) && string.IsNullOrEmpty(hk))
                        return;

                    var ability = FP(data, "ability");
                    string name = ReadString(ability, "DisplayName")
                            ?? ReadString(data, "name") ?? "";
                    string cmd = ReadString(ability, "Command") ?? "";

                    var entry = new Dictionary<string, object> {
                        ["name"] = name,
                        ["command"] = cmd,
                        ["quickKey"] = quick,
                        ["hotkey"] = hk
                    };

                    MergeHelpers.ExportAbilityMenuHotkeys(
                        new List<Dictionary<string, object>> { entry }
                    );
                }
                catch { }
            }
        }

        // ===== Equipment Screen (Classic) =====
        [HarmonyPatch(typeof(EquipmentScreen), nameof(EquipmentScreen.Show))]
        public static class Patch_EquipmentScreen_Show
        {
            [HarmonyPrefix] 
            static void Prefix()
            {
                if (XRLCore.Core?.Game?.Player?.Body == null) return;
                MenuExporters.ExportEquipmentMenu();
            }
        
            [HarmonyPostfix]
            static void Postfix()
            {
                try
                {
                    var keys = QuickKeyScraper.ExtractFromCurrentWindow();
                    if (keys.Count > 0)
                        MergeHelpers.AppendKeysToFile("EQUIPMENT_SCREEN", keys);

                    var bottom = BottomBarScraper.ScrapeFromWindow(WindowScraper.GetCurrentWindow());
                    if (bottom != null && bottom.Count > 0)
                    {
                        var cleaned = BottomBarScraper.KeepTextAndHotkeyOnly(bottom);
                        cleaned = BottomBarScraper.FilterEquipmentBottom(cleaned);
                        if (cleaned.Count > 0)
                            MergeHelpers.ExportEquipmentBottom(cleaned);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] EquipmentScreen keys/bottom scrape failed: {ex.Message}");
                }
            }
        }

        // ===== Inventory/Equipment Line Hotkey Capture =====
        [HarmonyPatch]
        public static class Patch_InventoryLine_ScrollIndexChanged
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.InventoryLine");
                return t != null ? AccessTools.Method(t, "ScrollIndexChanged", new[] { typeof(int) }) : null;
            }

            [HarmonyPostfix]
            static void Postfix(object __instance, int index)
            {
                try
                {
                    // This fires when scroll position changes and hotkeys are assigned
                    // Immediately capture the hotkey after assignment
                    var context = FP(__instance, "context");
                    var data = FP(context, "data");
                    if (data == null) return;

                    var spread = FP(data, "spread");
                    if (spread == null) return;

                    // Get the hotkey character using the same method the game uses
                    var codeAtMethod = spread.GetType().GetMethod("codeAt", new[] { typeof(int) });
                    var charAtMethod = spread.GetType().GetMethod("charAt", new[] { typeof(int) });
                    
                    if (codeAtMethod == null || charAtMethod == null) return;

                    var keyCode = codeAtMethod.Invoke(spread, new object[] { index });
                    var keyChar = charAtMethod.Invoke(spread, new object[] { index });

                    if (keyCode == null || (int)keyCode == 0) return;

                    var go = FP(data, "go");
                    var name = ReadString(data, "name") ?? ReadString(data, "displayName") ?? "";
                    var bp = go != null ? ReadString(go, "Blueprint") ?? "" : "";
                    var goid = go != null ? ReadString(go, "id") ?? "" : "";  // ADD THIS

                    var entry = new Dictionary<string, object>
                    {
                        ["name"] = name,
                        ["bp"] = bp,
                        ["goid"] = goid,  // ADD THIS
                        ["quickKey"] = keyChar?.ToString() ?? "",
                        ["hotkey"] = ""
                    };

                    Debug.Log($"[Narrator] Captured inventory hotkey: {keyChar} for {name}");
                    MergeHelpers.ExportEquipmentQuickKeys(new List<Dictionary<string, object>> { entry });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] ScrollIndexChanged capture failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch]
        public static class Patch_EquipmentLine_ScrollIndexChanged
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.EquipmentLine");
                return t != null ? AccessTools.Method(t, "ScrollIndexChanged", new[] { typeof(int) }) : null;
            }

            [HarmonyPostfix]
            static void Postfix(object __instance, int index)
            {
                try
                {
                    var context = FP(__instance, "context");
                    var data = FP(context, "data");
                    if (data == null) return;

                    var spread = FP(data, "spread");
                    if (spread == null) return;

                    var codeAtMethod = spread.GetType().GetMethod("codeAt", new[] { typeof(int) });
                    var charAtMethod = spread.GetType().GetMethod("charAt", new[] { typeof(int) });
                    
                    if (codeAtMethod == null || charAtMethod == null) return;

                    var keyCode = codeAtMethod.Invoke(spread, new object[] { index });
                    var keyChar = charAtMethod.Invoke(spread, new object[] { index });

                    if (keyCode == null || (int)keyCode == 0) return;

                    var itemName = ReadString(data, "name") ?? "";
                    var bodyPart = FP(data, "bodyPart");
                    var slotName = bodyPart != null ? ReadString(bodyPart, "Name") ?? "" : "";
                    var go = FP(data, "go");
                    var bp = go != null ? ReadString(go, "Blueprint") ?? "" : "";
                    var goid = go != null ? ReadString(go, "id") ?? "" : "";

                    var entry = new Dictionary<string, object>
                    {
                        ["name"] = itemName,
                        ["slot"] = slotName,
                        ["bp"] = bp,
                        ["goid"] = goid,
                        ["quickKey"] = keyChar?.ToString() ?? "",
                        ["hotkey"] = ""
                    };

                    Debug.Log($"[Narrator] Captured equipment hotkey: {keyChar} for {itemName} in {slotName}");
                    MergeHelpers.ExportEquipmentQuickKeys(new List<Dictionary<string, object>> { entry });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] EquipmentLine ScrollIndexChanged failed: {ex.Message}");
                }
            }
        }
        
        [HarmonyPatch]
        public static class Patch_InventoryLine_UpdateHotkey
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.InventoryLine");
                return t != null ? AccessTools.Method(t, "UpdateHotkey", new Type[] { }) : null;
            }

            static char? TryCharAt(object spread, int idx)
            {
                if (spread == null) return null;
                var m = AccessTools.Method(spread.GetType(), "charAt", new[] { typeof(int) });
                if (m == null) return null;
                try
                {
                    var s = m.Invoke(spread, new object[] { idx }) as string;
                    return string.IsNullOrEmpty(s) ? (char?)null : s[0];
                }
                catch { return null; }
            }

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    var context = FP(__instance, "context");
                    var data = FP(context, "data");
                    var spread = FP(data, "spread");
                    var idxObj = FP(__instance, "scrollIndex");
                    int idx = idxObj is int i ? i : 0;

                    var quick = TryCharAt(spread, idx);
                    if (quick == null) return;

                    var itemName = ReadString(data, "name") ?? "";
                    var go = FP(data, "go");
                    var bp = ReadString(go, "Blueprint") ?? "";
                    var goid = ReadString(go, "id") ?? "";

                    var entry = new Dictionary<string, object>
                    {
                        ["name"] = itemName,
                        ["bp"] = bp,
                        ["goid"] = goid,
                        ["quickKey"] = quick.Value.ToString(),
                        ["hotkey"] = ""
                    };

                    MergeHelpers.ExportEquipmentQuickKeys(new List<Dictionary<string, object>> { entry });
                }
                catch { }
            }
        }

        [HarmonyPatch]
        public static class Patch_EquipmentLine_UpdateHotkey
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.EquipmentLine");
                return t != null ? AccessTools.Method(t, "UpdateHotkey", new Type[] { }) : null;
            }

            static char? TryCharAt(object spread, int idx)
            {
                if (spread == null) return null;
                var m = AccessTools.Method(spread.GetType(), "charAt", new[] { typeof(int) });
                if (m == null) return null;
                try
                {
                    var s = m.Invoke(spread, new object[] { idx }) as string;
                    return string.IsNullOrEmpty(s) ? (char?)null : s[0];
                }
                catch { return null; }
            }

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    var context = FP(__instance, "context");
                    var data = FP(context, "data");
                    var spread = FP(data, "spread");
                    var idxObj = FP(__instance, "scrollIndex");
                    int idx = idxObj is int i ? i : 0;

                    var quick = TryCharAt(spread, idx);
                    if (quick == null) return;

                    var itemName = ReadString(data, "name") ?? "";
                    var go = FP(data, "go");
                    var bp = ReadString(go, "Blueprint") ?? "";
                    var goid = ReadString(go, "id") ?? "";
                    var slotName = ReadString(data, "slot") ?? "";

                    var entry = new Dictionary<string, object>
                    {
                        ["name"] = itemName,
                        ["slot"] = slotName,
                        ["bp"] = bp,
                        ["goid"] = goid,
                        ["quickKey"] = quick.Value.ToString(),
                        ["hotkey"] = ""
                    };

                    MergeHelpers.ExportEquipmentQuickKeys(new List<Dictionary<string, object>> { entry });
                }
                catch { }
            }
        }

        // ===== Inventory/Equipment Line Additional Hotkey Capture =====
        [HarmonyPatch]
        public static class Patch_InventoryLine_SetData
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.InventoryLine");
                return t != null ? AccessTools.Method(t, "setData") : null;
            }

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    // Force UpdateHotkey call
                    var updateMethod = __instance.GetType().GetMethod("UpdateHotkey",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (updateMethod != null)
                        updateMethod.Invoke(__instance, null);
                }
                catch { }
            }
        }

        [HarmonyPatch]
        public static class Patch_EquipmentLine_SetData
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.EquipmentLine");
                return t != null ? AccessTools.Method(t, "setData") : null;
            }

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    // Force UpdateHotkey call
                    var updateMethod = __instance.GetType().GetMethod("UpdateHotkey",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (updateMethod != null)
                        updateMethod.Invoke(__instance, null);
                }
                catch { }
            }
        }

        // ===== Skills & Powers =====
        [HarmonyPatch(typeof(SkillsAndPowersScreen), nameof(SkillsAndPowersScreen.Show))]
        public static class Patch_SkillsAndPowersScreen_Show
        {
            [HarmonyPrefix] 
            static void Prefix() => MenuExporters.ExportSkillsAndPowers();

            [HarmonyPostfix]
            static void Postfix()
            {
                try
                {
                    var keys = QuickKeyScraper.ExtractFromCurrentWindow();
                    if (keys.Count > 0)
                        MergeHelpers.AppendKeysToFile("SKILLS_POWERS", keys);

                    var bottom = BottomBarScraper.ScrapeFromWindow(WindowScraper.GetCurrentWindow());
                    if (bottom.Count > 0)
                        MergeHelpers.AppendBottomToFile("SKILLS_POWERS", bottom);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] Skills&Powers keys/bottom scrape failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch]
        public static class Patch_SkillsPowers_ListUpdates
        {
            static MethodBase _target;
            static MethodBase FindTarget()
            {
                var t = AccessTools.TypeByName("Qud.UI.SkillsAndPowersStatusScreen");
                if (t == null) return null;
                return AccessTools.Method(t, "UpdateViewFromData", Type.EmptyTypes);
            }
            
            [HarmonyPrepare] 
            public static bool Prepare() 
            { 
                _target = FindTarget(); 
                return _target != null; 
            }
            
            static MethodBase TargetMethod() => _target;

            [HarmonyPostfix]
            static void Postfix()
            {
                try
                {
                    var keys = QuickKeyScraper.ExtractFromCurrentWindow();
                    if (keys != null && keys.Count > 0)
                        MergeHelpers.AppendKeysToFile("SKILLS_POWERS", keys);

                    var bottom = BottomBarScraper.ScrapeFromWindow(WindowScraper.GetCurrentWindow());
                    if (bottom != null && bottom.Count > 0)
                        MergeHelpers.AppendBottomToFile("SKILLS_POWERS", bottom);
                }
                catch { }
            }
        }

        // ===== Status Screen =====
        [HarmonyPatch(typeof(StatusScreen), nameof(StatusScreen.Show))]
        public static class Patch_StatusScreen_Show
        {
            [HarmonyPrefix] 
            static void Prefix() => MenuExporters.ExportStatusScreen();
        }

        [HarmonyPatch]
        public static class Patch_CharacterStatus_ListUpdates
        {
            static MethodBase _target;
            static MethodBase FindTarget()
            {
                var t = AccessTools.TypeByName("Qud.UI.CharacterStatusScreen");
                if (t == null) return null;
                return AccessTools.Method(t, "UpdateViewFromData", Type.EmptyTypes);
            }
            
            [HarmonyPrepare] 
            public static bool Prepare() 
            { 
                _target = FindTarget(); 
                return _target != null; 
            }
            
            static MethodBase TargetMethod() => _target;

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    var keys = QuickKeyScraper.ExtractFromCurrentWindow();
                    if (keys != null && keys.Count > 0)
                        MergeHelpers.AppendKeysToFile("STATUS_SCREEN", keys);

                    var bottom = BottomBarScraper.ScrapeFromWindow(WindowScraper.GetCurrentWindow());
                    if (bottom != null && bottom.Count > 0)
                        MergeHelpers.AppendBottomToFile("STATUS_SCREEN", bottom);
                }
                catch { }
            }
        }

        // ===== Character Screen =====
        [HarmonyPatch]
        public static class Patch_CharacterStatus_ShowScreen
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.CharacterStatusScreen");
                return t != null ? AccessTools.Method(t, "ShowScreen") : null;
            }
            
            [HarmonyPostfix]
            static void Postfix(object __instance, object __result, object parent)
            {
                try
                {
                    var bottom = new List<Dictionary<string, object>>();
                    
                    // Get defaultMenuOptionOrder from parent (StatusScreensScreen)
                    var defaults = FP(parent, "defaultMenuOptionOrder") as System.Collections.IEnumerable;
                    if (defaults != null)
                        bottom.AddRange(MenuItemHelpers.HarvestMenuItems(defaults));
                    
                    // Get screenGlobalContext menu options from parent
                    var globalCtx = FP(parent, "screenGlobalContext");
                    if (globalCtx != null)
                    {
                        var globalMenuOpts = FP(globalCtx, "menuOptionDescriptions") as System.Collections.IEnumerable;
                        if (globalMenuOpts != null)
                            bottom.AddRange(MenuItemHelpers.HarvestMenuItems(globalMenuOpts));
                    }
                    
                    // Deduplicate and clean
                    var cleaned = MenuItemHelpers.DedupMenuItems(bottom);
                    cleaned = BottomBarScraper.KeepTextAndHotkeyOnly(cleaned);
                    cleaned = BottomBarScraper.FilterStatusBottom(cleaned);
                    
                    if (cleaned.Count > 0)
                        MergeHelpers.AppendBottomToFile("STATUS_SCREEN", cleaned);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] CharacterStatus ShowScreen bottom scrape failed: {ex.Message}");
                }
            }
        }

        // ===== Tinkering =====
        [HarmonyPatch]
        public static class Patch_TinkeringScreen_Show_Target1
        {
            static MethodBase TargetMethod() =>
                AccessTools.Method(typeof(TinkeringScreen), "Show",
                    new Type[] { typeof(XRL.World.GameObject) });

            static void Prefix(XRL.World.GameObject GO) => MenuExporters.ExportTinkeringMenu();
        }

        [HarmonyPatch]
        public static class Patch_Tinkering_ListUpdates
        {
            static MethodBase _target;
            static MethodBase FindTarget()
            {
                var t = AccessTools.TypeByName("Qud.UI.TinkeringStatusScreen");
                if (t == null) return null;
                return AccessTools.Method(t, "UpdateViewFromData", Type.EmptyTypes);
            }
            
            [HarmonyPrepare] 
            public static bool Prepare() 
            { 
                _target = FindTarget(); 
                return _target != null; 
            }
            
            static MethodBase TargetMethod() => _target;

            [HarmonyPostfix]
            static void Postfix()
            {
                try
                {
                    var keys = QuickKeyScraper.ExtractFromCurrentWindow();
                    if (keys != null && keys.Count > 0)
                        MergeHelpers.AppendKeysToFile("TINKERING_SCREEN", keys);

                    var bottom = BottomBarScraper.ScrapeFromWindow(WindowScraper.GetCurrentWindow());
                    if (bottom != null && bottom.Count > 0)
                        MergeHelpers.AppendBottomToFile("TINKERING_SCREEN", bottom);
                }
                catch { }
            }
        }

        [HarmonyPatch]
        public static class Patch_TinkeringLine_UpdateHotkey
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.TinkeringLine");
                return t != null ? AccessTools.Method(t, "setData") : null;
            }

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    var context = FP(__instance, "context");
                    var data = FP(context, "data");
                    if (data == null) return;

                    // Skip category rows
                    var isCategory = ReadBool(data, "category") ?? false;
                    if (isCategory) return;

                    var tinkerData = FP(data, "data");
                    if (tinkerData == null) return;

                    var name = ReadString(tinkerData, "DisplayName") ?? "";
                    var bp = ReadString(tinkerData, "Blueprint") ?? "";
                    
                    // Get quick key from scroll index
                    var screen = FP(data, "screen");
                    var controller = FP(screen, "controller");
                    var scrollIdx = FP(__instance, "scrollIndex");
                    
                    if (scrollIdx is int idx)
                    {
                        // TinkeringLine doesn't have spread like inventory, 
                        // keys are assigned by position in visible list
                        char quickKey = (char)('a' + (idx % 26));
                        
                        var entry = new Dictionary<string, object>
                        {
                            ["name"] = name,
                            ["blueprint"] = bp,
                            ["quickKey"] = quickKey.ToString(),
                            ["hotkey"] = ""
                        };

                        MergeHelpers.ExportTinkeringQuickKeys(new List<Dictionary<string, object>> { entry });
                    }
                }
                catch { }
            }
        }

        // ===== Trade Screen =====
        [HarmonyPatch]
        public static class Patch_TradeScreen_Show
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.TradeScreen");
                return t != null ? AccessTools.Method(t, "showScreen") : null;
            }

            [HarmonyPrepare]
            public static bool Prepare() => TargetMethod() != null;

            // Remove Prefix - export AFTER data is loaded
            
            [HarmonyPostfix]
            static void Postfix()
            {
                try
                {
                    // Export after the trade screen has been set up
                    if (XRLCore.Core?.Game?.Player?.Body == null) return;
                    MenuExporters.ExportTradeScreen();
                    
                    var bottom = BottomBarScraper.ScrapeFromWindow(WindowScraper.GetCurrentWindow());
                    if (bottom != null && bottom.Count > 0)
                        MergeHelpers.AppendBottomToFile("TRADE_SCREEN", bottom);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] TradeScreen export failed: {ex.Message}");
                }
            }
        }

        // Also patch UpdateViewFromData to capture after refresh
        [HarmonyPatch]
        public static class Patch_TradeScreen_UpdateView
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.TradeScreen");
                return t != null ? AccessTools.Method(t, "UpdateViewFromData") : null;
            }

            [HarmonyPrepare]
            public static bool Prepare() => TargetMethod() != null;

            [HarmonyPostfix]
            static void Postfix()
            {
                try
                {
                    if (XRLCore.Core?.Game?.Player?.Body == null) return;
                    MenuExporters.ExportTradeScreen();
                    
                    var bottom = BottomBarScraper.ScrapeFromWindow(WindowScraper.GetCurrentWindow());
                    if (bottom != null && bottom.Count > 0)
                        MergeHelpers.AppendBottomToFile("TRADE_SCREEN", bottom);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] TradeScreen update export failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch]
        public static class Patch_TradeLine_SetData
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.TradeLine");
                return t != null ? AccessTools.Method(t, "setData") : null;
            }

            [HarmonyPrepare]
            public static bool Prepare() => TargetMethod() != null;

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    var context = FP(__instance, "context");
                    var data = FP(context, "data");
                    if (data == null) return;

                    // Get type - skip categories
                    var typeVal = FP(data, "type");
                    if (typeVal?.ToString().Contains("Category") == true) return;

                    var go = FP(data, "go") as XRL.World.GameObject;
                    if (go == null) return;

                    // Get the side (0 = trader, 1 = player)
                    var side = (int)(FP(data, "side") ?? -1);
                    if (side < 0) return;

                    // Get hotkey info
                    var quickKeyChar = FP(data, "quickKey");
                    var hotkeyDesc = ReadString(data, "hotkeyDescription") ?? "";
                    
                    string quickKey = "";
                    if (quickKeyChar is char c && c != '\0')
                        quickKey = c.ToString();

                    if (string.IsNullOrEmpty(quickKey) && string.IsNullOrEmpty(hotkeyDesc))
                        return;

                    var entry = new Dictionary<string, object>
                    {
                        ["name"] = go.DisplayName,
                        ["bp"] = go.Blueprint,
                        ["goid"] = go.ID,
                        ["quickKey"] = quickKey,
                        ["hotkey"] = hotkeyDesc
                    };

                    MergeHelpers.ExportTradeQuickKeys(new List<Dictionary<string, object>> { entry }, side);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] TradeLine setData capture failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch]
        public static class Patch_TradeUI_PerformOffer
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("XRL.UI.TradeUI");
                return t != null ? AccessTools.Method(t, "PerformOffer") : null;
            }

            [HarmonyPrepare]
            public static bool Prepare() => TargetMethod() != null;

            [HarmonyPostfix]
            static void Postfix(object __result, int Difference, bool forceComplete, object Trader, 
                object screenMode, object Objects, object NumberSelected)
            {
                try
                {
                    // __result is TradeUI.OfferStatus enum
                    // Get the enum value
                    int resultValue = Convert.ToInt32(__result);
                    
                    // 0 = NEXT (cancelled/failed)
                    // 1 = REFRESH 
                    // 2 = TOP (completed successfully)
                    // 3 = CLOSE
                    
                    // Only log successful trades (TOP = 2)
                    if (resultValue != 2) return;

                    var player = XRLCore.Core?.Game?.Player?.Body;
                    if (player == null) return;

                    var itemsReceived = new List<object>();
                    var itemsGiven = new List<object>();

                    // Objects is List<TradeEntry>[]
                    // NumberSelected is Int32[][]
                    if (Objects is Array objectsArray && NumberSelected is Array numberSelectedArray)
                    {
                        // Items received from trader (left side, index 0)
                        var leftObjects = objectsArray.GetValue(0) as System.Collections.IEnumerable;
                        var leftSelected = numberSelectedArray.GetValue(0) as int[];
                        
                        if (leftObjects != null && leftSelected != null)
                        {
                            int index = 0;
                            foreach (var entry in leftObjects)
                            {
                                if (index >= leftSelected.Length) break;
                                
                                var count = leftSelected[index];
                                if (count > 0)
                                {
                                    var go = FP(entry, "GO") as XRL.World.GameObject;
                                    if (go != null)
                                    {
                                        itemsReceived.Add(new Dictionary<string, object>
                                        {
                                            ["name"] = go.DisplayName,
                                            ["blueprint"] = go.Blueprint,
                                            ["count"] = count,
                                            ["goid"] = go.ID
                                        });
                                    }
                                }
                                index++;
                            }
                        }

                        // Items given to trader (right side, index 1)
                        var rightObjects = objectsArray.GetValue(1) as System.Collections.IEnumerable;
                        var rightSelected = numberSelectedArray.GetValue(1) as int[];
                        
                        if (rightObjects != null && rightSelected != null)
                        {
                            int index = 0;
                            foreach (var entry in rightObjects)
                            {
                                if (index >= rightSelected.Length) break;
                                
                                var count = rightSelected[index];
                                if (count > 0)
                                {
                                    var go = FP(entry, "GO") as XRL.World.GameObject;
                                    if (go != null)
                                    {
                                        itemsGiven.Add(new Dictionary<string, object>
                                        {
                                            ["name"] = go.DisplayName,
                                            ["blueprint"] = go.Blueprint,
                                            ["count"] = count,
                                            ["goid"] = go.ID
                                        });
                                    }
                                }
                                index++;
                            }
                        }
                    }

                    var traderObj = Trader as XRL.World.GameObject;
                    var waterDifference = Difference;

                    MenuExporters.ExportTradeCompletion(
                        true,
                        "Trade completed",
                        itemsReceived,
                        itemsGiven,
                        waterDifference
                    );
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] Trade completion capture failed: {ex.Message}");
                }
            }
        }

        // ===== Nearby Items =====
        [HarmonyPatch]
        public static class Patch_NearbyItems_Update
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.NearbyItemsWindow");
                return t != null ? AccessTools.Method(t, "UpdateGameContext") : null;
            }

            [HarmonyPrepare]
            public static bool Prepare() => TargetMethod() != null;

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    var visible = ReadBool(__instance, "Visible") ?? false;
                    if (visible)
                        MenuExporters.ExportNearbyItems();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] NearbyItems export failed: {ex.Message}");
                }
            }
        }
    }
}