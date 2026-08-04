using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Added automatically to anything the pool creates, recording which prefab
    /// it came from so it can be returned to the right bucket.
    ///
    /// Its presence is also how <see cref="ObjectPool.Despawn"/> tells a pooled
    /// object from one that was placed in the scene by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledInstance : MonoBehaviour
    {
        private ParticleSystem[] effects;

        /// <summary>The prefab this instance was cloned from.</summary>
        public GameObject Source { get; internal set; }

        /// <summary>Convenience for despawning from the object itself.</summary>
        public void Despawn()
        {
            ObjectPool.Despawn(gameObject);
        }

        /// <summary>
        /// Replays any particle effects on this instance.
        ///
        /// A fresh clone plays on its own through Play On Awake, but a reused one
        /// has already finished, and the hit and explosion effects are the whole
        /// point of those prefabs - silently failing to replay the second one
        /// would be a miserable bug to track down. The lookup happens once and is
        /// kept; for projectiles it finds nothing and costs nothing thereafter.
        /// </summary>
        public void Replay()
        {
            effects ??= GetComponentsInChildren<ParticleSystem>(includeInactive: true);

            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null)
                {
                    effects[i].Play(withChildren: false);
                }
            }
        }
    }
}
