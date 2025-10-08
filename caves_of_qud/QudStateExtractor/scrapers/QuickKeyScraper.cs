// Mods/QudStateExtractor/scrapers/QuickKeyScraper.cs

/// <summary> 
/// Different methods required per UI screen for extracting the quick key list
/// and utilising it as an `id` in the export
/// </summary>

using System;
using System.Collections.Generic;
using UnityEngine;
using QudStateExtractor.Core;
using static QudStateExtractor.Core.ReflectionHelpers;

namespace QudStateExtractor.Scrapers
{
    public static class QuickKeyScraper
    {
        public static List<Dictionary<string, object>> ExtractFromCurrentWindow()
        {
            var outList = new List<Dictionary<string, object>>();
            try
            {
                var win = Qud.UI.UIManager.instance?.currentWindow as UnityEngine.Component;
                var go = win?.gameObject;
                if (go == null) return outList;

                var comps = go.GetComponentsInChildren<UnityEngine.Component>(true);
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    var tn = c.GetType().Name;
                    if (!tn.EndsWith("Line", StringComparison.Ordinal)) continue;

                    var ctx = FP(c, "context");
                    var data = FP(ctx, "data");
                    if (data == null) continue;

                    string name =
                        ReadString(data, "name")
                        ?? ReadString(data, "label")
                        ?? ReadString(FP(data, "ability"), "DisplayName")
                        ?? ReadString(FP(data, "item"), "DisplayName")
                        ?? ReadString(FP(data, "power"), "Name")
                        ?? ReadString(FP(data, "skill"), "Name")
                        ?? "";

                    var quick = FP(data, "quickKey")?.ToString() ?? "";
                    var hk = ReadString(data, "hotkeyDescription") ?? "";

                    if (!string.IsNullOrEmpty(name))
                    {
                        var entry = new Dictionary<string, object> {
                            ["name"] = StringHelpers.StripQudMarkup(name),
                            ["quickKey"] = quick ?? "",
                            ["hotkey"] = hk ?? ""
                        };

                        var rowIndexObj = FP(c, "scrollIndex");
                        if (rowIndexObj is int ri) entry["rowIndex"] = ri;

                        var goObj = FP(data, "go");
                        if (goObj != null)
                        {
                            var goId = ReadString(goObj, "IDIfAssigned")
                                    ?? ReadString(goObj, "id")
                                    ?? ReadString(goObj, "ID");
                            if (!string.IsNullOrEmpty(goId)) entry["goid"] = goId;
                            var bp = ReadString(goObj, "Blueprint");
                            if (!string.IsNullOrEmpty(bp)) entry["bp"] = bp;
                        }
                        
                        var slotStr = ReadString(data, "slot");
                        if (!string.IsNullOrEmpty(slotStr)) entry["slot"] = slotStr;

                        var ability = FP(data, "ability");
                        if (ability != null)
                        {
                            var guid = ReadString(ability, "ID") ?? ReadString(ability, "Guid") ?? "";
                            if (!string.IsNullOrEmpty(guid)) entry["abilityId"] = guid;
                            var cmd = ReadString(ability, "Command") ?? "";
                            if (!string.IsNullOrEmpty(cmd)) entry["command"] = cmd;
                        }

                        var power = FP(data, "power");
                        if (power != null)
                        {
                            var cls = ReadString(power, "Class") ?? "";
                            if (!string.IsNullOrEmpty(cls)) entry["powerClass"] = cls;
                        }
                        
                        var skill = FP(data, "skill");
                        if (skill != null)
                        {
                            var cls = ReadString(skill, "Class") ?? "";
                            if (!string.IsNullOrEmpty(cls)) entry["skillClass"] = cls;
                        }

                        outList.Add(entry);
                    }
                }
            }
            catch { }

            return outList;
        }

        public static List<Dictionary<string, object>> ExtractFromFrameworkScroller(object scroller)
        {
            var outList = new List<Dictionary<string, object>>();
            if (scroller == null) return outList;

            var choices = FP(scroller, "choices") as System.Collections.IEnumerable;
            if (choices == null) return outList;

            int i = 0;
            foreach (var item in choices)
            {
                if (item == null) { i++; continue; }

                if (item.GetType().FullName == "Qud.UI.InventoryLineData")
                {
                    bool isCategory = ReadBool(item, "category") ?? false;
                    if (isCategory) { i++; continue; }

                    string name = StringHelpers.StripQudMarkup(ReadString(item, "displayName") ?? "");
                    var go = FP(item, "go");
                    string goid = ReadString(go, "IDIfAssigned");
                    string bp = ReadString(go, "Blueprint");
                    var spread = FP(item, "spread");
                    string quickKey = CallString(spread, "charAt", i);

                    outList.Add(new Dictionary<string, object> {
                        ["name"] = name ?? "",
                        ["quickKey"] = quickKey ?? "",
                        ["hotkey"] = "",
                        ["goid"] = goid ?? "",
                        ["bp"] = bp ?? "",
                        ["rowIndex"] = i
                    });
                }
                else if (item.GetType().FullName == "Qud.UI.EquipmentLineData")
                {
                    var bodyPart = FP(item, "bodyPart");
                    string slot = ReadString(bodyPart, "Name");
                    var equipped = FP(bodyPart, "Equipped") ?? FP(bodyPart, "DefaultBehavior") ?? FP(bodyPart, "Cybernetics");
                    string goid = ReadString(equipped, "IDIfAssigned");
                    string name = StringHelpers.StripQudMarkup(ReadString(equipped, "DisplayName") ?? slot);
                    var spread = FP(item, "spread");
                    string quickKey = CallString(spread, "charAt", i);

                    outList.Add(new Dictionary<string, object> {
                        ["name"] = name ?? "",
                        ["quickKey"] = quickKey ?? "",
                        ["hotkey"] = "",
                        ["slot"] = slot ?? "",
                        ["goid"] = goid ?? "",
                        ["rowIndex"] = i
                    });
                }

                i++;
            }
            return outList;
        }

        public static List<Dictionary<string, object>> ExtractFromPickGameObjectScreen(object screen)
        {
            var outList = new List<Dictionary<string, object>>();
            if (screen == null) return outList;

            var listItems = FP(screen, "listItems") as System.Collections.IList;
            if (listItems == null) return outList;

            for (int i = 0; i < listItems.Count; i++)
            {
                var item = listItems[i];
                if (item == null) continue;

                // Skip categories
                var typeVal = FP(item, "type");
                if (typeVal != null && typeVal.ToString().Contains("Category")) continue;

                var go = FP(item, "go");
                if (go == null) continue;

                string name = StringHelpers.StripQudMarkup(ReadString(go, "DisplayName") ?? "");
                string quick = ReadString(item, "hotkeyDescription") ?? "";
                string goid = ReadString(go, "ID") ?? "";
                string bp = ReadString(go, "Blueprint") ?? "";

                if (!string.IsNullOrEmpty(name))
                {
                    outList.Add(new Dictionary<string, object> {
                        ["name"] = name,
                        ["quickKey"] = quick,
                        ["hotkey"] = "",
                        ["goid"] = goid,
                        ["bp"] = bp
                    });
                }
            }
            
            return outList;
        }
    }
}