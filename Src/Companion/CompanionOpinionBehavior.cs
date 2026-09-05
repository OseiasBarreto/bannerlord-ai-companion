using System.Collections.Generic;
using AICompanion.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace AICompanion.Companion
{
    /// <summary>
    /// Tracks how the current "Minha Mão" holder feels about the player over time, per hero
    /// (StringId) — so loyalty belongs to the person, not the office: promoting someone new
    /// starts fresh at neutral trust, and an old holder's opinion just sits unused once they're
    /// no longer the holder. Rides on the player's own personality traits (Honor, Mercy,
    /// Generosity, Calculating), which the base game already updates from real choices — that's
    /// the actual "memory" of what the player did, not something this mod tracks by hand.
    /// </summary>
    public sealed class CompanionOpinionBehavior : CampaignBehaviorBase
    {
        private const int BetrayalThreshold = -60;
        private const int MaxOpinion = 100;
        private const int MinOpinion = -100;
        private const int StartingOpinion = 20; // modestly trusting — hasn't seen much of you yet.

        [SaveableField(1)]
        private Dictionary<string, int> _opinionByHero = new Dictionary<string, int>();

        private static CompanionOpinionBehavior _instance;

        private static string CurrentHeroId => AICompanionRoleBehavior.Instance?.CurrentHolder?.StringId;

        public static int Opinion
        {
            get
            {
                var heroId = CurrentHeroId;
                if (heroId == null || _instance == null)
                {
                    return 0;
                }

                return _instance._opinionByHero.TryGetValue(heroId, out var value) ? value : StartingOpinion;
            }
        }

        public override void RegisterEvents()
        {
            _instance = this;
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("AICompanion_OpinionByHero", ref _opinionByHero);
            _opinionByHero ??= new Dictionary<string, int>();
        }

        private void DailyTick()
        {
            var heroId = CurrentHeroId;
            var playerHero = Hero.MainHero;
            if (heroId == null || playerHero == null)
            {
                return;
            }

            if (!_opinionByHero.TryGetValue(heroId, out var opinion))
            {
                opinion = StartingOpinion;
            }

            var previous = opinion;
            var target = ComputeTargetOpinion(playerHero);
            if (opinion < target)
            {
                opinion = System.Math.Min(opinion + 1, MaxOpinion);
            }
            else if (opinion > target)
            {
                opinion = System.Math.Max(opinion - 1, MinOpinion);
            }

            _opinionByHero[heroId] = opinion;

            if (opinion != previous)
            {
                ModLog.Info($"Opinion (hero {heroId}) drifted {previous} -> {opinion} (target {target}).");
            }

            if (opinion <= BetrayalThreshold)
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
            var hero = AICompanionRoleBehavior.Instance?.CurrentHolder;
            if (hero == null || Clan.PlayerClan == null)
            {
                ModLog.Error("Opinion hit betrayal threshold but no current holder or player " +
                             "clan was null — cannot process departure.");
                return;
            }

            ModLog.Info($"Opinion for {hero.Name} crossed betrayal threshold — leaving.");

            InformationManager.DisplayMessage(new InformationMessage(
                $"{hero.Name} não reconhece mais quem você se tornou. Ele reúne suas coisas e " +
                "parte, sem olhar para trás.", Colors.Red));

            AICompanionRoleBehavior.Instance.Clear();
            RemoveCompanionAction.ApplyByFire(Clan.PlayerClan, hero);
        }

        /// <summary>
        /// Short line describing where the relationship stands, meant to be folded into the
        /// chat system prompt so the AI's tone actually reflects it instead of staying static.
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
