using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Shortens the wait between dashes.
    ///
    /// The dodging counterpart to Attack Speed. The dash is the only answer to
    /// the ram and the one move that arrives late, so how often it is there is
    /// worth as much to survival as how fast the ship goes.
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
