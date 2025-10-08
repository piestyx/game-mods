// Mods/QudStateExtractor/scrapers/BottomBarScraper.cs

/// <summary> 
/// Scrapes the bottom bar menu options from various UI windows
/// and returns them as a list of dictionaries with "text" and "hotkey" keys
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using QudStateExtractor.Core;
using static QudStateExtractor.Core.ReflectionHelpers;

namespace QudStateExtractor.Scrapers
{
    public static class BottomBarScraper
    {
        public static List<Dictionary<string, object>> ScrapeFromWindow(UnityEngine.Component window)
        {
            var bottom = new List<Dictionary<string, object>>();
            if (window == null) return bottom;

            var winType = window.GetType().FullName;

            if (winType == "Qud.UI.AbilityManagerScreen")
                bottom.AddRange(ScrapeAbilityManagerBottom(window));
            else if (winType == "Qud.UI.CharacterStatusScreen")
                bottom.AddRange(ScrapeCharacterStatusBottom(window));
            else if (winType == "Qud.UI.StatusScreensScreen")
                bottom.AddRange(ScrapeStatusScreensHubBottom(window));
            else if (winType == "Qud.UI.PickGameObjectScreen")  // ADD THIS
                bottom.AddRange(ScrapePickGameObjectBottom(window));
            else if (winType == "Qud.UI.TradeScreen")  // ADD THIS TOO
                bottom.AddRange(ScrapeTradeScreenBottom(window));
            else
            {
                var parent = FP(window, "statusScreensScreen");
                if (parent != null && parent.GetType().FullName == "Qud.UI.StatusScreensScreen")
                    bottom.AddRange(ScrapeStatusScreensHubBottom(parent));
            }

            bottom.AddRange(ScrapeGenericBottom(window));
            
            var deduped = MenuItemHelpers.DedupMenuItems(bottom);
            
            if (winType == "Qud.UI.AbilityManagerScreen")
                return ReduceToTextHotkeyOnly(deduped);

            return deduped;
        }

        private static List<Dictionary<string, object>> ScrapeGenericBottom(UnityEngine.Component window)
        {
            var bottom = new List<Dictionary<string, object>>();
            
            var fallbackScroller = FP(window, "menuOptionScroller") ?? FP(window, "hotkeyBar");
            bottom.AddRange(HarvestMenuOptionsFromScroller(fallbackScroller));

            var candidates = new[] {
                "vertNav", "horizNav", "controller", "bitsController",
                "categoryBar", "filterBar", "leftNav", "rightNav", "nav", "footerBar"
            };

            foreach (var name in candidates)
            {
                var field = FP(window, name);
                if (field == null) continue;

                if (field.GetType().Name.Contains("NavigationContext"))
                    HarvestBottomFromNavContext(field, bottom);

                var scrollContext = FP(field, "scrollContext");
                if (scrollContext != null)
                    HarvestBottomFromNavContext(scrollContext, bottom);

                var mods = FP(field, "menuOptionDescriptions");
                MenuItemHelpers.HarvestMenuItems(mods, bottom);
            }

            return bottom;
        }

        private static List<Dictionary<string, object>> ScrapeAbilityManagerBottom(UnityEngine.Component window)
        {
            var bottom = new List<Dictionary<string, object>>();
            
            var hotkeyBar = FP(window, "hotkeyBar");
            bottom.AddRange(HarvestMenuOptionsFromScroller(hotkeyBar));
            
            if (bottom.Count == 0)
            {
                var defaults = FP(window, "defaultMenuOptions") as System.Collections.IEnumerable;
                bottom.AddRange(MenuItemHelpers.HarvestMenuItems(defaults));
            }
            
            return bottom;
        }

        private static List<Dictionary<string, object>> ScrapeCharacterStatusBottom(UnityEngine.Component window)
        {
            var bottom = new List<Dictionary<string, object>>();
            
            // CharacterStatusScreen has its bottom in horizNav context
            var horizNav = FP(window, "horizNav");
            if (horizNav != null)
            {
                var menuOpts = FP(horizNav, "menuOptionDescriptions") as System.Collections.IEnumerable;
                bottom.AddRange(MenuItemHelpers.HarvestMenuItems(menuOpts));
            }
            return bottom;
        }

        private static List<Dictionary<string, object>> ScrapePickGameObjectBottom(UnityEngine.Component window)
        {
            var bottom = new List<Dictionary<string, object>>();
            
            // Get the hotkeyBar which contains menu options
            var hotkeyBar = FP(window, "hotkeyBar");
            bottom.AddRange(HarvestMenuOptionsFromScroller(hotkeyBar));
            
            // The hotkeyBar should already have the menu options from yieldMenuOptions()
            // which is called in UpdateMenuBars(), but let's also try to get them directly
            
            // Try to call yieldMenuOptions() to get the current menu options
            try
            {
                var yieldMethod = window.GetType().GetMethod("yieldMenuOptions", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                
                if (yieldMethod != null)
                {
                    var result = yieldMethod.Invoke(window, null);
                    if (result is System.Collections.IEnumerable enumerable)
                    {
                        bottom.AddRange(MenuItemHelpers.HarvestMenuItems(enumerable));
                    }
                }
            }
            catch { }
            
            // Also check navigationContext
            var navContext = FP(window, "navigationContext");
            if (navContext != null)
            {
                var menuOpts = FP(navContext, "menuOptionDescriptions") as System.Collections.IEnumerable;
                bottom.AddRange(MenuItemHelpers.HarvestMenuItems(menuOpts));
            }
            
            // Check the vertNav context as well
            var vertNav = FP(window, "vertNav");
            if (vertNav != null)
            {
                var scrollContext = FP(vertNav, "scrollContext");
                if (scrollContext != null)
                {
                    var menuOpts = FP(scrollContext, "menuOptionDescriptions") as System.Collections.IEnumerable;
                    bottom.AddRange(MenuItemHelpers.HarvestMenuItems(menuOpts));
                }
            }
            
            return bottom;
        }

        private static List<Dictionary<string, object>> ScrapeStatusScreensHubBottom(object statusScreens)
        {
            var bottom = new List<Dictionary<string, object>>();

            var ctx = FP(statusScreens, "screenGlobalContext");
            var ctxMenu = FP(ctx, "menuOptionDescriptions") as System.Collections.IEnumerable;
            bottom.AddRange(MenuItemHelpers.HarvestMenuItems(ctxMenu));

            var defaults = FP(statusScreens, "defaultMenuOptionOrder") as System.Collections.IEnumerable;
            bottom.AddRange(MenuItemHelpers.HarvestMenuItems(defaults));

            var cs = FP(statusScreens, "CurrentScreen");
            var current = cs is int ii ? ii : 0;
            if (current == 2)
            {
                var scroller = FP(statusScreens, "menuOptionScroller");
                bottom.AddRange(HarvestMenuOptionsFromScroller(scroller));
            }
            return MenuItemHelpers.DedupMenuItems(bottom);
        }

        private static List<Dictionary<string, object>> ScrapeTradeScreenBottom(UnityEngine.Component window)
        {
            var bottom = new List<Dictionary<string, object>>();
            
            // Get the hotkeyBar which contains menu options
            var hotkeyBar = FP(window, "hotkeyBar");
            bottom.AddRange(HarvestMenuOptionsFromScroller(hotkeyBar));
            
            // Also check navigationContext
            var navContext = FP(window, "navigationContext");
            if (navContext != null)
            {
                var menuOpts = FP(navContext, "menuOptionDescriptions") as System.Collections.IEnumerable;
                bottom.AddRange(MenuItemHelpers.HarvestMenuItems(menuOpts));
            }
            return bottom;
        }

        private static List<Dictionary<string, object>> HarvestMenuOptionsFromScroller(object scroller)
        {
            var list = new List<Dictionary<string, object>>();
            if (scroller == null) return list;

            var choices = FP(scroller, "choices");
            MenuItemHelpers.HarvestMenuItems(choices, list);

            var clones = FP(scroller, "selectionClones");
            MenuItemHelpers.HarvestMenuItems(clones, list);

            return list;
        }

        private static void HarvestBottomFromNavContext(object navContext, List<Dictionary<string, object>> into)
        {
            if (navContext == null) return;

            var mods = FP(navContext, "menuOptionDescriptions");
            MenuItemHelpers.HarvestMenuItems(mods, into);

            var controller = FP(navContext, "controller");
            if (controller != null)
            {
                var items = FP(controller, "menuOptionDescriptions");
                MenuItemHelpers.HarvestMenuItems(items, into);
            }
        }

        private static List<Dictionary<string, object>> ReduceToTextHotkeyOnly(IEnumerable<Dictionary<string, object>> items)
        {
            var outList = new List<Dictionary<string, object>>();
            if (items == null) return outList;

            foreach (var d in items)
            {
                if (d == null) continue;
                var text = d.TryGetValue("text", out var tv) ? tv as string : "";
                var hot = d.TryGetValue("hotkey", out var hv) ? hv as string : "";
                if (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(hot))
                {
                    outList.Add(new Dictionary<string, object> {
                        ["text"] = StringHelpers.StripQudMarkup(text ?? ""),
                        ["hotkey"] = hot ?? ""
                    });
                }
            }
            return outList;
        }

        public static List<Dictionary<string, object>> KeepTextAndHotkeyOnly(IEnumerable<object> items)
        {
            var outList = new List<Dictionary<string, object>>();
            if (items == null) return outList;

            foreach (var o in items)
            {
                if (o is Dictionary<string, object> d)
                {
                    var text = d.TryGetValue("text", out var tv) ? tv as string : "";
                    var hot = d.TryGetValue("hotkey", out var hv) ? hv as string : "";
                    if (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(hot))
                        outList.Add(new Dictionary<string, object> { ["text"] = text ?? "", ["hotkey"] = hot ?? "" });
                }
                else if (o != null)
                {
                    var text = o.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                        outList.Add(new Dictionary<string, object> { ["text"] = text, ["hotkey"] = "" });
                }
            }
            return outList;
        }

        public static List<Dictionary<string, object>> FilterEquipmentBottom(List<Dictionary<string, object>> items)
        {
            var blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "navigate", "select", "remove keybind", "restore defaults", "laptop defaults"
            };

            var outList = new List<Dictionary<string, object>>();
            foreach (var d in items ?? new List<Dictionary<string, object>>())
            {
                var text = d.TryGetValue("text", out var tv) ? tv as string : "";
                if (!string.IsNullOrWhiteSpace(text) && !blacklist.Contains(text.Trim()))
                    outList.Add(d);
            }
            return outList;
        }

        public static List<Dictionary<string, object>> FilterStatusBottom(List<Dictionary<string, object>> items)
        {
            var blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "navigate", "select", "remove keybind", "restore defaults", "laptop defaults"
            };
            
            var outList = new List<Dictionary<string, object>>();
            foreach (var d in items ?? new List<Dictionary<string, object>>())
            {
                if (d == null) continue;
                var text = d.TryGetValue("text", out var tv) ? tv as string : "";
                var hot = d.TryGetValue("hotkey", out var hv) ? hv as string : "";
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (blacklist.Contains(text.Trim())) continue;
                if (!string.IsNullOrEmpty(hot) && hot.Length == 1 && hot[0] >= 0xE000 && hot[0] <= 0xF8FF)
                    hot = "";
                outList.Add(new Dictionary<string, object> { ["text"] = text ?? "", ["hotkey"] = hot ?? "" });
            }
            return outList;
        }
    }
}