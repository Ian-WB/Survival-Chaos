using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// An enemy's first moments on the ring: it flashes and grows in from
    /// nothing, and cannot touch the ship or be shot until it is all there.
    ///
    /// Until 30 September 2026 an enemy was put down at full size on the frame
    /// it spawned, and two in three of them were in view when that happened.
    /// Added to each enemy by <see cref="WaveDirector"/> the first time it
    /// spawns and reused by the pool after that, so no prefab needs it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyArrival : MonoBehaviour
    {
        private readonly List<Collider> held = new List<Collider>();
        private Vector3 restScale;
        private bool measured;
        private float seconds;
        private float elapsed;

        /// <summary>True while the enemy is still growing in.</summary>
        public bool Arriving => enabled && elapsed < seconds;

        /// <summary>Starts an arrival on <paramref name="enemy"/>, adding the component the first time.</summary>
        public static void Begin(GameObject enemy, float seconds)
        {
            if (enemy == null || seconds <= 0f)
            {
                return;
            }

            if (!enemy.TryGetComponent(out EnemyArrival arrival))
            {
                arrival = enemy.AddComponent<EnemyArrival>();
            }

            arrival.Launch(seconds);
        }

        private void Launch(float duration)
        {
            if (!measured)
            {
                restScale = transform.localScale;
                measured = true;
            }

            seconds = duration;
            elapsed = 0f;

            // Only the colliders that are on now: anything its prefab keeps off
            // stays off afterwards.
            held.Clear();
            foreach (Collider part in GetComponentsInChildren<Collider>())
            {
                if (part.enabled)
                {
                    part.enabled = false;
                    held.Add(part);
                }
            }

            transform.localScale = restScale * 0.01f;

            if (TryGetComponent(out HitFlash flash))
            {
                flash.Strike();
            }

            enabled = true;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);

            // Fast at first and settling, so it reads as arriving, not inflating.
            float grown = 1f - (1f - t) * (1f - t) * (1f - t);
            transform.localScale = restScale * Mathf.Max(0.01f, grown);

            if (t >= 1f)
            {
                Finish();
            }
        }

        /// <summary>
        /// Also on despawn: an enemy taken off the ring mid-arrival goes back to
        /// the pool at its real size, with its colliders on.
        /// </summary>
        private void OnDisable()
        {
            Finish();
        }

        private void Finish()
        {
            if (measured)
            {
                transform.localScale = restScale;
            }

            foreach (Collider part in held)
            {
                if (part != null)
                {
                    part.enabled = true;
                }
            }

            held.Clear();
            elapsed = seconds;
            enabled = false;
        }
    }
}
