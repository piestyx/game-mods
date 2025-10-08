// Mods/QudStateExtractor/patches/CoreLifecyclePatch.cs

/// <summary> 
/// Contains the Harmony patches and events used for hooking into core game lifecycle
/// 
/// </summary>

using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using XRL;
using XRL.Core;
using XRL.World;
using XRL.World.Conversations;
using XRL.UI;
using QudStateExtractor.Core;
using QudStateExtractor.Exporters;
using GO = XRL.World.GameObject;

namespace QudStateExtractor.Patches
{
    public static class CoreLifecyclePatches
    {
        [HarmonyPatch(typeof(GO), nameof(GO.FireEvent), new[] { typeof(XRL.World.Event) })]
        public static class Patch_PlayerFireEvent
        {
            static void Postfix(GO __instance, XRL.World.Event E)
            {
                if (!__instance.IsPlayer()) return;

                switch (E.ID)
                {
                    case "GameStart":
                    case "Regenerating2":
                        GameStateExporters.ExportAgentState();
                        GameStateExporters.ExportWorldState();
                        GameStateExporters.ExportJournal();
                        GameStateExporters.ExportPointsOfInterest();
                        break;
                }
            }
        }

        [HarmonyPatch(typeof(QuestStartedEvent), nameof(QuestStartedEvent.Send))]
        public static class Patch_QuestStartedEvent_Send
        {
            static void Postfix() => GameStateExporters.ExportQuests();
        }

        [HarmonyPatch(typeof(XRLGame), nameof(XRLGame.FinishQuest), new[] { typeof(string) })]
        public static class Patch_QuestFinished
        {
            static void Postfix(string QuestID) => GameStateExporters.ExportQuests();
        }

        [HarmonyPatch(typeof(XRLGame), nameof(XRLGame.FinishQuestStep), new[] { typeof(string), typeof(string), typeof(int), typeof(bool), typeof(string) })]
        public static class Patch_QuestStepFinished
        {
            static void Postfix(string QuestID, string QuestStepList) => GameStateExporters.ExportQuests();
        }

        [HarmonyPatch(typeof(Statistic), nameof(Statistic.NotifyChange))]
        public static class Patch_Statistic_NotifyChange
        {
            static void Postfix(Statistic __instance)
            {
                if (__instance?.Owner?.IsPlayer() != true) return;

                switch (__instance.Name)
                {
                    case "Hitpoints":
                    case "Strength":
                    case "Toughness":
                    case "Agility":
                    case "Willpower":
                    case "Ego":
                    case "Intelligence":
                        GameStateExporters.ExportAgentState();
                        break;
                }
            }
        }

        [HarmonyPatch(typeof(ConversationUI), nameof(ConversationUI.Prepare))]
        public static class Patch_ConversationUI_Prepare
        {
            static void Postfix() => GameStateExporters.ExportDialogue();
        }

        [HarmonyPatch]
        public static class Patch_MessageQueue_AddPlayerMessage
        {
            static System.Reflection.MethodBase TargetMethod() =>
                AccessTools.Method("XRL.Messages.MessageQueue:AddPlayerMessage",
                    new Type[] { typeof(string), typeof(string), typeof(bool) });

            static void Postfix(string Message, string Color, bool Capitalize)
            {
                try
                {
                    string logPath = EnvHelper.GetEnvPath("MESSAGE_LOG_FILE_PATH");
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? ".");

                    if (Message == "You died.")
                    {
                        File.WriteAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [System] Log reset on player death.\n");
                        Debug.Log("[Narrator] Log file reset due to death message.");
                        return;
                    }

                    using (var writer = new StreamWriter(logPath, append: true))
                        writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] {Message}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Narrator] ExportMessageLog failed: {ex.Message}");
                }
            }
        }
    }
}