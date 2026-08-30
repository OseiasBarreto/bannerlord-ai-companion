using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace AICompanion.Companion
{
    /// <summary>
    /// Spawns the AI companion hero once per campaign and puts him directly in the player's
    /// party from the very first day — he's meant to always be at the player's side, not
    /// something to go track down and recruit like a regular wanderer.
    /// </summary>
    public sealed class CompanionBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No custom save data needed: the hero itself is tracked by the base game's
            // save system once created (MBObjectManager persists all Hero objects).
        }

        private void OnNewGameCreated(CampaignGameStarter starter) => EnsureCompanionExists();

        private void OnGameLoaded(CampaignGameStarter starter) => EnsureCompanionExists();

        private void EnsureCompanionExists()
        {
            try
            {
                var existing = Hero.AllAliveHeroes
                    .FirstOrDefault(h => h.StringId == CompanionDefinition.HeroStringId);
                if (existing != null)
                {
                    return;
                }

                var template = MBObjectManager.Instance.GetObject<CharacterObject>(
                    CompanionDefinition.CharacterTemplateStringId);
                if (template == null)
                {
                    // Fallback: any vanilla wanderer template, in case the configured id
                    // doesn't exist in this game version.
                    template = CharacterObject.All
                        .FirstOrDefault(c => c.Occupation == Occupation.Wanderer);
                }

                if (template == null)
                {
                    Debug.Print("[AICompanion] No wanderer template found — cannot spawn Cláudio.");
                    return;
                }

                var hero = HeroCreator.CreateSpecialHero(
                    template, null, Clan.PlayerClan, null, MBRandom.RandomInt());

                hero.StringId = CompanionDefinition.HeroStringId;
                hero.SetName(new TaleWorlds.Localization.TextObject(CompanionDefinition.Name),
                             new TaleWorlds.Localization.TextObject(CompanionDefinition.FullTitle));
                hero.Occupation = Occupation.Wanderer;
                hero.ChangeState(Hero.CharacterStates.Active);

                // No going to a tavern to find him — he starts right there in the player's
                // own party, same as any companion recruited via AddCompanionAction.
                AddCompanionAction.Apply(MobileParty.MainParty, hero);

                InformationManager.DisplayMessage(new InformationMessage(
                    $"{CompanionDefinition.FullTitle} se junta a você desde o primeiro dia " +
                    "de jornada.", Colors.Yellow));

                Debug.Print($"[AICompanion] Spawned {CompanionDefinition.FullTitle} directly " +
                            "into the player's party.");
            }
            catch (Exception ex)
            {
                // Never let a spawn failure take down the whole campaign — log and move on.
                Debug.Print($"[AICompanion] Failed to spawn companion: {ex}");
            }
        }
    }
}
