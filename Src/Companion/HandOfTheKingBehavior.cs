using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace AICompanion.Companion
{
    /// <summary>
    /// Watches whether the player has become a kingdom's ruler and, when so, elevates the
    /// companion from ordinary wanderer to "Mão do Rei" — a title, not a mechanical council
    /// seat (Bannerlord's own council roles are assigned separately, in the kingdom screen).
    /// The elevated status also flows into the chat system prompt via
    /// <see cref="IsHandOfTheKing"/>, so Cláudio's tone changes once it happens.
    /// </summary>
    public sealed class HandOfTheKingBehavior : CampaignBehaviorBase
    {
        public static bool IsHandOfTheKing { get; private set; }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, CheckStatus);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, (_) => CheckStatus());
        }

        public override void SyncData(IDataStore dataStore)
        {
            var isHand = IsHandOfTheKing;
            dataStore.SyncData("AICompanion_IsHandOfTheKing", ref isHand);
            IsHandOfTheKing = isHand;
        }

        private void CheckStatus()
        {
            var clan = Hero.MainHero?.Clan;
            var kingdom = clan?.Kingdom;
            var isRulerNow = kingdom != null && kingdom.RulingClan == clan;

            if (isRulerNow == IsHandOfTheKing)
            {
                return;
            }

            IsHandOfTheKing = isRulerNow;
            UpdateCompanionTitle();

            if (isRulerNow)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{CompanionDefinition.Name} agora é a Mão do Rei — seu conselheiro mais " +
                    "próximo desde que ascendeu ao trono.", Colors.Yellow));
            }
        }

        private void UpdateCompanionTitle()
        {
            var hero = Hero.AllAliveHeroes
                .FirstOrDefault(h => h.StringId == CompanionDefinition.HeroStringId);
            if (hero == null)
            {
                return;
            }

            var title = IsHandOfTheKing
                ? CompanionDefinition.HandOfTheKingTitle
                : CompanionDefinition.FullTitle;

            hero.SetName(new TextObject(CompanionDefinition.Name), new TextObject(title));
        }
    }
}
