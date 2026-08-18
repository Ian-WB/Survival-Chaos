using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Advances the player's shot pattern. Four picks walk the whole progression:
    /// double, triple, sextuple, then shots to the rear.
    /// </summary>
    [CreateAssetMenu(fileName = "ShotUpgrade", menuName = "Survival Chaos/Skills/Shot Upgrade")]
    public sealed class ShotUpgradeSkill : SkillDefinition
    {
        [SerializeField]
        [Tooltip("Banner text per stage, in pick order.")]
        private string[] stageNames =
        {
            "Double Shot!",
            "Triple Shot!",
            "SexTUPLO Shot!",
            "Back Shot!"
        };

        [SerializeField]
        [Tooltip("Pickup text per stage, in pick order. Left empty, the banner names above " +
                 "are used.")]
        private string[] stagePickupNames =
        {
            "Double Shot",
            "Triple Shot",
            "SexTUPLO Shot",
            "Back Shot"
        };

        public override string GetDisplayName(int picksTaken)
        {
            return Stage(stageNames, picksTaken) ?? base.GetDisplayName(picksTaken);
        }

        /// <summary>
        /// The stage the player would get next, without the banner's exclamation.
        ///
        /// Falls through to the banner names rather than to the plain display
        /// name: "More Shots!" is accurate for every stage and therefore useless
        /// on a pickup, where the whole question is which one this is.
        /// </summary>
        public override string GetPickupName(int picksTaken)
        {
            return Stage(stagePickupNames, picksTaken) ?? GetDisplayName(picksTaken);
        }

        /// <summary>
        /// The entry for a pick count, or null when the list cannot answer.
        ///
        /// Clamped rather than wrapped, so the last stage keeps naming itself
        /// once the progression is spent instead of starting over at "Double
        /// Shot" - which would read as the upgrade having been undone.
        /// </summary>
        private static string Stage(string[] names, int picksTaken)
        {
            if (names == null || names.Length == 0)
            {
                return null;
            }

            return names[Mathf.Clamp(picksTaken - 1, 0, names.Length - 1)];
        }

        public override void Apply(ISkillTarget target)
        {
            target.UpgradeShotPattern();
        }
    }
}
