using AICompanion.Chat;
using AICompanion.Companion;
using AICompanion.Config;
using TaleWorlds.CampaignSystem;

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
            ModLog.Info("RegisterDialogs called.");

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

            starter.AddPlayerLine(
                "aicompanion_delegate_command",
                "hero_main_options",
                "aicompanion_delegate_command_response",
                "{=aicompanion_delegate_command_line}Lidere as tropas na próxima batalha!",
                IsTalkingToCompanion,
                DelegateCommand);

            starter.AddDialogLine(
                "aicompanion_delegate_command_ack",
                "aicompanion_delegate_command_response",
                "close_window",
                "{=aicompanion_delegate_command_ack}Como quiser. Na próxima batalha, deixe " +
                "comigo — cuidarei bem dos seus homens.",
                null,
                null);
        }

        private static void DelegateCommand()
        {
            AICompanion.Companion.CommandDelegationState.CommandDelegated = true;
        }

        private static bool IsTalkingToCompanion()
        {
            var conversationHero = Hero.OneToOneConversationHero;
            var result = conversationHero != null &&
                         conversationHero.StringId == CompanionDefinition.HeroStringId;
            ModLog.Info($"IsTalkingToCompanion check: conversationHero=" +
                        $"{conversationHero?.StringId ?? "null"} ({conversationHero?.Name}), result={result}.");
            return result;
        }

        private static void OpenChatScreen()
        {
            // Doing this straight from inside a dialog consequence fights with the conversation
            // system's own cleanup — deferring to ConversationEndOneShot lets the conversation
            // actually finish closing first.
            ModLog.Info("OpenChatScreen: deferring to ConversationEndOneShot.");
            Campaign.Current.ConversationManager.ConversationEndOneShot += OpenChatOverlay;
        }

        private static void OpenChatOverlay()
        {
            ModLog.Info("OpenChatOverlay: conversation ended, opening chat overlay now.");
            ChatOverlay.Open();
        }
    }
}
