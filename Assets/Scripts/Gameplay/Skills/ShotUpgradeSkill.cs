using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Hydra: more rounds a volley. Three picks walk the whole progression,
    /// two rounds, then four, then eight (Ian, 15 November 2026: a middle
    /// stage, a last stage of eight, and a name of its own in place of
    /// "Double Shot" and "Sextuple Shot"). The last waits for level 14, near
    /// the end of a run; the gates and the banners are in the asset, the
    /// rows each stage fires in Player.ForwardPattern.
    ///
    /// A StagedSkill since then. It used to carry its own copy of the two name
    /// lists, under the same field names, so the asset kept its data across
    /// the move.
    ///
    /// There was a fourth, Back Shot, which added four shots the other way round
    /// the ring. It was the only upgrade that changed what the gun does rather
    /// than how much of it there is, and it has been retired along with the
    /// backward firing path in Player.
    /// </summary>
    [CreateAssetMenu(fileName = "ShotUpgrade", menuName = "Survival Chaos/Skills/Shot Upgrade")]
    public sealed class ShotUpgradeSkill : StagedSkill
    {
        public override void Apply(ISkillTarget target)
        {
            target.UpgradeShotPattern();
        }
    }
}
