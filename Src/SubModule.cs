using AICompanion.Chat;
using AICompanion.Companion;
using AICompanion.Config;
using AICompanion.Dialog;
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
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            Config.ModLog.Info($"OnGameStart — GameType: {game.GameType?.GetType().Name}.");

            if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter starter)
            {
                starter.AddBehavior(new CompanionBehavior());
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
            mission.AddMissionBehavior(new AICompanion.Mission.CompanionCommandMissionBehavior());
        }
    }
}
