using System.Collections.Generic;
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
