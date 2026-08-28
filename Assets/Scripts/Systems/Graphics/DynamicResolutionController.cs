namespace SurvivalChaos
{
    /// <summary>
    /// Decides what fraction of native to render at, chasing a target frame rate.
    ///
    /// Dynamic resolution was enabled in the pipeline asset from the start, but
    /// the scaler handed to it returned a fixed number - so the machinery ran and
    /// the resolution never moved. This is the part that was missing.
    ///
    /// Deliberately free of UnityEngine: the interesting behaviour is how it
    /// settles, and a controller that oscillates is far easier to catch in a test
    /// that can run a thousand simulated frames in a millisecond than by watching
    /// a build.
    /// </summary>
    public sealed class DynamicResolutionController
    {
        /// <summary>
        /// How far this controller will take the resolution.
        ///
        /// This used to mirror the pipeline asset's own minimum, which was 50 in
        /// every tier, and the point of mirroring was to keep the controller from
        /// spending its travel in a band that would be clipped off anyway. The
        /// asset floor sits at 33 now, because the upscalers are handed their
        /// scale from here and Ultra Performance renders at a third - so this is
        /// no longer a mirror of anything. It is a judgement: half resolution is
        /// as far as this is willing to go on its own to hold a frame rate, and a
        /// player who wants less than that can ask for it by name through the
        /// upscale quality row.
        /// </summary>
        public const float MinScale = 0.5f;
        public const float MaxScale = 1f;

        /// <summary>
        /// How far frame time may sit either side of target before anything moves.
        ///
        /// Without a deadband the scale changes every frame forever, and a
        /// resolution that never holds still is more distracting than one that is
        /// slightly wrong.
        /// </summary>
        public const float Deadband = 0.08f;

        /// <summary>
        /// Scale lost per second while missing target, and gained per second while
        /// beating it.
        ///
        /// Deliberately asymmetric. Dropping resolution is the response to being
        /// too slow, and being slow is the thing the player is feeling right now,
        /// so it happens quickly. Climbing back is a luxury and doing it eagerly
        /// is what makes these systems visibly pump - a scene that is briefly
        /// cheap pulls the resolution up, the next frame is expensive again, and
        /// the image breathes.
        /// </summary>
        public const float DropPerSecond = 0.60f;
        public const float RisePerSecond = 0.15f;

        public float Scale { get; private set; } = MaxScale;

        /// <summary>Back to native. For when the target changes or a run restarts.</summary>
        public void Reset()
        {
            Scale = MaxScale;
        }

        /// <summary>
        /// Advances the scale one step.
        /// </summary>
        /// <param name="frameMs">How long the last frame took.</param>
        /// <param name="targetMs">How long a frame is allowed to take.</param>
        /// <param name="deltaSeconds">Real time since the previous call.</param>
        public float Update(float frameMs, float targetMs, float deltaSeconds)
        {
            if (targetMs <= 0f || frameMs <= 0f || deltaSeconds <= 0f)
            {
                return Scale;
            }

            // Long stalls - a shader compile, a level load - are not evidence that
            // the scene is too expensive, and reacting to one drops the resolution
            // for several seconds afterwards for no reason.
            if (deltaSeconds > 0.5f)
            {
                return Scale;
            }

            float ratio = frameMs / targetMs;

            if (ratio > 1f + Deadband)
            {
                Scale -= DropPerSecond * deltaSeconds;
            }
            else if (ratio < 1f - Deadband)
            {
                Scale += RisePerSecond * deltaSeconds;
            }

            if (Scale < MinScale)
            {
                Scale = MinScale;
            }
            else if (Scale > MaxScale)
            {
                Scale = MaxScale;
            }

            return Scale;
        }
    }
}
