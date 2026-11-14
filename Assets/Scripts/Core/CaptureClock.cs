using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Real seconds per frame for things timed outside the game's own clock,
    /// which still keep step with a video being recorded.
    ///
    /// The splash and the trailer run on unscaled time, so a pause or Slow Mo
    /// does not hold them. But while frames are captured for a video
    /// (Time.captureFramerate) each one takes far longer to write than it
    /// lasts on film, and unscaled time counts the writing: the 60 s trailer
    /// played out in the first 12 s of its own recording, and the splash ran
    /// three times too fast (found 13 November 2026). During a capture one
    /// frame is one frame of film.
    /// </summary>
    public static class CaptureClock
    {
        /// <summary>Seconds this frame lasted, or lasts on film while one is being recorded.</summary>
        public static float UnscaledDelta => Delta(Time.captureFramerate, Time.unscaledDeltaTime);

        /// <summary>The same, from its two inputs.</summary>
        public static float Delta(int captureFramerate, float unscaledDelta)
        {
            return captureFramerate > 0 ? 1f / captureFramerate : unscaledDelta;
        }
    }
}
