using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// A skill that names each of its picks, so the pickup says which stage is
    /// on offer rather than repeating a name that was true of all of them.
    ///
    /// The four upgrades added in September 2026 use this, and Shot Upgrade
    /// (Hydra) since 15 November 2026; it had the same two lists written into
    /// it before.
    /// </summary>
    public abstract class StagedSkill : SkillDefinition
    {
        [SerializeField]
        [Tooltip("Banner text per pick, in pick order, used as written: write the pick's numeral " +
                 "into it. Only for a banner that says more than the name, as Slow Mo's teaches " +
                 "its button. Left empty, the banner is the pickup name with its numeral.")]
        private string[] stageNames = new string[0];

        [SerializeField]
        [Tooltip("Pickup text per pick, in pick order, without the numeral: it is added. Left " +
                 "empty, the Pickup Name above is used.")]
        private string[] stagePickupNames = new string[0];

        public override string GetDisplayName(int picksTaken)
        {
            return Stage(stageNames, picksTaken) ?? base.GetDisplayName(picksTaken);
        }

        public override string GetPickupName(int picksTaken)
        {
            string stage = Stage(stagePickupNames, picksTaken);
            return stage != null ? Numbered(stage, picksTaken) : base.GetPickupName(picksTaken);
        }

        /// <summary>
        /// The entry for a pick count, or null when the list cannot answer.
        /// Clamped rather than wrapped, so a spent progression keeps naming its
        /// last stage instead of starting over.
        /// </summary>
        private static string Stage(string[] names, int picksTaken)
        {
            if (names == null || names.Length == 0)
            {
                return null;
            }

            return names[Mathf.Clamp(picksTaken - 1, 0, names.Length - 1)];
        }
    }
}
