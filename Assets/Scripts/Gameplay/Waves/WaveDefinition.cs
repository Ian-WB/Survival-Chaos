using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The whole difficulty curve for a run, in one asset. This previously
    /// existed only as timing fields spread across 21 hand-placed Spawner
    /// objects in the scene, where no single view showed how the run escalated.
    /// </summary>
    [CreateAssetMenu(fileName = "Wave", menuName = "Survival Chaos/Wave")]
    public sealed class WaveDefinition : ScriptableObject
    {
        /// <summary>
        /// Seconds after which no further enemies spawn.
        ///
        /// The default tracks MainRun, which stops at 602 and lands the boss at
        /// 600 - two seconds of overlap so the last arrivals are already in the
        /// air when it appears. It was left at 301 when the run was doubled,
        /// which broke nothing because MainRun overrides it, and would have
        /// quietly given any newly authored wave half a run.
        /// </summary>
        [SerializeField]
        [Tooltip("Seconds after which no further enemies spawn. This is when the boss takes over. 0 means never stop.")]
        private float stopSpawningAt = 602f;

        [SerializeField]
        private List<SpawnStream> streams = new List<SpawnStream>();

        public float StopSpawningAt => stopSpawningAt;

        public IReadOnlyList<SpawnStream> Streams => streams;

        /// <summary>
        /// When the boss arrives, in seconds from the start of the run, or a
        /// negative number if this wave has no boss in it.
        ///
        /// Derived rather than authored, because the arrival time already exists
        /// as the start delay on the boss's own spawn stream and a second field
        /// holding the same number is a second field that can disagree with it.
        /// The boss stream is found by what it spawns - the only prefab in a wave
        /// carrying a <see cref="BossEmitter"/> - so nothing has to be labelled
        /// or kept in order for this to keep working.
        ///
        /// <see cref="Timer"/> reads this to decide when to put the boss health
        /// bar up. That used to be its own serialized number in the scene, set to
        /// 600 by hand to match a 600 authored here, with nothing checking that
        /// they still agreed.
        /// </summary>
        public float BossArrivesAt
        {
            get
            {
                foreach (SpawnStream stream in streams)
                {
                    if (stream == null || stream.Prefab == null)
                    {
                        continue;
                    }

                    if (stream.Prefab.GetComponent<BossEmitter>() != null)
                    {
                        return stream.StartDelay;
                    }
                }

                return -1f;
            }
        }

        /// <summary>
        /// Writes a line for every stream that spawns outside the given vertical
        /// band, and returns how many there were.
        ///
        /// The band is a parameter rather than something looked up here, because
        /// a wave asset has no scene to look in - it may be inspected with no
        /// scene open at all. The caller knows which arena it is asking about.
        ///
        /// Reports rather than repairs. A stream outside the band is sometimes
        /// deliberate, and the fix depends on which of the two numbers is wrong:
        /// moving the streams suits a bounds change, moving the bounds suits a
        /// deliberately taller arena. Guessing between them would be worse than
        /// saying so.
        /// </summary>
        public int DescribeStreamsOutside(float floor, float ceiling, StringBuilder into)
        {
            if (streams == null)
            {
                return 0;
            }

            int found = 0;

            foreach (SpawnStream stream in streams)
            {
                if (stream == null || stream.Prefab == null)
                {
                    continue;
                }

                SpawnBand.RangeOf(
                    stream.Position.y, stream.YOffsetRange, out float lowest, out float highest);

                if (SpawnBand.IsFullyInside(lowest, highest, floor, ceiling))
                {
                    continue;
                }

                found++;

                if (into == null)
                {
                    continue;
                }

                bool wholly = SpawnBand.IsWhollyOutside(lowest, highest, floor, ceiling);
                float below = SpawnBand.BelowFloorBy(lowest, floor);
                float above = SpawnBand.AboveCeilingBy(highest, ceiling);

                into.Append("  ").Append(stream.Label)
                    .Append("  spawns ").Append(lowest.ToString("0.###"))
                    .Append(" to ").Append(highest.ToString("0.###"))
                    .Append(wholly ? "  - ENTIRELY outside, every one of them" : "  - partly outside");

                if (below > 0f)
                {
                    into.Append(", ").Append(below.ToString("0.###")).Append(" under the floor");
                }

                if (above > 0f)
                {
                    into.Append(", ").Append(above.ToString("0.###")).Append(" over the ceiling");
                }

                into.AppendLine();
            }

            return found;
        }

        /// <summary>Replaces the stream list. Used by the migration tool.</summary>
        public void SetStreams(List<SpawnStream> value)
        {
            streams = value ?? new List<SpawnStream>();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Repairs streams added through the inspector's + button.
        /// </summary>
        /// <remarks>
        /// The repair itself lives on <see cref="SpawnStream.RepairRotation"/>,
        /// which owns the fields it fixes and explains what makes a hand-added
        /// stream arrive unusable. This is only the sweep that offers every
        /// stream the chance.
        ///
        /// The existing streams came through <see cref="SetStreams"/> from the
        /// migration tool and are unaffected. This only catches the next one
        /// someone adds by hand.
        /// </remarks>
        private void OnValidate()
        {
            if (streams == null)
            {
                return;
            }

            foreach (SpawnStream stream in streams)
            {
                stream?.RepairRotation();
            }
        }
#endif
    }
}
