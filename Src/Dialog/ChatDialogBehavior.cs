using AICompanion.Companion;
using AICompanion.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace AICompanion.Dialog
{
    /// <summary>
    /// Adds dialog options for the "Minha Mão" system: promoting/dismissing whoever holds the
    /// role, and — for the current holder specifically — the AI chat and battle-command
    /// options. Whether these show up is now driven entirely by
    /// <see cref="AICompanionRoleBehavior"/> instead of one fixed hero's StringId.
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
                IsTalkingToHolder,
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
                IsTalkingToHolder,
                DelegateCommand);

            starter.AddDialogLine(
                "aicompanion_delegate_command_ack",
                "aicompanion_delegate_command_response",
                "close_window",
                "{=aicompanion_delegate_command_ack}Como quiser. Na próxima batalha, deixe " +
                "comigo — cuidarei bem dos seus homens.",
                null,
                null);

            // "companion_roles" is the vanilla node listing Engenheiro/Cirurgião/Intendente/
            // Batedor ("Que função você tem em mente?") — confirmed live via
            // ConversationTokenLogger (reflecting ConversationManager.ActiveToken/stateMap):
            // the real transition is hero_main_options -> companion_role -> companion_roles,
            // and the role list itself is shown exactly when the active token is
            // "companion_roles". Two earlier guesses ("party_role_assignment", then
            // "companion_okay_to_role_selection", both inferred from method/string names
            // without live confirmation) were wrong. Minha Mão belongs in this same list
            // conceptually, not loose in the main hero menu.
            starter.AddPlayerLine(
                "aicompanion_promote",
                "companion_roles",
                "aicompanion_promote_response",
                "{=aicompanion_promote_line}Quero que você seja minha mão.",
                IsPromotableCompanion,
                Promote);

            starter.AddDialogLine(
                "aicompanion_promote_ack",
                "aicompanion_promote_response",
                "close_window",
                "{=aicompanion_promote_ack}Será uma honra. Pode contar comigo pra tudo daqui em diante.",
                null,
                null);

            starter.AddPlayerLine(
                "aicompanion_dismiss",
                "companion_roles",
                "aicompanion_dismiss_response",
                "{=aicompanion_dismiss_line}Não preciso mais que você seja minha mão.",
                IsTalkingToHolder,
                Dismiss);

            starter.AddDialogLine(
                "aicompanion_dismiss_ack",
                "aicompanion_dismiss_response",
                "close_window",
                "{=aicompanion_dismiss_ack}Entendido. Sempre por perto, se precisar de novo.",
                null,
                null);
        }

        private static void DelegateCommand()
        {
            CommandDelegationState.CommandDelegated = true;
        }

        private static bool IsTalkingToHolder()
        {
            var conversationHero = Hero.OneToOneConversationHero;
            var result = AICompanionRoleBehavior.Instance != null &&
                         AICompanionRoleBehavior.Instance.IsHolder(conversationHero);
            ModLog.Info($"IsTalkingToHolder check: conversationHero=" +
                        $"{conversationHero?.StringId ?? "null"} ({conversationHero?.Name}), result={result}.");
            return result;
        }

        /// <summary>
        /// Any living hero in the player's own clan, other than the player, who doesn't already
        /// hold the role — this covers both family members and recruited companions, matching
        /// "qualquer herói do grupo pode virar minha mão."
        /// </summary>
        private static bool IsPromotableCompanion()
        {
            var hero = Hero.OneToOneConversationHero;
            if (hero == null || hero == Hero.MainHero || hero.Clan != Clan.PlayerClan)
            {
                return false;
            }

            return AICompanionRoleBehavior.Instance == null ||
                   !AICompanionRoleBehavior.Instance.IsHolder(hero);
        }

        private static void Promote()
        {
            var hero = Hero.OneToOneConversationHero;
            AICompanionRoleBehavior.Instance?.Promote(hero);
            InformationManager.DisplayMessage(new InformationMessage(
                $"{hero?.Name} agora é sua Mão.", Colors.Yellow));
        }

        private static void Dismiss()
        {
            var hero = Hero.OneToOneConversationHero;
            AICompanionRoleBehavior.Instance?.Clear();
            InformationManager.DisplayMessage(new InformationMessage(
                $"{hero?.Name} não é mais sua Mão.", Colors.Yellow));
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
