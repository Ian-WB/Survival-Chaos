using System;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// One continuous source of enemies: what to spawn, where, and how the
    /// cadence tightens over a run. Each of these replaces one of the Spawner
    /// components that used to be placed by hand in the scene.
    ///
    /// Authored in the inspector and read at runtime, never written there. The
    /// fields are serialized and private, and everything outside sees read-only
    /// properties - these live inside MainRun.asset, and a ScriptableObject
    /// written to during play keeps the change on disk in the editor. A stream
    /// that quietly retuned itself over a play session would be a very hard bug
    /// to see coming.
    /// </summary>
    [Serializable]
    public class SpawnStream
    {
        [SerializeField]
        [Tooltip("Name shown in the inspector list. Has no effect on the game.")]
        private string label = "Stream";

        [SerializeField]
        private GameObject prefab;

        [Header("Placement")]
        [SerializeField]
        [Tooltip("World position the enemy spawns at, before the random offsets below.")]
        private Vector3 position;

        [SerializeField]
        private Quaternion rotation = Quaternion.identity;

        [SerializeField]
        [Tooltip("Random offset added to x. Both zero means no horizontal spread.")]
        private Vector2 xOffsetRange = Vector2.zero;

        [SerializeField]
        [Tooltip("Random offset added to y.")]
        private Vector2 yOffsetRange = Vector2.zero;

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
        [SerializeField]
        [Tooltip("Keeps this stream spawning at exactly the point above. Off - the default - " +
                 "sends each enemy in from a random bearing around the arena, at the same " +
                 "distance from the axis and the same height.")]
        private bool lockBearing;

        [SerializeField]
        [Tooltip("Random variation in how far out the enemy appears, in world units. Widens " +
                 "the band they fly in from so arrivals do not all cross the same circle.")]
        private Vector2 radiusOffsetRange = Vector2.zero;

        [Header("Timing")]
        [SerializeField]
        [Tooltip("Seconds before the first spawn.")]
        private float startDelay = 1f;

        [SerializeField]
        [Tooltip("Seconds between spawns, before any ramping.")]
        private float interval = 10f;

        [Header("Ramp")]
        [SerializeField]
        [Tooltip("Interval is multiplied by this every rampEvery seconds. 0.9 tightens; 1 disables ramping.")]
        private float intervalScale = 0.9f;

        [SerializeField]
        [Tooltip("How often the ramp is applied, in seconds.")]
        private float rampEvery = 25f;

        [SerializeField]
        [Tooltip("The interval never drops below this.")]
        private float minInterval = 0.1f;

        /// <summary>Inspector-only name. Nothing reads this at runtime.</summary>
        public string Label => label;

        /// <summary>What this stream spawns. Null streams are skipped by the director.</summary>
        public GameObject Prefab => prefab;

        /// <summary>Where the enemy appears, before the random offsets are applied.</summary>
        public Vector3 Position => position;

        /// <summary>
        /// The orientation to spawn with. Stops mattering within a frame either
        /// way, because EnemyMovement looks at the axis on every update.
        /// </summary>
        public Quaternion Rotation => rotation;

        public Vector2 XOffsetRange => xOffsetRange;

        public Vector2 YOffsetRange => yOffsetRange;

        /// <summary>See the remarks on the backing field: false means randomise.</summary>
        public bool LockBearing => lockBearing;

        public Vector2 RadiusOffsetRange => radiusOffsetRange;

        public float StartDelay => startDelay;

        public float Interval => interval;

        public float IntervalScale => intervalScale;

        public float RampEvery => rampEvery;

        public float MinInterval => minInterval;

        /// <summary>
        /// Replaces an unusable rotation with identity, reporting whether it had
        /// to. Editor-side repair, called from <see cref="WaveDefinition"/>.
        ///
        /// Unity zero-fills a newly inserted list element rather than running the
        /// C# constructor, so the field initializers above never apply to a stream
        /// added by hand: it arrives with rotation (0,0,0,0), which is not a
        /// rotation at all but a quaternion of zero length, and produces garbage
        /// orientation rather than an obvious zero.
        ///
        /// Lives here rather than in the caller because this is the one thing
        /// allowed to write these fields, and the invariant belongs with the data
        /// it constrains. Any near-zero quaternion counts, not just exactly zero:
        /// a normalised rotation always has length 1, so nothing legitimate lands
        /// in the band this catches.
        /// </summary>
        public bool RepairRotation()
        {
            float lengthSquared = (rotation.x * rotation.x) + (rotation.y * rotation.y)
                                + (rotation.z * rotation.z) + (rotation.w * rotation.w);

            if (lengthSquared >= 0.0001f)
            {
                return false;
            }

            rotation = Quaternion.identity;
            return true;
        }
    }
}
