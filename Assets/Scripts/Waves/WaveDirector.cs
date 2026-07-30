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

        private float startTime;

        /// <summary>Seconds since spawning began.</summary>
        public float Elapsed => Time.time - startTime;

        private void Start()
        {
            startTime = Time.time;

            if (wave == null)
            {
                Debug.LogWarning("WaveDirector has no wave assigned; nothing will spawn.", this);
                return;
            }

            foreach (SpawnStream stream in wave.Streams)
            {
                if (stream == null || stream.prefab == null)
                {
                    continue;
                }

                StartCoroutine(RunStream(stream));
            }
        }

        private IEnumerator RunStream(SpawnStream stream)
        {
            yield return new WaitForSeconds(stream.startDelay);

            while (!HasStopped())
            {
                Spawn(stream);

                float interval = SpawnMath.IntervalAt(
                    stream.interval,
                    stream.intervalScale,
                    stream.rampEvery,
                    stream.minInterval,
                    Elapsed);

                yield return new WaitForSeconds(interval);
            }
        }

        private bool HasStopped()
        {
            return wave.StopSpawningAt > 0f && Elapsed > wave.StopSpawningAt;
        }

        private void Spawn(SpawnStream stream)
        {
            float offsetX = Random.Range(stream.xOffsetRange.x, stream.xOffsetRange.y);
            float offsetY = Random.Range(stream.yOffsetRange.x, stream.yOffsetRange.y);

            Vector3 position = new Vector3(
                stream.position.x + offsetX,
                stream.position.y + offsetY,
                stream.position.z);

            Instantiate(stream.prefab, position, stream.rotation);
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

                Gizmos.DrawWireSphere(stream.position, 3f);
            }
        }
    }
}
