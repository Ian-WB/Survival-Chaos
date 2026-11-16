using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The arithmetic behind <see cref="StormRise"/>: how far the storm has
    /// grown on the run's clock, how tall the wall stands for that, and what
    /// the cloud layer and its map have to be set to for the wall to stand
    /// so.
    ///
    /// Kept apart from the component so that "the storm is full when the
    /// boss is due" and "the floor under the island never moves" can be
    /// tested without a scene, the way <see cref="EruptionCurve"/> is.
    /// </summary>
    public static class StormRiseCurve
    {
        /// <summary>HDRP's own, in VolumetricClouds: it stretches the cloud map by this and the layer's middle.</summary>
        public const float PlanetRadius = 6378100f;

        /// <summary>
        /// How far the storm has grown: 0, calm, until <paramref name="startsAt"/>
        /// of the way to the boss, then evenly up to 1, full, as the boss is
        /// due, and 1 from then on.
        /// </summary>
        /// <param name="startsAt">
        /// The share of the time to the boss the storm stays calm for: 0.75
        /// starts it growing three quarters of the way in.
        /// </param>
        public static float Grown(float elapsed, float bossAt, float startsAt)
        {
            if (bossAt <= 0f)
            {
                return 1f;
            }

            float share = Mathf.Clamp01(elapsed / bossAt);
            float start = Mathf.Clamp(startsAt, 0f, 0.99f);
            return Mathf.Clamp01((share - start) / (1f - start));
        }

        /// <summary>
        /// How much of the cloud is there, 0 none to 1 all of it, as the
        /// storm grows: the clouds condense from nothing over the first
        /// <paramref name="formsBy"/> of the growth, eased at both ends so
        /// neither their arrival nor their filling in is a step.
        /// </summary>
        public static float Formed(float grown, float formsBy)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(grown / Mathf.Max(formsBy, 0.01f)));
        }

        /// <summary>
        /// How wild the storm is once its clouds are there: 0 until they are
        /// whole, <paramref name="formsBy"/> of the way through the growth,
        /// then evenly up to 1 as the storm is full. The storm's lightning
        /// comes faster by it (StormLightning).
        /// </summary>
        public static float Fury(float grown, float formsBy)
        {
            float whole = Mathf.Clamp(formsBy, 0f, 0.99f);
            return Mathf.Clamp01((Mathf.Clamp01(grown) - whole) / (1f - whole));
        }

        /// <summary>
        /// How many times its painted height the wall stands:
        /// <paramref name="lowest"/> calm, <paramref name="tallest"/> full.
        /// </summary>
        public static float Rise(float grown, float tallest, float lowest)
        {
            return Mathf.Lerp(Mathf.Max(lowest, 0f), Mathf.Max(tallest, 0f), Mathf.Clamp01(grown));
        }

        /// <summary>
        /// How deep the cloud layer has to be for the wall to stand
        /// <paramref name="rise"/> times its painted height: deeper over it,
        /// and never shallower than the painted layer, whose floor lies under
        /// the island however low the wall sinks.
        /// </summary>
        public static float Depth(float lowDepth, float rise)
        {
            return lowDepth * Mathf.Max(rise, 1f);
        }

        /// <summary>
        /// What Cloud Tiling has to be at <paramref name="depth"/> for the map
        /// to cover the same ground it does at <paramref name="lowDepth"/> with
        /// <paramref name="lowTiling"/>. HDRP stretches the map over a distance
        /// that grows with the layer's middle, so a deeper layer needs more
        /// tiling, or the eye would widen as the wall rose.
        /// </summary>
        public static float Tiling(float lowTiling, float bottom, float lowDepth, float depth)
        {
            return lowTiling * Stretch(bottom + depth * 0.5f) / Stretch(bottom + lowDepth * 0.5f);
        }

        /// <summary>HDRP's ComputeNormalizationFactor for a layer whose middle is at <paramref name="middle"/>.</summary>
        private static float Stretch(float middle)
        {
            return Mathf.Sqrt((PlanetRadius + middle) * (PlanetRadius + middle) - PlanetRadius * PlanetRadius);
        }
    }
}
