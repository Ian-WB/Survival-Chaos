using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>Raises the player's maximum health.</summary>
    [CreateAssetMenu(fileName = "MaxHealth", menuName = "Survival Chaos/Skills/Max Health")]
    public sealed class MaxHealthSkill : SkillDefinition
    {
        [SerializeField]
        private int amount = 20;

        public override void Apply(ISkillTarget target)
        {
            target.AddMaxHealth(amount);
        }
    }
}
