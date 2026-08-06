using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>Cuts the delay between the player's shots.</summary>
    [CreateAssetMenu(fileName = "AttackSpeed", menuName = "Survival Chaos/Skills/Attack Speed")]
    public sealed class AttackSpeedSkill : SkillDefinition
    {
        public override void Apply(ISkillTarget target)
        {
            target.IncreaseAttackSpeed();
        }
    }
}
