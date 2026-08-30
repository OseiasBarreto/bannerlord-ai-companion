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
            AICompanionConfig.Reload();
            Debug.Print(AICompanionConfig.Instance.IsConfigured
                ? "[AICompanion] API key loaded — chat enabled."
                : "[AICompanion] No API key found — chat will show a setup message in-game.");
        }

        public override void OnCampaignStart(Game game, object starterObject)
        {
            base.OnCampaignStart(game, starterObject);
            if (starterObject is CampaignGameStarter starter)
            {
                ChatDialogBehavior.RegisterDialogs(starter);
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter starter)
            {
                starter.AddBehavior(new CompanionBehavior());
            }
        }
    }
}
