using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Works out when a particle effect has genuinely finished.
    ///
    /// This replaces "duration plus lifetime", which is wrong for almost every
    /// effect in this project and wrong in the expensive direction.
    ///
    /// Duration is the length of the emission *window*, not the length of the
    /// effect. Every system in the packs used here emits a single burst at time
    /// zero and then sits out the rest of its two second window emitting
    /// nothing, so the last particle dies one lifetime after the start - not one
    /// lifetime after the window closes. Adding the duration anyway inflated
    /// every retire timer by the whole window: a muzzle flash that is over in
    /// 0.18s was being held for 1.4s, nearly eight times too long.
    ///
    /// The cost is not frame rate. It is that a finished effect stays in the
    /// pool's active set, so the pool keeps growing to cover effects that are
    /// no longer doing anything - which is the exact problem the measured timer
    /// was added to solve.
    /// </summary>
    public static class EffectTiming
    {
        /// <summary>
        /// Seconds after the computed end before the object retires, as a
        /// fraction of the effect's own length.
        ///
        /// Proportional rather than fixed. The old flat half second was a third
        /// of a two second explosion and nearly three times a muzzle flash - the
        /// shorter the effect, the more disproportionate the padding.
        /// </summary>
        private const float MarginFraction = 0.1f;

        /// <summary>
        /// Smallest absolute margin, covering frame granularity and the fact
        /// that a pooled effect is spawned partway through a frame.
        /// </summary>
        private const float MinimumMargin = 0.1f;

        /// <summary>
        /// When the last particle of <paramref name="effect"/> dies, in seconds
        /// from the moment it is spawned.
        /// </summary>
        public static float MeasureEnd(GameObject effect)
        {
            float end = 0f;

            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                float systemEnd = EndOf(system);

                if (systemEnd > end)
                {
                    end = systemEnd;
                }
            }

            return end;
        }

        /// <summary>The retire delay for an effect: its end plus a small margin.</summary>
        public static float RetireDelayFor(GameObject effect)
        {
            float end = MeasureEnd(effect);
            return end + Mathf.Max(MinimumMargin, end * MarginFraction);
        }

        /// <summary>
        /// When one system's particles are all gone, including anything its
        /// sub-emitters spawn and any trails they leave behind.
        /// </summary>
        private static float EndOf(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;

            float lifetime = main.startLifetime.constantMax;
            float delay = main.startDelay.constantMax;
            float emissionEnds = EmissionEndOf(system);

            float end = delay + emissionEnds + lifetime;

            // A trail can outlive the particle that drew it, unless it is set to
            // die with it. Missing this would cut a trail off mid-air.
            ParticleSystem.TrailModule trails = system.trails;
            if (trails.enabled && !trails.dieWithParticles)
            {
                end += trails.lifetime.constantMax;
            }

            return Mathf.Max(end, SubEmitterEndOf(system, emissionEnds, delay + emissionEnds + lifetime));
        }

        /// <summary>
        /// The last moment this system emits anything.
        ///
        /// A continuous rate - over time or over distance - runs for the whole
        /// duration. A system with neither only emits on its bursts, and the
        /// last burst is usually at zero, which is what makes the duration
        /// irrelevant for these effects.
        /// </summary>
        private static float EmissionEndOf(ParticleSystem system)
        {
            ParticleSystem.EmissionModule emission = system.emission;

            if (!emission.enabled)
            {
                return 0f;
            }

            bool continuous = emission.rateOverTime.constantMax > 0f
                              || emission.rateOverDistance.constantMax > 0f;

            if (continuous)
            {
                return system.main.duration;
            }

            float lastBurst = 0f;

            for (int i = 0; i < emission.burstCount; i++)
            {
                ParticleSystem.Burst burst = emission.GetBurst(i);

                // A repeating burst carries on past its start time, once per
                // cycle for as many cycles as it has.
                float burstEnd = burst.time;
                if (burst.cycleCount > 1)
                {
                    burstEnd += burst.repeatInterval * (burst.cycleCount - 1);
                }

                if (burstEnd > lastBurst)
                {
                    lastBurst = burstEnd;
                }
            }

            return lastBurst;
        }

        /// <summary>
        /// When particles spawned by this system's sub-emitters are gone.
        ///
        /// The trigger decides how late a sub-particle can be born. Birth fires
        /// as the parent particle appears, so the last one is born when emission
        /// stops; Death and Collision fire as the parent particle ends, so the
        /// last one is born when the parent system itself finishes. Treating a
        /// Death sub-emitter as if it fired at birth is how an effect gets cut
        /// off at exactly the moment its most visible part begins.
        /// </summary>
        private static float SubEmitterEndOf(ParticleSystem system, float emissionEnds, float parentEnd)
        {
            ParticleSystem.SubEmittersModule subs = system.subEmitters;

            if (!subs.enabled)
            {
                return 0f;
            }

            float end = 0f;

            for (int i = 0; i < subs.subEmittersCount; i++)
            {
                ParticleSystem child = subs.GetSubEmitterSystem(i);
                if (child == null)
                {
                    continue;
                }

                ParticleSystemSubEmitterType type = subs.GetSubEmitterType(i);

                bool atBirth = type == ParticleSystemSubEmitterType.Birth;
                float lastSpawn = atBirth ? emissionEnds : parentEnd;

                float childEnd = lastSpawn + child.main.startLifetime.constantMax;

                if (childEnd > end)
                {
                    end = childEnd;
                }
            }

            return end;
        }
    }
}
