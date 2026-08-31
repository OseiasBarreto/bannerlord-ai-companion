using System;
using System.Linq;
using AICompanion.Config;
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
                    ModLog.Info($"Companion already exists (StringId={existing.StringId}), skipping spawn.");
                    FixAgeIfStillAChild(existing);
                    return;
                }

                var template = MBObjectManager.Instance.GetObject<CharacterObject>(
                    CompanionDefinition.CharacterTemplateStringId);
                if (template != null)
                {
                    ModLog.Info($"Found configured template '{CompanionDefinition.CharacterTemplateStringId}'.");
                }
                else
                {
                    // Fallback: any vanilla wanderer template, in case the configured id
                    // doesn't exist in this game version (or was replaced by another mod).
                    ModLog.Info($"Configured template '{CompanionDefinition.CharacterTemplateStringId}' " +
                                "not found — falling back to any Occupation.Wanderer template. " +
                                $"Total CharacterObject count: {CharacterObject.All.Count}.");
                    template = CharacterObject.All
                        .FirstOrDefault(c => c.Occupation == Occupation.Wanderer);
                }

                if (template == null)
                {
                    ModLog.Error("No wanderer template found at all — cannot spawn Cláudio. " +
                                 "This usually means another mod removed every wanderer-occupation " +
                                 "CharacterObject before this behavior ran.");
                    return;
                }

                ModLog.Info($"Using template '{template.StringId}' to create the companion.");

                // MBRandom.RandomInt() with no bounds returns a value across the whole int
                // range, which produced a nonsensical birth date (he spawned as an infant).
                // HeroComesOfAge (18 in the base game) is the youngest a hero counts as a full
                // adult, so roll within a plausible young-adult-to-veteran band above that.
                var age = MBRandom.RandomInt(18, 40);
                ModLog.Info($"Rolled age {age} for the companion.");
                var hero = HeroCreator.CreateSpecialHero(
                    template, null, Clan.PlayerClan, null, age);

                hero.StringId = CompanionDefinition.HeroStringId;
                hero.SetName(new TaleWorlds.Localization.TextObject(CompanionDefinition.Name),
                             new TaleWorlds.Localization.TextObject(CompanionDefinition.FullTitle));
                hero.SetNewOccupation(Occupation.Wanderer);
                hero.ChangeState(Hero.CharacterStates.Active);

                // No going to a tavern to find him — he starts right there in the player's
                // own party, same as any companion recruited via AddCompanionAction.
                AddCompanionAction.Apply(Clan.PlayerClan, hero);

                InformationManager.DisplayMessage(new InformationMessage(
                    $"{CompanionDefinition.FullTitle} se junta a você desde o primeiro dia " +
                    "de jornada.", Colors.Yellow));

                ModLog.Info($"Spawned {CompanionDefinition.FullTitle} directly into the player's party.");
            }
            catch (Exception ex)
            {
                // Never let a spawn failure take down the whole campaign — log and move on.
                ModLog.Error("Failed to spawn companion", ex);
            }
        }

        /// <summary>
        /// One-time repair for saves created before the age bug fix: earlier builds passed
        /// MBRandom.RandomInt() with no bounds as the companion's age, which spawned him as an
        /// infant. Detects that state and backdates his birthday so he's a proper adult.
        /// </summary>
        private static void FixAgeIfStillAChild(Hero companion)
        {
            if (!companion.IsChild)
            {
                return;
            }

            var age = MBRandom.RandomInt(18, 40);
            companion.SetBirthDay(CampaignTime.Now - CampaignTime.Years(age));

            InformationManager.DisplayMessage(new InformationMessage(
                $"{CompanionDefinition.Name} de repente cresceu — parece que o tempo passou " +
                "diferente pra ele.", Colors.Yellow));

            ModLog.Info($"Fixed companion age: was a child, backdated birthday to age {age}.");
        }
    }
}
