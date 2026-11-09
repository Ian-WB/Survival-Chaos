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

        /// <summary>
        /// Passed as the level to open every level gate - see
        /// SkillDefinition.IsAvailableAt. The default for callers with no level
        /// to give, which is how the pool behaved before skills had gates.
        /// </summary>
        public const int AnyLevel = int.MaxValue;

        /// <summary>Skills that still have picks remaining, whatever level they open at.</summary>
        public int AvailableCount
        {
            get
            {
                RefreshAvailable(AnyLevel);
                return availableBuffer.Count;
            }
        }

        /// <summary>How many times a given skill has been handed out.</summary>
        public int PicksTaken(SkillDefinition definition)
        {
            return definition != null && pickCounts.TryGetValue(definition, out int taken) ? taken : 0;
        }

        /// <summary>
        /// Whether a player at <paramref name="level"/> can take this skill
        /// next: the pool holds it, it has a pick left, and that pick is open
        /// at the level. False for null and for skills the pool does not hold.
        ///
        /// The same test a draw makes, asked again later. An offer is drawn
        /// and then sits on the ring, and a pick of the same skill from
        /// another offer can close it in the meantime.
        /// </summary>
        public bool CanTake(SkillDefinition definition, int level = AnyLevel)
        {
            return definition != null
                && pickCounts.TryGetValue(definition, out int taken)
                && Open(definition, taken, level);
        }

        /// <summary>
        /// Returns the next skill a player at <paramref name="level"/> can have
        /// and records the pick, or null when nothing is left. A pool containing
        /// an unlimited, ungated skill never returns null.
        /// </summary>
        public SkillDefinition Next(int level = AnyLevel)
        {
            RefreshAvailable(level);
            SkillDefinition chosen = TakeOne(availableBuffer);

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
        /// Only skills whose next pick is open at <paramref name="level"/> are
        /// drawn, and none in <paramref name="excluding"/> - the skills already
        /// on the offer a replacement is being drawn for.
        /// </summary>
        public List<SkillDefinition> Draw(
            int count,
            int level = AnyLevel,
            ICollection<SkillDefinition> excluding = null)
        {
            var drawn = new List<SkillDefinition>();

            if (count < 1)
            {
                return drawn;
            }

            // A copy, because entries are removed as they are drawn to keep the
            // set distinct - and the shared buffer is refreshed from the pool
            // rather than owned by this call.
            RefreshAvailable(level);
            var remaining = new List<SkillDefinition>(availableBuffer);

            if (excluding != null)
            {
                remaining.RemoveAll(excluding.Contains);
            }

            while (drawn.Count < count && remaining.Count > 0)
            {
                drawn.Add(TakeOne(remaining));
            }

            return drawn;
        }

        /// <summary>
        /// Draws a level-up's offer: up to <paramref name="count"/> distinct
        /// skills, chosen with a memory of what earlier offers showed. Like
        /// <see cref="Draw"/> it charges nothing against the pick limits.
        ///
        /// Two rules on top of the plain draw, added on 9 November 2026 when
        /// level-ups were halved and each offer came to matter twice as much:
        ///
        /// A gated pick that has just opened is always in the next offer. Slow
        /// Mo opening at level 5 used to wait on the dice, and a run could go
        /// several offers without seeing it.
        ///
        /// The rest of the offer leans toward whatever has been shown least,
        /// three to one. A lean rather than a rota, so the same skill can
        /// still turn up twice running.
        ///
        /// Only this method records what was shown. Draw is left as it was for
        /// replacements, which stand in for a pickup already on the ring.
        /// </summary>
        public List<SkillDefinition> DrawOffer(int count, int level = AnyLevel)
        {
            var drawn = new List<SkillDefinition>();

            if (count < 1)
            {
                return drawn;
            }

            RefreshAvailable(level);
            var remaining = new List<SkillDefinition>(availableBuffer);

            // In pool order, so which of two skills opening together comes
            // first is the list's choice rather than the dice's.
            for (int i = 0; i < remaining.Count && drawn.Count < count; i++)
            {
                SkillDefinition definition = remaining[i];

                if (definition.IsGated && !ShownAtThisStage(definition))
                {
                    drawn.Add(definition);
                    remaining.RemoveAt(i);
                    i--;
                }
            }

            while (drawn.Count < count && remaining.Count > 0)
            {
                drawn.Add(TakeLeastShown(remaining));
            }

            foreach (SkillDefinition definition in drawn)
            {
                timesShown[definition] = TimesShown(definition) + 1;
                shownAtStage[definition] = pickCounts[definition];
            }

            return drawn;
        }

        /// <summary>How many offers a skill has been part of, for the lean in <see cref="DrawOffer"/>.</summary>
        public int TimesShown(SkillDefinition definition)
        {
            return definition != null && timesShown.TryGetValue(definition, out int shown) ? shown : 0;
        }

        private readonly Dictionary<SkillDefinition, int> timesShown = new Dictionary<SkillDefinition, int>();

        /// <summary>The pick count each skill had when an offer last carried it.</summary>
        private readonly Dictionary<SkillDefinition, int> shownAtStage = new Dictionary<SkillDefinition, int>();

        /// <summary>How much likelier the least-shown skills are than the rest.</summary>
        private const int LeastShownWeight = 3;

        private bool ShownAtThisStage(SkillDefinition definition)
        {
            return shownAtStage.TryGetValue(definition, out int stage) && stage == pickCounts[definition];
        }

        /// <summary>
        /// Takes one entry, the least-shown ones weighted up. With every entry
        /// shown equally often the weights are equal and this is a plain draw.
        /// </summary>
        private SkillDefinition TakeLeastShown(List<SkillDefinition> from)
        {
            int least = int.MaxValue;

            foreach (SkillDefinition definition in from)
            {
                least = Math.Min(least, TimesShown(definition));
            }

            int total = 0;

            foreach (SkillDefinition definition in from)
            {
                total += TimesShown(definition) == least ? LeastShownWeight : 1;
            }

            int roll = selectIndex(total);
            if (roll < 0 || roll >= total)
            {
                roll = 0;
            }

            for (int i = 0; i < from.Count; i++)
            {
                roll -= TimesShown(from[i]) == least ? LeastShownWeight : 1;

                if (roll < 0)
                {
                    SkillDefinition chosen = from[i];
                    from.RemoveAt(i);
                    return chosen;
                }
            }

            // Unreachable: the weights sum to the total the roll is under.
            SkillDefinition last = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            return last;
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
        private SkillDefinition TakeOne(List<SkillDefinition> from)
        {
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

        private void RefreshAvailable(int level)
        {
            availableBuffer.Clear();

            foreach (SkillDefinition definition in skills)
            {
                if (Open(definition, pickCounts[definition], level))
                {
                    availableBuffer.Add(definition);
                }
            }
        }

        /// <summary>Whether the pick after <paramref name="taken"/> is within the limit and open at the level.</summary>
        private static bool Open(SkillDefinition definition, int taken, int level)
        {
            return (definition.IsUnlimited || taken < definition.MaxPicks)
                && definition.IsAvailableAt(taken, level);
        }
    }
}
