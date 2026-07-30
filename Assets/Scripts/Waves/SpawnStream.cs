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
