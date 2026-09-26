using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The frame limiter's schedule, run on made-up clocks. The wait itself is
    /// timing and only a real run shows it; what can go wrong on paper is the
    /// rhythm, and a rhythm that drifts or rushes is the stutter this replaced.
    /// </summary>
    public class FrameLimiterTests
    {
        private const double Interval = 1d / 60d;

        [Test]
        public void AnEarlyFrame_WaitsForItsSlot()
        {
            Assert.AreEqual(1d + Interval, FrameLimiter.StartOf(1d, 1.005d, Interval), 1e-12);
        }

        [Test]
        public void ALateFrame_StartsAtOnce()
        {
            Assert.AreEqual(1.03d, FrameLimiter.StartOf(1d, 1.03d, Interval), 1e-12);
        }

        /// <summary>
        /// Frames of any cost under the interval start on an exact grid. Anchored
        /// to the previous slot, not to when the frame arrived, so the waits do not
        /// add up to a slow drift below the cap.
        /// </summary>
        [Test]
        public void CheapFrames_KeepAnExactRhythm()
        {
            double start = 0d;
            System.Random random = new System.Random(7);

            for (int frame = 1; frame <= 1000; frame++)
            {
                double arrived = start + Interval * random.NextDouble() * 0.95d;
                start = FrameLimiter.StartOf(start, arrived, Interval);
                Assert.AreEqual(frame * Interval, start, 1e-9, "frame " + frame);
            }
        }

        /// <summary>
        /// After a hitch the rhythm restarts from the late frame. Catching up would
        /// start the next frame at once as well, and one long gap would become a
        /// long one and a short one.
        /// </summary>
        [Test]
        public void AfterAHitch_TheNextFrameIsNotRushed()
        {
            double late = FrameLimiter.StartOf(0d, 0.030d, Interval);
            double next = FrameLimiter.StartOf(late, late + 0.004d, Interval);

            Assert.AreEqual(Interval, next - late, 1e-12);
        }

        /// <summary>
        /// With the cap above what the machine manages, every frame arrives late
        /// and none waits: the cap costs nothing when it is not binding.
        /// </summary>
        [Test]
        public void ACapAboveWhatTheMachineManages_NeverWaits()
        {
            double start = 0d;
            for (int frame = 1; frame <= 100; frame++)
            {
                double arrived = start + Interval * 1.5d;
                double next = FrameLimiter.StartOf(start, arrived, Interval);
                Assert.AreEqual(arrived, next, 1e-12);
                start = next;
            }
        }
    }
}
