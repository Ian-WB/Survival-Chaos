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
            SkillDefinition chosen = TakeOne(availableBuffer, refresh: true);

            if (chosen != null)
            {
                RecordPick(chosen);
            }

            return chosen;
        }

        /// <summary>
        /// Draws up to <paramref name="count"/> distinct skills <em>without</em>
        /// recording anything.
        ///
        /// This exists because an offer is not a pick. When a level-up puts three
        /// pickups on the ring the player collects one and forfeits the rest, so
        /// charging all three against their limits would burn the pool three
        /// times as fast - and a one-pick skill would be spent by an offer the
        /// player never touched. Call <see cref="RecordPick"/> when one is
        /// actually taken.
        ///
        /// Fewer than <paramref name="count"/> come back when the pool cannot
        /// field that many distinct skills; an offer of two is still an offer.
        /// </summary>
        public List<SkillDefinition> Draw(int count)
        {
            var drawn = new List<SkillDefinition>();

            if (count < 1)
            {
                return drawn;
            }

            // A copy, because entries are removed as they are drawn to keep the
            // set distinct - and the shared buffer is refreshed from the pool
            // rather than owned by this call.
            RefreshAvailable();
            var remaining = new List<SkillDefinition>(availableBuffer);

            while (drawn.Count < count && remaining.Count > 0)
            {
                drawn.Add(TakeOne(remaining, refresh: false));
            }

            return drawn;
        }

        /// <summary>
        /// Charges one pick against a skill's limit. Safe to call with null, and
        /// with a skill this pool does not hold - both do nothing.
        /// </summary>
        public void RecordPick(SkillDefinition definition)
        {
            if (definition != null && pickCounts.ContainsKey(definition))
            {
                pickCounts[definition] = pickCounts[definition] + 1;
            }
        }

        /// <summary>
        /// Chooses an entry through <see cref="selectIndex"/> and removes it from
        /// the list it came from, clamping a selector that returns out of range.
        /// </summary>
        private SkillDefinition TakeOne(List<SkillDefinition> from, bool refresh)
        {
            if (refresh)
            {
                RefreshAvailable();
            }

            if (from.Count == 0)
            {
                return null;
            }

            int index = selectIndex(from.Count);
            if (index < 0 || index >= from.Count)
            {
                index = 0;
            }

            SkillDefinition chosen = from[index];

            // The shared buffer is rebuilt on every refresh, so removing from it
            // here costs nothing and keeps this one method serving both callers.
            from.RemoveAt(index);
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
