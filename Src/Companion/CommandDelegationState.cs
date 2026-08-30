using TaleWorlds.CampaignSystem;

namespace AICompanion.Companion
{
    /// <summary>
    /// Tracks whether the player has asked Cláudio to take command of the troops in the next
    /// battle. Set from the dialog ("Lidere as tropas!"); read by
    /// <see cref="AICompanion.Mission.CompanionCommandMissionBehavior"/> once a battle starts.
    /// It's a one-shot request, not a permanent setting — it resets after each battle so the
    /// player decides fresh every time.
    /// </summary>
    public sealed class CommandDelegationState : CampaignBehaviorBase
    {
        public static bool CommandDelegated { get; set; }

        public override void RegisterEvents()
        {
            CampaignEvents.OnMissionEndedEvent.AddNonSerializedListener(this, (_) => CommandDelegated = false);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Deliberately not persisted — it's a per-battle request, meaningless across saves.
        }
    }
}
