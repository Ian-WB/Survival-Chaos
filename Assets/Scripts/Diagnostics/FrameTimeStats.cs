using System;

namespace SurvivalChaos
{
    /// <summary>
    /// A rolling window of frame times, and the statistics worth reading from it.
    ///
    /// Average FPS is the least useful number here. A run that averages 180 FPS
    /// but stutters is what pooling was meant to fix, and an average cannot show
    /// that - the lows can. "1% low" is the average of the slowest one percent of
    /// frames, which is the figure benchmarking tools report and the one that
    /// tracks what a player actually feels.
    ///
    /// Kept free of UnityEngine so the arithmetic can be tested directly.
    /// </summary>
    public sealed class FrameTimeStats
    {
        private readonly float[] samples;
        private readonly float[] scratch;

        private int count;
        private int next;

        public FrameTimeStats(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "A window needs at least one sample.");
            }

            samples = new float[capacity];
            scratch = new float[capacity];
        }

        /// <summary>How many samples the window currently holds.</summary>
        public int Count => count;

        /// <summary>The most samples it will hold before overwriting the oldest.</summary>
        public int Capacity => samples.Length;

        /// <summary>Records one frame, in seconds. Non-positive values are ignored.</summary>
        public void Add(float frameSeconds)
        {
            if (frameSeconds <= 0f || float.IsNaN(frameSeconds) || float.IsInfinity(frameSeconds))
            {
                return;
            }

            samples[next] = frameSeconds;
            next = (next + 1) % samples.Length;

            if (count < samples.Length)
            {
                count++;
            }
        }

        public void Clear()
        {
            count = 0;
            next = 0;
        }

        /// <summary>Mean frame time in milliseconds, or 0 with no samples.</summary>
        public float AverageMs
        {
            get
            {
                if (count == 0)
                {
                    return 0f;
                }

                double total = 0d;
                for (int i = 0; i < count; i++)
                {
                    total += samples[i];
                }

                return (float)(total / count * 1000d);
            }
        }

        /// <summary>Mean frames per second, or 0 with no samples.</summary>
        public float AverageFps => FpsFromMs(AverageMs);

        /// <summary>The fastest frame, as milliseconds.</summary>
        public float BestMs => Extreme(smallest: true);

        /// <summary>The slowest frame, as milliseconds. This is the spike.</summary>
        public float WorstMs => Extreme(smallest: false);

        /// <summary>
        /// The average of the slowest <paramref name="fraction"/> of frames,
        /// as milliseconds. Pass 0.01 for the 1% low, 0.001 for the 0.1% low.
        ///
        /// Always considers at least one frame, so a small window still reports
        /// its worst rather than nothing.
        /// </summary>
        public float LowMs(float fraction)
        {
            if (count == 0 || fraction <= 0f)
            {
                return 0f;
            }

            Array.Copy(samples, scratch, count);
            Array.Sort(scratch, 0, count);

            // Ascending, so the slowest frames are the tail.
            int take = (int)(count * fraction);
            take = Math.Max(1, Math.Min(count, take));

            double total = 0d;
            for (int i = count - take; i < count; i++)
            {
                total += scratch[i];
            }

            return (float)(total / take * 1000d);
        }

        /// <summary>The slowest <paramref name="fraction"/> of frames, as FPS.</summary>
        public float LowFps(float fraction) => FpsFromMs(LowMs(fraction));

        private float Extreme(bool smallest)
        {
            if (count == 0)
            {
                return 0f;
            }

            float best = samples[0];
            for (int i = 1; i < count; i++)
            {
                if (smallest ? samples[i] < best : samples[i] > best)
                {
                    best = samples[i];
                }
            }

            return best * 1000f;
        }

        private static float FpsFromMs(float milliseconds)
        {
            return milliseconds > 0f ? 1000f / milliseconds : 0f;
        }
    }
}
