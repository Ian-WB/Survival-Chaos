using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Pickups within reach drift to the player: salvage and upgrades alike.
    ///
    /// Fetching a pickup means matching its height as well as its place on the
    /// ring, and the last part of that approach is usually the part flown through
    /// whatever is shooting. This shortens it. It cannot take a whole offer: the
    /// pickups of one are placed far wider apart than the widest reach.
    /// </summary>
    [CreateAssetMenu(fileName = "Magnet", menuName = "Survival Chaos/Skills/Magnet")]
    public sealed class MagnetSkill : StagedSkill
    {
        public override void Apply(ISkillTarget target)
        {
            target.ExtendMagnet();
        }
    }
}
