using System.Reflection;
using AICompanion.Chat;
using AICompanion.Companion;
using AICompanion.Config;
using AICompanion.Dialog;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AICompanion
{
    public sealed class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Config.ModLog.Info("OnSubModuleLoad — AI Companion starting up.");
            AICompanionConfig.Reload();

            try
            {
                new Harmony("AICompanion").PatchAll(Assembly.GetExecutingAssembly());
                Config.ModLog.Info("Harmony patches applied (DialogTextPatch).");
            }
            catch (System.Exception ex)
            {
                Config.ModLog.Error("Failed to apply Harmony patches", ex);
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            Config.ModLog.Info($"OnGameStart — GameType: {game.GameType?.GetType().Name}.");

            if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter starter)
            {
                starter.AddBehavior(new AICompanionRoleBehavior());
                starter.AddBehavior(new ChatHistoryBehavior());
                starter.AddBehavior(new HandOfTheKingBehavior());
                starter.AddBehavior(new CommandDelegationState());
                starter.AddBehavior(new CompanionOpinionBehavior());
                starter.AddBehavior(new CompanionMemoryBehavior());
                Config.ModLog.Info("Campaign behaviors registered.");

                ChatDialogBehavior.RegisterDialogs(starter);
            }
        }

        public override void OnMissionBehaviorInitialize(TaleWorlds.MountAndBlade.Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            Config.ModLog.Info($"OnMissionBehaviorInitialize — Mission.Mode={mission.Mode}, " +
                                $"SceneName={mission.SceneName}.");
            mission.AddMissionBehavior(new AICompanion.Mission.CompanionCommandMissionBehavior());
            mission.AddMissionBehavior(new AICompanion.Mission.ChatConversationView());
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            // OnMissionScreenTick never fires on ChatConversationView (confirmed via logging —
            // a reply sat queued forever, never applied), so pump it from here instead, which we
            // already know runs every real engine frame.
            TaleWorlds.MountAndBlade.Mission.Current
                ?.GetMissionBehavior<AICompanion.Mission.ChatConversationView>()?.Pump();
        }
    }
}
