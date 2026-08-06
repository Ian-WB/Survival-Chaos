using System;
using System.Collections.Generic;

namespace SurvivalChaos
{
    /// <summary>
    /// Tracks which skills are still available in a run and picks the next one.
    ///
    /// Replaces the old hand-rolled logic in SkillSelect, which removed skills
    /// from a List as they were exhausted and could empty completely - after
    /// which every level-up silently fell through to a heal.
    /// </summary>
    public sealed class SkillPool
    {
        private readonly List<SkillDefinition> skills = new List<SkillDefinition>();
        private readonly Dictionary<SkillDefinition, int> pickCounts = new Dictionary<SkillDefinition, int>();
        private readonly List<SkillDefinition> availableBuffer = new List<SkillDefinition>();
        private readonly Func<int, int> selectIndex;

        /// <param name="definitions">
        /// Skills to draw from. Nulls and duplicates are ignored.
        /// </param>
        /// <param name="selectIndex">
        /// Chooses an index given the number of available skills. Defaults to
        /// UnityEngine.Random; tests supply a deterministic function.
        /// </param>
        public SkillPool(IEnumerable<SkillDefinition> definitions, Func<int, int> selectIndex = null)
        {
            this.selectIndex = selectIndex ?? (count => UnityEngine.Random.Range(0, count));

            if (definitions == null)
            {
                return;
            }

            foreach (SkillDefinition definition in definitions)
            {
                if (definition == null || pickCounts.ContainsKey(definition))
                {
                    continue;
                }

                skills.Add(definition);
                pickCounts.Add(definition, 0);
            }
        }

        /// <summary>Skills that still have picks remaining.</summary>
        public int AvailableCount
        {
            get
            {
                RefreshAvailable();
                return availableBuffer.Count;
            }
        }

        /// <summary>How many times a given skill has been handed out.</summary>
        public int PicksTaken(SkillDefinition definition)
        {
            return definition != null && pickCounts.TryGetValue(definition, out int taken) ? taken : 0;
        }

        /// <summary>
        /// Returns the next skill and records the pick, or null when nothing is
        /// left. A pool containing an unlimited skill never returns null.
        /// </summary>
        public SkillDefinition Next()
        {
            RefreshAvailable();

            if (availableBuffer.Count == 0)
            {
                return null;
            }

            int index = selectIndex(availableBuffer.Count);
            if (index < 0 || index >= availableBuffer.Count)
            {
                index = 0;
            }

            SkillDefinition chosen = availableBuffer[index];
            pickCounts[chosen] = pickCounts[chosen] + 1;
            return chosen;
        }

        private void RefreshAvailable()
        {
            availableBuffer.Clear();

            foreach (SkillDefinition definition in skills)
            {
                if (definition.IsUnlimited || pickCounts[definition] < definition.MaxPicks)
                {
                    availableBuffer.Add(definition);
                }
            }
        }
    }
}
