// Mods/QudStateExtractor/patches/StatusScreenPatches.cs

/// <summary> 
/// Contains the Harmony patches and events used for hooking into modern
/// UI status screens
/// </summary>

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Qud.UI;
using QudStateExtractor.Core;
using QudStateExtractor.Exporters;
using QudStateExtractor.Scrapers;
using static QudStateExtractor.Core.ReflectionHelpers;

namespace QudStateExtractor.Patches
{
    /// <summary>
    /// Handles the modern StatusScreensScreen hub that contains Skills/Status/Equipment/Tinkering tabs
    /// </summary>
    public static class StatusScreensHubPatch
    {
        [HarmonyPatch]
        public static class Patch_StatusScreensScreen_show
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Qud.UI.StatusScreensScreen");
                return AccessTools.Method(t, "show", new[] { typeof(int), typeof(XRL.World.GameObject) });
            }

            [HarmonyPrefix]
            static void Prefix(int __0 /*tab*/, XRL.World.GameObject __1)
            {
                try
                {
                    switch (__0)
                    {
                        case 0: MenuExporters.ExportSkillsAndPowers(); break; // k
                        case 1: MenuExporters.ExportStatusScreen(); break;    // tab (stats)
                        // case 2 (equipment): handled on actual open
                        case 3: MenuExporters.ExportTinkeringMenu(); break;   // n
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] StatusScreensScreen Prefix export failed: {ex.Message}");
                }
            }

            [HarmonyPostfix]
            static void Postfix(int __0 /*tab*/, XRL.World.GameObject __1)
            {
                try
                {
                    var bottom = BottomBarScraper.ScrapeFromWindow(WindowScraper.GetCurrentWindow());
                    if (bottom != null && bottom.Count > 0)
                    {
                        var cleaned = BottomBarScraper.KeepTextAndHotkeyOnly(bottom);
                        
                        // Route to correct file based on tab
                        switch (__0)
                        {
                            case 0: // Skills & Powers
                                cleaned = BottomBarScraper.FilterStatusBottom(cleaned);
                                if (cleaned.Count > 0)
                                    MergeHelpers.AppendBottomToFile("SKILLS_POWERS", cleaned);
                                break;
                                
                            case 3: // Tinkering
                                cleaned = BottomBarScraper.FilterStatusBottom(cleaned);
                                if (cleaned.Count > 0)
                                    MergeHelpers.AppendBottomToFile("TINKERING_SCREEN", cleaned);
                                break;
                        }
                    }

                    var keys = QuickKeyScraper.ExtractFromCurrentWindow();
                    if (keys != null && keys.Count > 0)
                    {
                        switch (__0)
                        {
                            case 0: MergeHelpers.AppendKeysToFile("SKILLS_POWERS", keys); break;
                            case 2: MergeHelpers.AppendKeysToFile("EQUIPMENT_SCREEN", keys); break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] StatusScreensScreen Postfix scrape failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(StatusScreensScreen), nameof(StatusScreensScreen.Update))]
        public static class Patch_StatusScreensScreen_Update
        {
            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                try
                {
                    if (!(bool)FP(FP(__instance, "navigationContext"), "IsActive"))
                        return;

                    var currentScreen = (int)(FP(__instance, "CurrentScreen") ?? -1);
                    var updateFlag = (bool)(FP(__instance, "updateMenuBar") ?? false);
                    
                    if (!updateFlag)
                    {
                        var bottom = BottomBarScraper.ScrapeFromWindow(WindowScraper.GetCurrentWindow());
                        if (bottom != null && bottom.Count > 0)
                        {
                            var cleaned = BottomBarScraper.KeepTextAndHotkeyOnly(bottom);
                            
                            // Route based on which screen is active
                            switch (currentScreen)
                            {
                                case 0: // Skills & Powers
                                    cleaned = BottomBarScraper.FilterStatusBottom(cleaned);
                                    if (cleaned.Count > 0)
                                        MergeHelpers.AppendBottomToFile("SKILLS_POWERS", cleaned);
                                    break;
                                    
                                case 3: // Tinkering
                                    cleaned = BottomBarScraper.FilterStatusBottom(cleaned);
                                    if (cleaned.Count > 0)
                                        MergeHelpers.AppendBottomToFile("TINKERING_SCREEN", cleaned);
                                    break;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(InventoryAndEquipmentStatusScreen), nameof(InventoryAndEquipmentStatusScreen.ShowScreen))]
        public static class Patch_InvEq_ShowScreen
        {
            [HarmonyPrefix]  // ADD THIS
            public static void Prefix()
            {
                if (XRL.Core.XRLCore.Core?.Game?.Player?.Body == null) return;
                MenuExporters.ExportEquipmentMenu();
            }
            
            [HarmonyPostfix]
            public static void Postfix(object __instance)
            {
                try
                {
                    if (XRL.Core.XRLCore.Core?.Game?.Player?.Body == null) return;
                    
                    // Capture bottom bar
                    var bottom = BottomBarScraper.ScrapeFromWindow(WindowScraper.GetCurrentWindow());
                    if (bottom != null && bottom.Count > 0)
                    {
                        var cleaned = BottomBarScraper.KeepTextAndHotkeyOnly(bottom);
                        cleaned = BottomBarScraper.FilterEquipmentBottom(cleaned);
                        if (cleaned.Count > 0)
                            MergeHelpers.ExportEquipmentBottom(cleaned);
                    }

                    // Capture hotkeys from BOTH scrollers
                    var allKeys = new List<Dictionary<string, object>>();
                    
                    // Inventory scroller (left pane)
                    var invScroller = FP(__instance, "inventoryController");
                    var invKeys = QuickKeyScraper.ExtractFromFrameworkScroller(invScroller);
                    if (invKeys != null && invKeys.Count > 0)
                        allKeys.AddRange(invKeys);
                    
                    // Equipment scroller (right pane) - check which mode is active
                    var equipMode = FP(__instance, "EquipmentMode") as string ?? "";
                    object eqScroller = null;
                    
                    if (equipMode == "List")
                    {
                        eqScroller = FP(__instance, "equipmentListController");
                    }
                    else // Paperdoll mode
                    {
                        eqScroller = FP(__instance, "equipmentPaperdollController");
                    }
                    
                    if (eqScroller != null)
                    {
                        var eqKeys = QuickKeyScraper.ExtractFromFrameworkScroller(eqScroller);
                        if (eqKeys != null && eqKeys.Count > 0)
                            allKeys.AddRange(eqKeys);
                    }
                    
                    if (allKeys.Count > 0)
                        MergeHelpers.ExportEquipmentQuickKeys(allKeys);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] Equip ShowScreen scrape failed: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(InventoryAndEquipmentStatusScreen), nameof(InventoryAndEquipmentStatusScreen.UpdateViewFromData))]
        public static class Patch_InvEq_UpdateView
        {
            [HarmonyPrefix]
            static void Prefix(object __instance)
            {
                try
                {
                    var statusScreen = FP(__instance, "statusScreensScreen");
                    if (statusScreen == null) return;
                    

                }
                catch { }
            }

            [HarmonyPostfix]
            public static void Postfix(object __instance)
            {
                try
                {
                    var allKeys = new List<Dictionary<string, object>>();
                    
                    // Capture from inventory scroller
                    var invScroller = FP(__instance, "inventoryController");
                    var invKeys = QuickKeyScraper.ExtractFromFrameworkScroller(invScroller);
                    if (invKeys != null && invKeys.Count > 0)
                        allKeys.AddRange(invKeys);

                    // Capture from equipment scroller (determine which is active)
                    var equipMode = FP(__instance, "EquipmentMode") as string ?? "";
                    object eqScroller = equipMode == "List" 
                        ? FP(__instance, "equipmentListController")
                        : FP(__instance, "equipmentPaperdollController");
                        
                    if (eqScroller != null)
                    {
                        var eqKeys = QuickKeyScraper.ExtractFromFrameworkScroller(eqScroller);
                        if (eqKeys != null && eqKeys.Count > 0)
                            allKeys.AddRange(eqKeys);
                    }
                    
                    if (allKeys.Count > 0)
                        MergeHelpers.ExportEquipmentQuickKeys(allKeys);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] Equip UpdateView scrape failed: {ex.Message}");
                }
            }
        }
    }
}