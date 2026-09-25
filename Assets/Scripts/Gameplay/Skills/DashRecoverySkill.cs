using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Shortens the wait between dashes.
    ///
    /// The dodging counterpart to Attack Speed. The dash is the only answer to
    /// the ram and the one move that arrives late, so how often it is there is
    /// worth as much to survival as how fast the ship goes.
    ///
    /// Each pick takes 1.5 seconds off a 10 second wait, so three bring it to
    /// 5.5 - see Player's Dash Recovery fields. Both were a tenth of that until
    /// 25 September 2026, when the dash went from every second to every ten.
    /// </summary>
    [CreateAssetMenu(fileName = "DashRecovery", menuName = "Survival Chaos/Skills/Dash Recovery")]
    public sealed class DashRecoverySkill : StagedSkill
    {
        public override void Apply(ISkillTarget target)
        {
            target.QuickenDash();
        }
    }
}
