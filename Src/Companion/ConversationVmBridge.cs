using TaleWorlds.CampaignSystem.ViewModelCollection.Conversation;

namespace AICompanion.Companion
{
    /// <summary>
    /// Bridges ChatVM (our chat window) with the vanilla conversation nameplate box — the one
    /// showing "Claro. Diga o que pensa." — so Cláudio's real reply is shown there directly,
    /// instead of only in our own panel, per the player's explicit request to reuse that
    /// existing UI instead of adding a redundant one. DialogTextPatch (a Harmony patch on
    /// MissionConversationVM.DialogText's setter) captures the live instance here and redirects
    /// its text to <see cref="OverrideText"/> whenever <see cref="IsActive"/> is true — scoped
    /// strictly to when our chat is open, so every other NPC conversation in the game is
    /// completely unaffected.
    /// </summary>
    public static class ConversationVmBridge
    {
        public static MissionConversationVM Instance;
        public static bool IsActive;
        public static string OverrideText = string.Empty;

        /// <summary>Pushes the current text into the live vanilla box, if one is captured.</summary>
        public static void Push(string text)
        {
            OverrideText = text ?? string.Empty;
            if (IsActive && Instance != null)
            {
                Instance.DialogText = OverrideText;
            }
        }
    }
}
