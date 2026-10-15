using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The arithmetic behind <see cref="StormRise"/>: how far the storm has
    /// opened on the run's clock, how tall the wall stands for that, and what
    /// the cloud layer and its map have to be set to for the wall to stand
    /// so.
    ///
    /// Kept apart from the component so that "the moon is clear when the
    /// boss is due" and "at the end the clouds are exactly the scene's" can be
    /// tested without a scene, the way <see cref="EruptionCurve"/> is.
    /// </summary>
    public static class StormRiseCurve
    {
        /// <summary>HDRP's own, in VolumetricClouds: it stretches the cloud map by this and the layer's middle.</summary>
        public const float PlanetRadius = 6378100f;

        /// <summary>
        /// How far the storm has opened, 0 as the run starts and 1 as the
        /// boss is due, and 1 from then on.
        /// </summary>
        /// <param name="lateness">
        /// 1 opens evenly. Above 1 the wall holds high through the first
        /// minutes and does most of its sinking late; below 1 it drops early
        /// and the last of it comes slowly.
        /// </param>
        public static float Open(float elapsed, float bossAt, float lateness)
        {
            if (bossAt <= 0f)
            {
                return 1f;
            }

            float share = Mathf.Clamp01(elapsed / bossAt);
            return Mathf.Pow(share, Mathf.Max(lateness, 0.01f));
        }

        /// <summary>How many times its lowest the wall stands: <paramref name="tallest"/> shut, 1 open.</summary>
        public static float Rise(float open, float tallest)
        {
            return Mathf.Lerp(Mathf.Max(tallest, 1f), 1f, Mathf.Clamp01(open));
        }

        /// <summary>
        /// How deep the cloud layer has to be for the wall to stand
        /// <paramref name="rise"/> times its lowest. The shader reads the rise
        /// back off the depth (StormRise in VolumetricCloudsUtilities.hlsl).
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
