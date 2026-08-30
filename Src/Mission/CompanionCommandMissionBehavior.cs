using System.Linq;
using AICompanion.Companion;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AICompanion.Mission
{
    /// <summary>
    /// When the player has asked Cláudio to lead the troops (via the "Lidere as tropas!"
    /// dialog option, tracked by <see cref="CommandDelegationState"/>), makes him the AI
    /// captain of the player's largest infantry-ish formation for that battle — the closest
    /// equivalent Bannerlord's Formation system has to "hand a companion the command".
    /// </summary>
    public sealed class CompanionCommandMissionBehavior : MissionBehavior
    {
        private bool _hasAssignedCommand;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);

            if (_hasAssignedCommand || !CommandDelegationState.CommandDelegated)
            {
                return;
            }

            var isCompanion = agent.Character is TaleWorlds.CampaignSystem.CharacterObject character &&
                               character.HeroObject?.StringId == CompanionDefinition.HeroStringId;
            if (!isCompanion)
            {
                return;
            }

            AssignCommand(agent);
        }

        private void AssignCommand(Agent companionAgent)
        {
            var playerFormations = companionAgent.Team?.FormationsIncludingEmpty
                .Where(f => f != null && f.CountOfUnits > 0)
                .ToList();

            var targetFormation = playerFormations?
                .OrderByDescending(f => f.CountOfUnits)
                .FirstOrDefault();

            if (targetFormation == null)
            {
                return;
            }

            targetFormation.Captain = companionAgent;
            _hasAssignedCommand = true;

            InformationManager.DisplayMessage(new InformationMessage(
                $"{CompanionDefinition.Name} assume o comando de {targetFormation.CountOfUnits} " +
                "homens nesta batalha.", Colors.Yellow));
        }
    }
}
