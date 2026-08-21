using System.Collections;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Runs every stream in a wave asset. One of these replaces all 21 Spawner
    /// objects that used to be placed around the scene.
    /// </summary>
    public sealed class WaveDirector : MonoBehaviour
    {
        [SerializeField]
        private WaveDefinition wave;

        [SerializeField]
        [Tooltip("The arena axis enemies fly in towards. Falls back to the object tagged " +
                 "Scenario, and to the world origin if there is none.")]
        private Transform arenaCenter;

        [SerializeField]
        [Min(0)]
        [Tooltip("Idle enemies to build per stream before the run starts, so the first " +
                 "arrivals do not pay to create themselves mid-frame.")]
        private int warmupPerStream = 4;

        private float startTime;

        /// <summary>Seconds since spawning began.</summary>
        public float Elapsed => Time.time - startTime;

        /// <summary>
        /// The wave being run, so the rest of the scene can ask about the shape
        /// of the run rather than being told it twice. Null until one is
        /// assigned, which is a mis-configured scene rather than a mode.
        /// </summary>
        public WaveDefinition Wave => wave;

        private void Start()
        {
            startTime = Time.time;

            if (arenaCenter == null)
            {
                GameObject scenario = GameObject.FindWithTag("Scenario");
                if (scenario != null)
                {
                    arenaCenter = scenario.transform;
                }
            }

            if (wave == null)
            {
                Debug.LogWarning("WaveDirector has no wave assigned; nothing will spawn.", this);
                return;
            }

            foreach (SpawnStream stream in wave.Streams)
            {
                if (stream == null || stream.Prefab == null)
                {
                    continue;
                }

                ObjectPool.Warm(stream.Prefab, warmupPerStream);
                StartCoroutine(RunStream(stream));
            }
        }

        private Vector3 Center => arenaCenter != null ? arenaCenter.position : Vector3.zero;

        /// <summary>
        /// The shortest gap the loop will honour, whatever a stream asks for.
        ///
        /// A stream added through the inspector arrives zero-filled - Unity does
        /// not run field initializers on a newly inserted list element - so both
        /// its interval and its floor are 0, and the loop would spawn an enemy
        /// every frame. That reads as the game locking up rather than as a
        /// mis-authored stream.
        /// </summary>
        private const float HardMinimumInterval = 0.05f;

        private IEnumerator RunStream(SpawnStream stream)
        {
            yield return new WaitForSeconds(stream.StartDelay);

            while (!HasStopped())
            {
                Spawn(stream);

                float interval = SpawnMath.IntervalAt(
                    stream.Interval,
                    stream.IntervalScale,
                    stream.RampEvery,
                    stream.MinInterval,
                    Elapsed);

                yield return new WaitForSeconds(Mathf.Max(interval, HardMinimumInterval));
            }
        }

        private bool HasStopped()
        {
            return wave.StopSpawningAt > 0f && Elapsed > wave.StopSpawningAt;
        }

        private void Spawn(SpawnStream stream)
        {
            float offsetX = Random.Range(stream.XOffsetRange.x, stream.XOffsetRange.y);
            float offsetY = Random.Range(stream.YOffsetRange.x, stream.YOffsetRange.y);

            Vector3 position = new Vector3(
                stream.Position.x + offsetX,
                stream.Position.y + offsetY,
                stream.Position.z);

            if (!stream.LockBearing)
            {
                position = AtRandomBearing(position, stream);
            }

            // Pooled. Enemies were the last thing in the game still going through
            // Instantiate and Destroy, on the argument that they arrive "once or
            // twice a second" - true of the opening minutes, which sit under one
            // a second, and not of the end, because nineteen streams each ramp
            // their own interval and the curve compounds. It reaches 7.5 a
            // second by the ten minute mark, and a run is 1331 enemies.
            //
            // That figure was quoted here as 7.5 a second at *five* minutes,
            // which was the end of the run back when the run was five minutes
            // long. Doubling it to 602s re-stretched the curve rather than
            // extending it, so the peak never changed and only moved: the same
            // 1.3, 4.1 and 7.5 a second that fell at two, four and five minutes
            // now fall at four, eight and ten. Five minutes is 1.7 a second.
            //
            // The rotation is whatever the stream authored. It stops mattering
            // within a frame either way: EnemyMovement calls LookAt towards the
            // axis every update, so a spawn brought in on a random bearing turns
            // to face the arena immediately.
            ObjectPool.Spawn(stream.Prefab, position, stream.Rotation);
        }

        /// <summary>
        /// The same distance out and the same height, in a random direction.
        ///
        /// Every stream in the wave asset authored a single world point with no
        /// horizontal spread at all - xOffsetRange is zero on all nineteen - so
        /// enemies arrived from nineteen fixed compass points and varied only in
        /// height. Keeping the authored radius keeps each stream's character,
        /// which is how far out it comes from; only the bearing changes.
        /// </summary>
        private Vector3 AtRandomBearing(Vector3 authored, SpawnStream stream)
        {
            Vector3 center = Center;

            Vector3 flat = authored - center;
            flat.y = 0f;

            float radius = flat.magnitude
                           + Random.Range(stream.RadiusOffsetRange.x, stream.RadiusOffsetRange.y);

            // A stream authored on the axis has no direction to preserve and no
            // radius to randomise around, so it stays where it was put.
            if (radius <= 0.01f)
            {
                return authored;
            }

            return PickupPlacement.PointAt(
                Random.Range(-180f, 180f), center, radius, authored.y);
        }

        private void OnDrawGizmosSelected()
        {
            if (wave == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;

            foreach (SpawnStream stream in wave.Streams)
            {
                if (stream == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(stream.Position, 3f);
            }
        }
    }
}
