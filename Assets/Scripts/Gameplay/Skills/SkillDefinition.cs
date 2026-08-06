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

        public string DisplayName => displayName;

        public int MaxPicks => maxPicks;

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
