// Mods/QudStateExtractor/scrapers/MenuItemHelper.cs

/// <summary> 
/// Helpers for extracting menu items from various in-game UI elements
/// </summary>

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using QudStateExtractor.Core;
using static QudStateExtractor.Core.ReflectionHelpers;

namespace QudStateExtractor.Scrapers
{
    public static class MenuItemHelpers
    {
        static bool ControlMgrChecked = false;
        static Type ControlMgrType = null;

        public static string GetBindingDisplayForCommand(string commandId)
        {
            if (string.IsNullOrEmpty(commandId)) return "";
            try
            {
                if (!ControlMgrChecked)
                {
                    // ControlManager is in the global namespace, not XRL.UI
                    ControlMgrType = AccessTools.TypeByName("ControlManager");
                    ControlMgrChecked = true;
                }
                if (ControlMgrType == null) return "";

                // Look for the 5-parameter getCommandInputDescription method
                MethodInfo mi = null;
                var methods = ControlMgrType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                
                foreach (var m in methods)
                {
                    if (m.Name == "getCommandInputDescription")
                    {
                        var ps = m.GetParameters();
                        if (ps.Length == 5)
                        {
                            mi = m;
                            break;
                        }
                    }
                }
                
                if (mi == null) return "";

                var paramTypes = mi.GetParameters();
                var inputDeviceType = paramTypes[3].ParameterType;
                
                // Get the "Unknown" enum value (3) from ControlManager.InputDeviceType
                object enumVal = inputDeviceType.IsEnum ? Enum.ToObject(inputDeviceType, 3) : null;

                var args = new object[] { commandId, false, false, enumVal, false };
                return mi.Invoke(null, args) as string ?? "";
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Narrator] GetBindingDisplayForCommand failed for '{commandId}': {ex.Message}");
                return "";
            }
        }

        public static Dictionary<string, object> QudMenuItemToDict(object item)
        {
            if (item == null) return new Dictionary<string, object>();

            string GetStr(params string[] names)
            {
                foreach (var n in names)
                {
                    var v = FP(item, n);
                    if (v is string s) return s;
                }
                return "";
            }

            var d = new Dictionary<string, object>
            {
                ["text"] = GetStr("text", "Description", "Text", "Label", "label", "name"),
                ["command"] = GetStr("command", "Command", "InputCommand", "id"),
                ["hotkey"] = GetStr("hotkey", "Hotkey"),
                ["simpleText"] = GetStr("simpleText")
            };

            if (string.IsNullOrEmpty((string)d["hotkey"]) && !string.IsNullOrEmpty((string)d["command"]))
                d["hotkey"] = GetBindingDisplayForCommand((string)d["command"]) ?? "";

            return d;
        }

        public static Dictionary<string, object> MenuOptionToDict(object item)
        {
            // Alias for QudMenuItemToDict - they do the same thing
            return QudMenuItemToDict(item);
        }

        public static void HarvestMenuItems(object maybeEnumerable, List<Dictionary<string, object>> into)
        {
            if (maybeEnumerable == null) return;

            if (maybeEnumerable is System.Collections.IDictionary dict)
            {
                foreach (var val in dict.Values)
                    if (val != null)
                        into.Add(QudMenuItemToDict(val));
                return;
            }

            if (maybeEnumerable is System.Collections.IEnumerable en)
            {
                foreach (var it in en)
                {
                    if (it == null) continue;

                    object candidate = it;
                    var valProp = it.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (valProp != null && valProp.GetIndexParameters().Length == 0)
                    {
                        try { candidate = valProp.GetValue(it, null) ?? it; } catch { }
                    }

                    bool hasAnyText =
                        FP(candidate, "text") is string ||
                        FP(candidate, "Description") is string ||
                        FP(candidate, "Text") is string ||
                        FP(candidate, "Label") is string ||
                        FP(candidate, "label") is string ||
                        FP(candidate, "name") is string;

                    if (hasAnyText)
                        into.Add(QudMenuItemToDict(candidate));
                }
            }
        }

        public static List<Dictionary<string, object>> HarvestMenuItems(object maybeEnumerable)
        {
            var list = new List<Dictionary<string, object>>();
            HarvestMenuItems(maybeEnumerable, list);
            return list;
        }

        public static List<Dictionary<string, object>> DedupMenuItems(List<Dictionary<string, object>> items)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var outList = new List<Dictionary<string, object>>(items.Count);
            foreach (var d in items)
            {
                if (d == null) continue;
                var t = (d.TryGetValue("text", out var tObj) ? tObj as string : "") ?? "";
                var c = (d.TryGetValue("command", out var cObj) ? cObj as string : "") ?? "";
                var h = (d.TryGetValue("hotkey", out var hObj) ? hObj as string : "") ?? "";

                if (string.IsNullOrWhiteSpace(t) && string.IsNullOrWhiteSpace(c) && string.IsNullOrWhiteSpace(h))
                    continue;
                if (t.Contains("KeyMenuOption"))
                    continue;

                var key = $"{t}|{c}|{h}";
                if (seen.Add(key))
                    outList.Add(d);
            }
            return outList;
        }
    }
}