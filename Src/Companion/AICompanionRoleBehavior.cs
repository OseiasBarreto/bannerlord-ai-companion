using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace AICompanion.Companion
{
    /// <summary>
    /// Tracks which hero currently holds "Minha Mão" — the AI-companion role any hero in the
    /// player's clan can be promoted into via dialogue (see ChatDialogBehavior), replacing the
    /// old fixed Cláudio character entirely. Only the hero's StringId is stored: the Hero object
    /// itself is already persisted by the base game's own save system.
    /// </summary>
    public sealed class AICompanionRoleBehavior : CampaignBehaviorBase
    {
        public static AICompanionRoleBehavior Instance { get; private set; }

        [SaveableField(1)]
        private string _holderId = string.Empty;

        public AICompanionRoleBehavior() => Instance = this;

        public Hero CurrentHolder => string.IsNullOrEmpty(_holderId)
            ? null
            : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == _holderId);

        public bool IsHolder(Hero hero) => hero != null && !string.IsNullOrEmpty(_holderId) &&
                                            hero.StringId == _holderId;

        public bool HasHolder => !string.IsNullOrEmpty(_holderId) && CurrentHolder != null;

        public void Promote(Hero hero)
        {
            _holderId = hero?.StringId ?? string.Empty;
            Config.ModLog.Info($"AICompanionRoleBehavior: Minha Mão is now {hero?.Name} " +
                                $"(StringId={_holderId}).");
        }

        public void Clear()
        {
            Config.ModLog.Info($"AICompanionRoleBehavior: role cleared (was {_holderId}).");
            _holderId = string.Empty;
        }

        public override void RegisterEvents()
        {
            // Nothing to react to here — HandOfTheKingBehavior and the opinion/memory
            // behaviors already watch for the holder leaving/dying via their own daily ticks.
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("AICompanion_HolderId", ref _holderId);
            _holderId ??= string.Empty;
        }
    }
}
