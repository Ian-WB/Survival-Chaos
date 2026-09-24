using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Lets every round the player fires pass through one more enemy.
    ///
    /// Aimed at the end of the waves, where the ships come in lines along the
    /// ring and a round that stops at the first one leaves the rest to the next
    /// volley. The boss's parts still stop every round - see ShootScript.Land.
    /// </summary>
    [CreateAssetMenu(fileName = "PiercingRounds", menuName = "Survival Chaos/Skills/Piercing Rounds")]
    public sealed class PiercingRoundsSkill : StagedSkill
    {
        public override void Apply(ISkillTarget target)
        {
            target.AddPierce();
        }
    }
}
