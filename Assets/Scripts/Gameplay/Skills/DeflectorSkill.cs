using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// A charge that takes one hit in place of the hull and comes back after a
    /// stretch without being hit. The first pick grants it; later picks shorten
    /// the wait. See <see cref="DeflectorCharge"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Deflector", menuName = "Survival Chaos/Skills/Deflector")]
    public sealed class DeflectorSkill : StagedSkill
    {
        public override void Apply(ISkillTarget target)
        {
            target.UpgradeDeflector();
        }
    }
}
