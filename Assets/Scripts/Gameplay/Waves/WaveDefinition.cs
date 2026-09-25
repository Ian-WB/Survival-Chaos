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

        /// <summary>
        /// The player's band the stream heights were placed against, bottom and
        /// top. Every spawn is carried from here into the live band, keeping its
        /// place in it (<see cref="SpawnBand.Carry"/>), so PlayerBounds can move
        /// or change size and the waves go with it.
        ///
        /// MainRun records 2.72 to 11.62, the band as it was on 25 September
        /// 2026, which leaves every stream where it already flew. Its heights
        /// were first placed against 4.42 to 13.32; the band came down 1.7 on
        /// 21 September and the streams did not, and the runs since were played
        /// against where they ended up, so that is what was kept.
        /// </summary>
        [SerializeField]
        [Tooltip("The bottom of the player's band (PlayerBounds) the stream heights below were placed against. Each spawn keeps its place in the band wherever PlayerBounds goes - a stream a third of the way up this band spawns a third of the way up the live one. Change this and the ceiling only when re-placing the streams against a different band by hand.")]
        private float authoredFloor = 2.721233f;

        [SerializeField]
        [Tooltip("The top of the player's band the stream heights were placed against. See the floor above.")]
        private float authoredCeiling = 11.618166f;

        [SerializeField]
        private List<SpawnStream> streams = new List<SpawnStream>();

        public float StopSpawningAt => stopSpawningAt;

        /// <summary>The bottom of the band the stream heights were placed against.</summary>
        public float AuthoredFloor => authoredFloor;

        /// <summary>The top of the band the stream heights were placed against.</summary>
        public float AuthoredCeiling => authoredCeiling;

        /// <summary>
        /// Where a height placed in this wave spawns against the given band: the
        /// same place in it, measured from the band the wave records.
        /// </summary>
        public float HeightIn(float authored, float floor, float ceiling)
        {
            return SpawnBand.Carry(authored, authoredFloor, authoredCeiling, floor, ceiling);
        }

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
        /// Each stream is measured where it really spawns, carried into the given
        /// band by <see cref="HeightIn"/>. Since the waves follow the band, moving
        /// PlayerBounds no longer strands anything; what still lands a stream
        /// here is a height placed outside the recorded band, or the recorded
        /// band edited without re-placing the streams.
        ///
        /// Reports rather than repairs. A stream outside the band is sometimes
        /// deliberate, and guessing which number was meant would be worse than
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

                // The boss sets its own height against the band as it arrives
                // (BossEmitter.FlyAtBandHeight), so its stream's height is not
                // where it flies and would only be reported wrongly.
                if (stream.Prefab.GetComponent<BossEmitter>() != null)
                {
                    continue;
                }

                SpawnBand.RangeOf(
                    stream.Position.y, stream.YOffsetRange, out float lowest, out float highest);

                lowest = HeightIn(lowest, floor, ceiling);
                highest = HeightIn(highest, floor, ceiling);

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
