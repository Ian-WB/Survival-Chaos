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
        private Renderer[] renderers;
        private MotionVectorGenerationMode[] authoredModes;
        private bool motionSuppressed;

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

        /// <summary>
        /// Stops this instance reporting motion for the frame it reappears on.
        ///
        /// A reused object is teleported to its new spawn point, so on its first
        /// frame the renderer reports movement from wherever it last died - often
        /// right across the screen. Anything that reprojects last frame using
        /// motion vectors then drags the old image along that path: FSR2 leaves a
        /// visible ghost trail on explosion particles, and TAA smears more subtly.
        ///
        /// One frame is the whole problem. After that the object is moving
        /// normally and its motion vectors are worth having, which is why the
        /// authored modes are restored rather than simply turned off for good.
        /// </summary>
        internal void SuppressMotionForOneFrame()
        {
            // Found once and kept, matching how the particle systems are cached.
            // Projectiles have a single renderer; explosions have a few.
            if (renderers == null)
            {
                renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
                authoredModes = new MotionVectorGenerationMode[renderers.Length];

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        authoredModes[i] = renderers[i].motionVectorGenerationMode;
                    }
                }
            }

            if (renderers.Length == 0 || motionSuppressed)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                }
            }

            motionSuppressed = true;
        }

        /// <summary>Puts the authored motion vector modes back. Safe to call twice.</summary>
        internal void RestoreMotion()
        {
            if (!motionSuppressed)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].motionVectorGenerationMode = authoredModes[i];
                }
            }

            motionSuppressed = false;
        }
    }
}
