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
    }
}
