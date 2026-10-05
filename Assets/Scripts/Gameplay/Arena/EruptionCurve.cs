using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// How far the volcano has got, from 0 to 1, as arithmetic on the run's
    /// clock. <see cref="Eruption"/> turns the number into light and smoke.
    ///
    /// Three parts. A resting level that climbs across the run and stops
    /// short of full; a surge, a quick swell that dies away, each time a new
    /// group of enemies arrives; and the Leviathan, which takes it to full
    /// and holds it there.
    ///
    /// Kept apart from the component so that "it never goes backwards between
    /// surges" and "it is full when the boss is out" can be tested without a
    /// scene, the way <see cref="SpawnMath"/> is.
    /// </summary>
    public static class EruptionCurve
    {
        /// <summary>
        /// The level the volcano sits at between surges: 0 as the run starts,
        /// <paramref name="peak"/> as the boss is due, and no further after.
        /// </summary>
        /// <param name="lateness">
        /// 1 climbs evenly. Above 1 the first minutes stay quiet and most of
        /// the climb comes late; below 1 it gets loud early and levels off.
        /// </param>
        public static float Resting(float elapsed, float bossAt, float peak, float lateness)
        {
            if (bossAt <= 0f)
            {
                return 0f;
            }

            float share = Mathf.Clamp01(elapsed / bossAt);
            return Mathf.Clamp01(peak) * Mathf.Pow(share, Mathf.Max(lateness, 0.01f));
        }

        /// <summary>
        /// One surge, 0 to 1: nothing before it starts, up over
        /// <paramref name="rise"/> seconds, back down over <paramref name="fall"/>.
        /// </summary>
        public static float Pulse(float since, float rise, float fall)
        {
            if (since <= 0f)
            {
                return 0f;
            }

            if (since < rise)
            {
                return Mathf.SmoothStep(0f, 1f, since / rise);
            }

            if (fall <= 0f)
            {
                return 0f;
            }

            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((since - rise) / fall));
        }

        /// <summary>
        /// The strongest surge still running at <paramref name="elapsed"/>.
        /// The strongest rather than the sum, so two arrivals close together
        /// are one swell and not a double one.
        /// </summary>
        public static float Surge(float elapsed, IReadOnlyList<float> surges, float rise, float fall)
        {
            float strongest = 0f;

            if (surges == null)
            {
                return strongest;
            }

            for (int i = 0; i < surges.Count; i++)
            {
                strongest = Mathf.Max(strongest, Pulse(elapsed - surges[i], rise, fall));
            }

            return strongest;
        }

        /// <summary>
        /// The level itself. <paramref name="sinceBoss"/> is seconds since the
        /// Leviathan appeared, negative while it has not; once it has, the
        /// level eases to full over <paramref name="arrivalSeconds"/> and
        /// stays.
        /// </summary>
        public static float Level(float resting, float surge, float surgeHeight, float sinceBoss, float arrivalSeconds)
        {
            float level = Mathf.Clamp01(resting + surge * surgeHeight);

            if (sinceBoss < 0f)
            {
                return level;
            }

            float arrived = arrivalSeconds > 0f
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sinceBoss / arrivalSeconds))
                : 1f;
            return Mathf.Lerp(level, 1f, arrived);
        }

        /// <summary>
        /// When the surges come, read off the wave: the first arrival of each
        /// kind of enemy, and any stream that starts after
        /// <paramref name="quietGap"/> seconds in which none did.
        ///
        /// A run has no numbered waves to surge on. It has streams, each
        /// starting at its own time and then speeding up, and on MainRun
        /// twelve of the nineteen start inside the first twenty seconds. A
        /// surge per stream would be a dozen in the opening; this gives five,
        /// at 15, 120, 160, 187 and 227 seconds, each of them a moment where
        /// what is coming at the ship changes. The boss is not one of them:
        /// its arrival is the level going to full.
        /// </summary>
        public static void SurgeTimes(IReadOnlyList<SpawnStream> streams, float quietGap, List<float> into)
        {
            into.Clear();

            if (streams == null)
            {
                return;
            }

            var ordered = new List<SpawnStream>();

            foreach (SpawnStream stream in streams)
            {
                if (stream != null && stream.Prefab != null && stream.Prefab.GetComponent<BossEmitter>() == null)
                {
                    ordered.Add(stream);
                }
            }

            ordered.Sort((a, b) => a.StartDelay.CompareTo(b.StartDelay));

            var seen = new HashSet<GameObject>();
            float previous = float.NegativeInfinity;

            foreach (SpawnStream stream in ordered)
            {
                bool firstOfItsKind = seen.Add(stream.Prefab);
                bool afterAQuietSpell = stream.StartDelay - previous >= quietGap;

                // What is there as the run opens is the run, not a change in it.
                if (stream.StartDelay > 0f && (firstOfItsKind || afterAQuietSpell))
                {
                    into.Add(stream.StartDelay);
                }

                previous = stream.StartDelay;
            }
        }
    }
}
