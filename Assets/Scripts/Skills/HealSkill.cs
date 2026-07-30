using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Restores health. Authored with no pick limit so it acts as the fallback
    /// once every other skill is exhausted - the pool then never runs dry.
    /// </summary>
    [CreateAssetMenu(fileName = "Heal", menuName = "Survival Chaos/Skills/Heal")]
    public sealed class HealSkill : SkillDefinition
    {
        [SerializeField]
        private int amount = 5;

        public override void Apply(ISkillTarget target)
        {
            target.Heal(amount);
        }
    }
}
