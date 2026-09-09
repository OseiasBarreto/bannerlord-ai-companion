using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace AICompanion.Companion
{
    /// <summary>
    /// Executes the "vá patrulhar X" / "volte pra mim" orders the player gives through chat to
    /// whoever currently holds "Minha Mão". Must only ever run on the main thread (called from
    /// ChatVM's main-thread queue, same as UI text updates) — it creates/moves MobileParty and
    /// Hero objects, which aren't safe to touch off the background thread the API response
    /// comes back on. Once away, the holder keeps the role — talking to them again (walking up
    /// to their patrol party, same as meeting any other lord party) still opens the chat, since
    /// ChatDialogBehavior's trigger is keyed to the role holder's StringId, not party membership.
    /// </summary>
    public static class CompanionOrders
    {
        private const string PatrolPartyStringId = "aicompanion_patrol";

        private static Hero Holder => AICompanionRoleBehavior.Instance?.CurrentHolder;

        public static bool IsAwayFromParty
        {
            get
            {
                var hero = Holder;
                return hero?.PartyBelongedTo != null && hero.PartyBelongedTo != MobileParty.MainParty;
            }
        }

        /// <summary>
        /// locationTag is whatever the model put after "[PATRULHAR: ...]" — either a single
        /// settlement name, "AQUI" (the player's current position), or "NomeX;NomeZ" for a
        /// patrol route between two settlements (we use their midpoint as the patrol center).
        /// Returns a short status line to log; null means success.
        /// </summary>
        public static string TryStartPatrol(string locationTag)
        {
            try
            {
                var hero = Holder;
                if (hero == null)
                {
                    return "TryStartPatrol: no current Minha Mão holder.";
                }

                if (IsAwayFromParty)
                {
                    return "TryStartPatrol: aborted, already away from the party.";
                }

                if (!TryResolvePosition(locationTag, out var position, out var homeSettlement))
                {
                    return $"TryStartPatrol: could not resolve location '{locationTag}'.";
                }

                // The vanilla-correct way to detach a companion into their own AI-led party —
                // confirmed via reflection (the alternatives, ApplyByFire/AfterQuest/ByDeath,
                // don't fit; this doesn't remove them from the clan, just from riding with us).
                RemoveCompanionAction.ApplyByByTurningToLord(Hero.MainHero.Clan, hero);

                var party = LordPartyComponent.CreateLordParty(
                    PatrolPartyStringId,
                    hero,
                    new CampaignVec2(position, true),
                    1f,
                    homeSettlement ?? MobileParty.MainParty.HomeSettlement,
                    hero);

                if (party == null)
                {
                    return "TryStartPatrol: CreateLordParty returned null.";
                }

                if (homeSettlement != null)
                {
                    party.SetMovePatrolAroundSettlement(homeSettlement, MobileParty.NavigationType.Default, false);
                }
                else
                {
                    party.SetMovePatrolAroundPoint(new CampaignVec2(position, true), MobileParty.NavigationType.Default);
                }

                Config.ModLog.Info($"TryStartPatrol: {hero.Name} now patrolling around '{locationTag}'.");
                return null;
            }
            catch (Exception ex)
            {
                Config.ModLog.Error("TryStartPatrol threw", ex);
                return $"TryStartPatrol threw: {ex.Message}";
            }
        }

        public static string TryReturnToParty()
        {
            try
            {
                var hero = Holder;
                if (hero == null)
                {
                    return "TryReturnToParty: no current Minha Mão holder.";
                }

                if (!IsAwayFromParty)
                {
                    return "TryReturnToParty: aborted, not away.";
                }

                var awayParty = hero.PartyBelongedTo;
                DestroyPartyAction.Apply(null, awayParty);
                // Same call used when a hero is first recruited as a companion — re-run it now
                // that they no longer lead their own party.
                AddCompanionAction.Apply(Hero.MainHero.Clan, hero);

                Config.ModLog.Info($"TryReturnToParty: {hero.Name} rejoined the player's party.");
                return null;
            }
            catch (Exception ex)
            {
                Config.ModLog.Error("TryReturnToParty threw", ex);
                return $"TryReturnToParty threw: {ex.Message}";
            }
        }

        private static bool TryResolvePosition(string locationTag, out Vec2 position, out Settlement settlement)
        {
            position = default;
            settlement = null;

            if (string.IsNullOrWhiteSpace(locationTag))
            {
                return false;
            }

            var tag = locationTag.Trim();

            if (tag.Equals("AQUI", StringComparison.OrdinalIgnoreCase))
            {
                position = MobileParty.MainParty.GetPosition2D;
                return true;
            }

            var parts = tag.Split(';');
            if (parts.Length == 2)
            {
                var from = FindSettlement(parts[0]);
                var to = FindSettlement(parts[1]);
                if (from != null && to != null)
                {
                    position = (from.GetPosition2D + to.GetPosition2D) * 0.5f;
                    return true;
                }
                // Fall through to treat the whole tag as a single (imperfect) name if that failed.
            }

            settlement = FindSettlement(tag);
            if (settlement != null)
            {
                position = settlement.GetPosition2D;
                return true;
            }

            return false;
        }

        private static Settlement FindSettlement(string name)
        {
            var needle = name.Trim();
            return Settlement.All.FirstOrDefault(s =>
                s.Name.ToString().IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
