using System;
using HarmonyLib;
using XRL;
using XRL.World;
using XRL.World.Conversations;
using XRL.UI;
using XRL.Core;
using Qud.API;

namespace QudStateExtractor
{
    public static class QudStateTriggers
    {
        [HarmonyPatch(typeof(GameObject), nameof(GameObject.FireEvent), new[] { typeof(Event) })]
        public static class Patch_PlayerFireEvent
        {
            static void Postfix(GameObject __instance, Event E)
            {
                if (!__instance.IsPlayer()) return;

                if (E.ID != "IsMobile")
                    UnityEngine.Debug.Log($"[Narrator][DEBUG] Event fired: {E.ID}");

                switch (E.ID)
                {
                    // Inventory
                    case "ObjectAddedToPlayerInventory":
                    case "PerformDrop":
                        QudStateExtractor.ExportAgentState();
                        break;

                    // Mutations
                    case "SyncMutationLevels":
                        QudStateExtractor.ExportAgentState();
                        break;

                    // Abilities — not yet confirmed
                    case "BeforeCooldownActivatedAbility":
                        QudStateExtractor.ExportAgentState();
                        break;

                    // Journal/Quests
                    case "AccomplishmentAdded":
                    case "GetPointsOfInterest":
                    case "QuestStarted":
                        QudStateExtractor.ExportJournal();
                        break;

                    // World
                    case "LookedAt":
                        QudStateExtractor.ExportWorldState();
                        QudStateExtractor.ExportJournal();
                        break;

                    // Agent
                    case "AIWakeupBroadcast":
                        QudStateExtractor.ExportAgentState();
                        QudStateExtractor.ExportWorldState();
                        break;
                }
            }
        }

        [HarmonyPatch(typeof(QuestStartedEvent), nameof(QuestStartedEvent.Send))]
        public static class Patch_QuestStartedEvent_Send
        {
            static void Postfix()
            {
                UnityEngine.Debug.Log($"[Narrator][DEBUG] QuestStartedEvent.Send fired");
                QudStateExtractor.ExportQuests();
            }
        }

        [HarmonyPatch(typeof(XRLGame), nameof(XRLGame.FinishQuest), new[] { typeof(string) })]
        public static class Patch_QuestFinished
        {
            static void Postfix(string QuestID)
            {
                UnityEngine.Debug.Log($"[Narrator][DEBUG] Quest finished: {QuestID}");
                QudStateExtractor.ExportQuests();
            }
        }

        [HarmonyPatch(typeof(XRLGame), nameof(XRLGame.FinishQuestStep), new[] { typeof(string), typeof(string), typeof(int), typeof(bool), typeof(string) })]
        public static class Patch_QuestStepFinished
        {
            static void Postfix(string QuestID, string QuestStepList)
            {
                UnityEngine.Debug.Log($"[Narrator][DEBUG] Quest step finished: {QuestID}:{QuestStepList}");
                QudStateExtractor.ExportQuests();
            }
        }

        [HarmonyPatch(typeof(Statistic), nameof(Statistic.NotifyChange))]
        public static class Patch_Statistic_NotifyChange
        {
            static void Postfix(Statistic __instance)
            {
                if (__instance?.Owner?.IsPlayer() != true)
                    return;

                switch (__instance.Name)
                {
                    case "Hitpoints":
                    case "Strength":
                    case "Toughness":
                    case "Agility":
                    case "Willpower":
                    case "Ego":
                    case "Intelligence":
                        UnityEngine.Debug.Log($"[Narrator][DEBUG] Stat changed: {__instance.Name}");
                        QudStateExtractor.ExportAgentState();
                        break;
                }
            }
        }

        [HarmonyPatch(typeof(ConversationUI), nameof(ConversationUI.Prepare))]
        public static class Patch_ConversationUI_Prepare
        {
            static void Postfix()
            {
                QudStateExtractor.ExportDialogue();
            }
        }
    }
}
