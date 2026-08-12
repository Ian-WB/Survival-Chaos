using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Drives the two values a holographic bar draws: the current fill, and the
    /// "ghost" that lags behind it after a loss.
    ///
    /// The old bars snapped, so losing health produced a bar that was simply
    /// shorter the next time you looked at it. Holding the lost stretch on screen
    /// for a moment and then draining it is what makes a hit legible while the
    /// player is busy elsewhere on the screen.
    ///
    /// Kept free of MonoBehaviour so the timing can be tested without a scene.
    /// </summary>
    public sealed class BarMotion
    {
        /// <summary>Seconds the lost stretch stays put before it drains.</summary>
        public float HoldSeconds { get; set; } = 0.45f;

        /// <summary>How sharply the fill chases the real value.</summary>
        public float FillResponse { get; set; } = 22f;

        /// <summary>How sharply the ghost drains once the hold expires.</summary>
        public float GhostResponse { get; set; } = 6f;

        private float hold;

        /// <summary>
        /// The value asked for last frame, which is how a fresh loss is told from
        /// a fill still on its way down. See <see cref="Advance"/>.
        /// </summary>
        private float lastTarget;

        /// <summary>The bar's drawn length, 0..1.</summary>
        public float Fill { get; private set; }

        /// <summary>Where the bar was before the last loss, 0..1.</summary>
        public float Ghost { get; private set; }

        /// <summary>Places both values immediately, with no animation.</summary>
        public void SnapTo(float value)
        {
            value = Mathf.Clamp01(value);
            Fill = value;
            Ghost = value;
            hold = 0f;

            // Or the next Advance would read the drop from here as a fresh hit.
            lastTarget = value;
        }

        /// <summary>Advances one frame toward <paramref name="target"/>.</summary>
        public void Advance(float target, float deltaTime)
        {
            target = Mathf.Clamp01(target);

            if (deltaTime <= 0f)
            {
                return;
            }

            // A fresh loss restarts the hold, so rapid hits keep the trail up
            // rather than each one cutting the previous one short.
            //
            // Measured against the target, not against the fill. The fill eases
            // toward its target exponentially and so is lower every single frame
            // of a drain - which meant this restarted the hold on every frame and
            // the ghost never drained at all. It went unnoticed because the fill
            // does eventually settle onto a non-zero target exactly, releasing the
            // hold; draining to *zero* takes hundreds of frames to reach exact
            // zero, so the trail stayed up for the whole death screen. A full red
            // bar behind an empty one, which is the failure this class was written
            // to avoid.
            if (target < lastTarget)
            {
                hold = HoldSeconds;
            }

            lastTarget = target;

            Fill = Approach(Fill, target, FillResponse, deltaTime);

            // Gaining: the ghost has nothing to show, so it rides along. Without
            // this, healing would leave a red stretch ahead of the fill.
            if (Fill >= Ghost)
            {
                Ghost = Fill;
                hold = 0f;
                return;
            }

            if (hold > 0f)
            {
                hold -= deltaTime;
                return;
            }

            Ghost = Approach(Ghost, Fill, GhostResponse, deltaTime);
        }

        /// <summary>
        /// Framerate-independent easing: the same curve whatever the frame rate,
        /// unlike a plain Lerp with a constant factor.
        /// </summary>
        private static float Approach(float current, float target, float response, float deltaTime)
        {
            if (response <= 0f)
            {
                return target;
            }

            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-response * deltaTime));
        }
    }
}
