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
            // The layer itself was already built (invisible, inert) back when this conversation
            // began — see ChatConversationView.OnConversationBegin. This just makes it visible
            // and takes input. Ported from wonderingmark123/Bannerlord.ChatGPT's
            // UpdateChatStatus(true) pattern: building the layer this late in the conversation
            // hit MissionScreen == null on a real test (confirmed via crash log).
            var view = TaleWorlds.MountAndBlade.Mission.Current
                ?.GetMissionBehavior<AICompanion.Mission.ChatConversationView>();
            if (view == null)
            {
                ModLog.Error("OpenChatScreen: no ChatConversationView found on the current mission.");
                return;
            }

            ModLog.Info("OpenChatScreen: activating chat via ChatConversationView.");
            view.SetActive(true);
        }
    }
}
