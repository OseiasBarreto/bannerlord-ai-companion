using System.Linq;
using AICompanion.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace AICompanion.Companion
{
    /// <summary>
    /// Tracks how Cláudio feels about the player over time, so he isn't unconditionally loyal —
    /// he forms his own read on who you're becoming and can eventually walk away over it. Rather
    /// than hooking every possible "moral" decision by hand, this rides on the player's own
    /// personality traits (Honor, Mercy, Generosity, Calculating), which the base game already
    /// updates from choices made across the campaign — that's the actual memory of what you did.
    /// </summary>
    public sealed class CompanionOpinionBehavior : CampaignBehaviorBase
    {
        private const int BetrayalThreshold = -60;
        private const int MaxOpinion = 100;
        private const int MinOpinion = -100;

        private int _opinion = 20; // starts modestly trusting — he hasn't seen much of you yet.
        private bool _hasLeft;

        private static CompanionOpinionBehavior _instance;

        public static int Opinion => _instance?._opinion ?? 0;
        public static bool HasLeft => _instance?._hasLeft ?? false;

        public override void RegisterEvents()
        {
            _instance = this;
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("AICompanion_Opinion", ref _opinion);
            dataStore.SyncData("AICompanion_OpinionHasLeft", ref _hasLeft);
        }

        private void DailyTick()
        {
            if (_hasLeft)
            {
                return;
            }

            var hero = Hero.MainHero;
            if (hero == null)
            {
                return;
            }

            var previous = _opinion;
            var target = ComputeTargetOpinion(hero);
            if (_opinion < target)
            {
                _opinion = System.Math.Min(_opinion + 1, MaxOpinion);
            }
            else if (_opinion > target)
            {
                _opinion = System.Math.Max(_opinion - 1, MinOpinion);
            }

            if (_opinion != previous)
            {
                ModLog.Info($"Opinion drifted {previous} -> {_opinion} (target {target}).");
            }

            if (_opinion <= BetrayalThreshold)
            {
                LeaveOverDisapproval();
            }
        }

        private static int ComputeTargetOpinion(Hero hero)
        {
            // Each trait usually sits in roughly [-2, 2]; scale so the target lands in [-100, 100].
            var score = hero.GetTraitLevel(DefaultTraits.Honor)
                        + hero.GetTraitLevel(DefaultTraits.Mercy)
                        + hero.GetTraitLevel(DefaultTraits.Generosity)
                        - hero.GetTraitLevel(DefaultTraits.Calculating);
            return MBMath.ClampInt(score * 12, MinOpinion, MaxOpinion);
        }

        private void LeaveOverDisapproval()
        {
            var hero = Hero.AllAliveHeroes
                .FirstOrDefault(h => h.StringId == CompanionDefinition.HeroStringId);
            if (hero == null || Clan.PlayerClan == null)
            {
                ModLog.Error("Opinion hit betrayal threshold but companion hero or player clan " +
                             "was null — cannot process departure.");
                return;
            }

            ModLog.Info($"Opinion ({_opinion}) crossed betrayal threshold — companion is leaving.");
            _hasLeft = true;

            InformationManager.DisplayMessage(new InformationMessage(
                $"{CompanionDefinition.Name} não reconhece mais quem você se tornou. Ele reúne " +
                "suas coisas e parte, sem olhar para trás.", Colors.Red));

            RemoveCompanionAction.ApplyByFire(Clan.PlayerClan, hero);
        }

        /// <summary>
        /// Short line describing where the relationship stands, meant to be folded into the
        /// chat system prompt so Cláudio's tone actually reflects it instead of staying static.
        /// </summary>
        public static string DescribeForPrompt()
        {
            var opinion = Opinion;
            if (opinion >= 60)
            {
                return "Ele confia profundamente em você e veste a camisa das suas causas.";
            }
            if (opinion >= 20)
            {
                return "Ele é leal, mas não é bajulador — discorda abertamente quando acha " +
                       "que você está errado.";
            }
            if (opinion >= -19)
            {
                return "Ele ainda está avaliando que tipo de líder você está se tornando; " +
                       "nem totalmente a favor, nem contra.";
            }
            if (opinion > BetrayalThreshold)
            {
                return "Ele está visivelmente desconfiado e incomodado com o rumo que você " +
                       "está tomando, e não esconde isso nas conversas.";
            }
            return "Ele está à beira de ir embora — só a lealdade que sobrou o mantém por perto.";
        }
    }
}
