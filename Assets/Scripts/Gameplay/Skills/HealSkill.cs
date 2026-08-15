using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Restores health.
    ///
    /// No longer offered. SkillSelect filters this type out of its pool, because
    /// healing as one of three pickups meant taking it forfeited two upgrades and
    /// taking an upgrade forfeited the heal - a trade between a reward for
    /// killing well and the thing you need when you are not. PickupSpawner drops
    /// health on its own level cadence instead, outside any offer.
    ///
    /// Kept as a type rather than deleted: it is what makes the filter possible,
    /// and the asset is still a valid way to describe "heal by this much" if
    /// something later wants to grant one directly.
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
