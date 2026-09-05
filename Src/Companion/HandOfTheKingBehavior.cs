using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace AICompanion.Companion
{
    /// <summary>
    /// Watches whether the player has become a kingdom's ruler and, when so, elevates whoever
    /// currently holds "Minha Mão" to "Mão do Rei" — a display-name title only (Bannerlord's own
    /// council roles are assigned separately, in the kingdom screen), not a mechanical change.
    /// Only ever touches the hero's full display name via SetName's second argument; FirstName
    /// stays untouched, so re-applying this never compounds into "X, a Mão do Rei, a Mão do Rei".
    /// Also flows into the chat system prompt via <see cref="IsHandOfTheKing"/>.
    /// </summary>
    public sealed class HandOfTheKingBehavior : CampaignBehaviorBase
    {
        public static bool IsHandOfTheKing { get; private set; }

        private string _titledHeroId;

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
            dataStore.SyncData("AICompanion_TitledHeroId", ref _titledHeroId);
        }

        private void CheckStatus()
        {
            var clan = Hero.MainHero?.Clan;
            var kingdom = clan?.Kingdom;
            var isRulerNow = kingdom != null && kingdom.RulingClan == clan;
            var holder = AICompanionRoleBehavior.Instance?.CurrentHolder;

            // Revert whichever hero was previously titled if they're no longer the holder, or
            // no longer applicable — never leave a stale "a Mão do Rei" title dangling on
            // someone who was dismissed or replaced.
            if (_titledHeroId != null && (holder == null || holder.StringId != _titledHeroId ||
                                           !isRulerNow))
            {
                RevertTitle(_titledHeroId);
                _titledHeroId = null;
            }

            var wasHandOfTheKing = IsHandOfTheKing;
            IsHandOfTheKing = isRulerNow && holder != null;

            if (IsHandOfTheKing && _titledHeroId != holder.StringId)
            {
                ApplyTitle(holder);
                _titledHeroId = holder.StringId;

                if (!wasHandOfTheKing)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{holder.FirstName} agora é a Mão do Rei — seu conselheiro mais " +
                        "próximo desde que você ascendeu ao trono.", Colors.Yellow));
                }
            }
        }

        private static void ApplyTitle(Hero hero)
        {
            hero.SetName(hero.FirstName, new TextObject("{=aicompanion_hand_of_king}{FIRST_NAME}, a Mão do Rei")
                .SetTextVariable("FIRST_NAME", hero.FirstName));
        }

        private static void RevertTitle(string heroId)
        {
            var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == heroId);
            hero?.SetName(hero.FirstName, hero.FirstName);
        }
    }
}
