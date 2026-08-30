using AICompanion.Chat;
using AICompanion.Companion;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ScreenSystem;

namespace AICompanion.Dialog
{
    /// <summary>
    /// Adds a "Conversar" dialog option whenever the player talks to the companion hero,
    /// opening the dedicated AI chat screen. Registered directly from SubModule.OnGameStart
    /// (not a CampaignBehaviorBase) since dialog trees are wired once, at CampaignGameStarter
    /// setup time, rather than through the per-tick behavior event system.
    /// </summary>
    public static class ChatDialogBehavior
    {
        public static void RegisterDialogs(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "aicompanion_start_chat",
                "hero_main_options",
                "aicompanion_chat_opened",
                "{=aicompanion_chat_line}Podemos conversar um instante?",
                IsTalkingToCompanion,
                OpenChatScreen);

            starter.AddDialogLine(
                "aicompanion_chat_opened_response",
                "aicompanion_chat_opened",
                "close_window",
                "{=aicompanion_chat_ack}Claro. Diga o que pensa.",
                null,
                null);
        }

        private static bool IsTalkingToCompanion()
        {
            var conversationHero = Hero.OneToOneConversationHero;
            return conversationHero != null &&
                   conversationHero.StringId == CompanionDefinition.HeroStringId;
        }

        private static void OpenChatScreen()
        {
            ScreenManager.PushScreen(new AICompanion.Chat.ChatScreen());
        }
    }
}
