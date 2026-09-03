using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Conversation;

namespace AICompanion.Companion
{
    /// <summary>
    /// Redirects the vanilla conversation nameplate's text (SPConversation.xml's "Claro. Diga o
    /// que pensa." box) to Cláudio's real reply while our chat is open. Captures every instance
    /// that gets a DialogText write — always a live one, since it's the same call the native
    /// screen itself uses — so ConversationVmBridge can push updates into it directly.
    /// </summary>
    [HarmonyPatch(typeof(MissionConversationVM))]
    [HarmonyPatch("DialogText", MethodType.Setter)]
    public static class DialogTextPatch
    {
        static void Prefix(MissionConversationVM __instance, ref string value)
        {
            ConversationVmBridge.Instance = __instance;

            if (ConversationVmBridge.IsActive)
            {
                value = ConversationVmBridge.OverrideText;
            }
        }
    }
}
