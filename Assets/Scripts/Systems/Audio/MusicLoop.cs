using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The arithmetic behind a music track that loops over part of itself, and
    /// behind one track handing over to another.
    ///
    /// Kept free of MonoBehaviour so it can be tested without a scene, the way
    /// <see cref="BarMotion"/> is. <see cref="MusicSource"/> is the only caller.
    /// </summary>
    public static class MusicLoop
    {
        /// <summary>A time in a clip as a sample index, never negative.</summary>
        public static int ToSamples(float seconds, int frequency)
        {
            return Mathf.Max(0, Mathf.RoundToInt(seconds * frequency));
        }

        /// <summary>
        /// Where playback belongs once it has reached the end of the loop, or -1
        /// while it has not.
        ///
        /// The overshoot is carried over rather than dropped. The jump happens on
        /// a frame, not on the sample, so by the time it is seen the track is a
        /// few milliseconds past the end; starting that far past the start keeps
        /// the beat where it was. An end at or before the start means no loop of
        /// its own, and the clip loops whole.
        /// </summary>
        public static int Wrap(int position, int start, int end)
        {
            if (end <= start || position < end)
            {
                return -1;
            }

            return start + ((position - end) % (end - start));
        }

        /// <summary>
        /// The two tracks' gains at a point in the handover: 0 is all the first,
        /// 1 all the second.
        ///
        /// Equal power rather than a straight line. Two unrelated tracks faded
        /// linearly dip about 3 dB in the middle, which reads as the music
        /// stumbling at exactly the moment the Leviathan arrives.
        /// </summary>
        public static void Crossfade(float mix, out float from, out float to)
        {
            float angle = Mathf.Clamp01(mix) * Mathf.PI * 0.5f;
            from = Mathf.Cos(angle);
            to = Mathf.Sin(angle);
        }
    }
}
