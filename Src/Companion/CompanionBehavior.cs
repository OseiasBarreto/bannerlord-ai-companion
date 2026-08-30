using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace AICompanion.Companion
{
    /// <summary>
    /// Spawns the AI companion hero once per campaign and places him as a recruitable
    /// wanderer in a starting settlement, exactly like a normal vanilla wanderer NPC.
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

                var settlement = Settlement.All
                    .Where(s => s.IsTown)
                    .OrderBy(s => s.StringId)
                    .FirstOrDefault();

                var hero = HeroCreator.CreateSpecialHero(
                    template, settlement, null, null, MBRandom.RandomInt());

                hero.StringId = CompanionDefinition.HeroStringId;
                hero.SetName(new TaleWorlds.Localization.TextObject(CompanionDefinition.Name),
                             new TaleWorlds.Localization.TextObject(CompanionDefinition.FullTitle));
                hero.Occupation = Occupation.Wanderer;

                if (settlement != null)
                {
                    EnterSettlementAction.ApplyForCharacterOnly(hero, settlement);
                }

                hero.ChangeState(Hero.CharacterStates.Active);

                Debug.Print($"[AICompanion] Spawned {CompanionDefinition.FullTitle} at " +
                            $"{settlement?.Name}.");
            }
            catch (Exception ex)
            {
                // Never let a spawn failure take down the whole campaign — log and move on.
                Debug.Print($"[AICompanion] Failed to spawn companion: {ex}");
            }
        }
    }
}
