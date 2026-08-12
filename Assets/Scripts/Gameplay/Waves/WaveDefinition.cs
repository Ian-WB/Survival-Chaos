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
        [SerializeField]
        [Tooltip("Seconds after which no further enemies spawn. This is when the boss takes over. 0 means never stop.")]
        private float stopSpawningAt = 301f;

        [SerializeField]
        private List<SpawnStream> streams = new List<SpawnStream>();

        public float StopSpawningAt => stopSpawningAt;

        public IReadOnlyList<SpawnStream> Streams => streams;

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
        /// Unity zero-fills a newly inserted list element rather than running the
        /// C# constructor, so the field initializers on <see cref="SpawnStream"/>
        /// never apply to one added by hand. A new stream therefore arrives with
        /// rotation (0,0,0,0) - not identity, but a quaternion of zero length,
        /// which is not a rotation at all and produces garbage orientation rather
        /// than an obvious zero.
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
                if (stream == null)
                {
                    continue;
                }

                // Any near-zero quaternion, not just exactly zero: a normalised
                // rotation always has length 1, so nothing legitimate lands here.
                Quaternion r = stream.rotation;
                if (r.x * r.x + r.y * r.y + r.z * r.z + r.w * r.w < 0.0001f)
                {
                    stream.rotation = Quaternion.identity;
                }
            }
        }
#endif
    }
}
