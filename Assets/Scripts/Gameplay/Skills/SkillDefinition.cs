using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Base type for a level-up reward. Concrete skills are authored as assets,
    /// so adding or retuning a skill is a content change rather than a code change.
    /// </summary>
    public abstract class SkillDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Shown on the level-up banner.")]
        private string displayName = "Skill";

        [SerializeField]
        [Tooltip("How many times this skill can be picked in a run. 0 means unlimited.")]
        private int maxPicks = 1;

        [SerializeField]
        [ColorUsage(showAlpha: false, hdr: true)]
        [Tooltip("Colour of this skill's pickup on the ring. HDR - values above 1 bloom, " +
                 "which is what makes it read as a glowing object rather than a painted one.")]
        private Color pickupColor = new Color(2f, 1.2f, 0.2f);

        public string DisplayName => displayName;

        public int MaxPicks => maxPicks;

        /// <summary>
        /// What colour this skill's pickup glows.
        ///
        /// Colour is the only thing telling one pickup from another in flight -
        /// there is no room for a label on an object the size of a bullet, and
        /// no time to read one while dodging. Authored per skill asset so
        /// retuning the palette stays a content change.
        /// </summary>
        public Color PickupColor => pickupColor;

        /// <summary>A skill with no pick limit never leaves the pool.</summary>
        public bool IsUnlimited => maxPicks <= 0;

        /// <summary>
        /// Banner text for a pick. Takes the number of times this skill has now
        /// been taken so multi-stage skills can name each stage, without the
        /// asset holding run state - ScriptableObjects outlive a play session.
        /// </summary>
        public virtual string GetDisplayName(int picksTaken)
        {
            return displayName;
        }

        public abstract void Apply(ISkillTarget target);
    }
}
