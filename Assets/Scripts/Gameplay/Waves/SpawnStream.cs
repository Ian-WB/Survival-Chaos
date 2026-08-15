using System;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// One continuous source of enemies: what to spawn, where, and how the
    /// cadence tightens over a run. Each of these replaces one of the Spawner
    /// components that used to be placed by hand in the scene.
    /// </summary>
    [Serializable]
    public class SpawnStream
    {
        [Tooltip("Name shown in the inspector list. Has no effect on the game.")]
        public string label = "Stream";

        public GameObject prefab;

        [Header("Placement")]
        [Tooltip("World position the enemy spawns at, before the random offsets below.")]
        public Vector3 position;

        public Quaternion rotation = Quaternion.identity;

        [Tooltip("Random offset added to x. Both zero means no horizontal spread.")]
        public Vector2 xOffsetRange = Vector2.zero;

        [Tooltip("Random offset added to y.")]
        public Vector2 yOffsetRange = Vector2.zero;

        /// <summary>
        /// Pins the stream to the exact point above instead of sending each enemy
        /// in from a random direction at the same distance and height.
        /// </summary>
        /// <remarks>
        /// Phrased as a lock rather than as "randomise" on purpose. Unity keeps
        /// what it already serialized and zero-fills fields it has not seen
        /// before, so a bool meaning "randomise" would arrive false on all
        /// nineteen existing streams however its initializer was written, and the
        /// spread would silently not happen. False is the useful default, so
        /// false has to mean randomise.
        /// </remarks>
        [Tooltip("Keeps this stream spawning at exactly the point above. Off - the default - " +
                 "sends each enemy in from a random bearing around the arena, at the same " +
                 "distance from the axis and the same height.")]
        public bool lockBearing;

        [Tooltip("Random variation in how far out the enemy appears, in world units. Widens " +
                 "the band they fly in from so arrivals do not all cross the same circle.")]
        public Vector2 radiusOffsetRange = Vector2.zero;

        [Header("Timing")]
        [Tooltip("Seconds before the first spawn.")]
        public float startDelay = 1f;

        [Tooltip("Seconds between spawns, before any ramping.")]
        public float interval = 10f;

        [Header("Ramp")]
        [Tooltip("Interval is multiplied by this every rampEvery seconds. 0.9 tightens; 1 disables ramping.")]
        public float intervalScale = 0.9f;

        [Tooltip("How often the ramp is applied, in seconds.")]
        public float rampEvery = 25f;

        [Tooltip("The interval never drops below this.")]
        public float minInterval = 0.1f;
    }
}
