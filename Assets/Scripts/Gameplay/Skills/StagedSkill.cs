using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// A skill that names each of its picks, so the pickup says which stage is
    /// on offer rather than repeating a name that was true of all of them.
    ///
    /// The four upgrades added in September 2026 use this. Shot Upgrade has the
    /// same two lists written into it and predates this class; it was left alone
    /// because moving its fields to a base class is a change to a shipped asset
    /// for no difference in play.
    /// </summary>
    public abstract class StagedSkill : SkillDefinition
    {
        [SerializeField]
        [Tooltip("Banner text per pick, in pick order. The last entry repeats for any pick past " +
                 "the end. Left empty, the Display Name above is used.")]
        private string[] stageNames = new string[0];

        [SerializeField]
        [Tooltip("Pickup text per pick, in pick order. Left empty, the Pickup Name above is used.")]
        private string[] stagePickupNames = new string[0];

        public override string GetDisplayName(int picksTaken)
        {
            return Stage(stageNames, picksTaken) ?? base.GetDisplayName(picksTaken);
        }

        public override string GetPickupName(int picksTaken)
        {
            return Stage(stagePickupNames, picksTaken) ?? base.GetPickupName(picksTaken);
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
