using System.Reflection;
using NUnit.Framework;
using UnityEngine.LowLevel;

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
        /// The wait always lets a frame go a few microseconds past its slot. Those
        /// are not counted, or the rate would drift below the cap by them.
        /// </summary>
        [Test]
        public void AReleaseRunningMicrosecondsOver_KeepsTheGrid()
        {
            double start = 0d;

            for (int frame = 1; frame <= 1000; frame++)
            {
                double due = FrameLimiter.StartOf(start, start + Interval * 0.5d, Interval);
                start = FrameLimiter.AnchorOf(due, due + 0.000005d);
                Assert.AreEqual(frame * Interval, start, 1e-9, "frame " + frame);
            }
        }

        /// <summary>
        /// The audit's case: the wait begins in good time, a sleep overruns, and
        /// the frame goes out 3 ms late. Counted from its slot, the next frame
        /// would go 3 ms early, a long frame and then a short one.
        /// </summary>
        [Test]
        public void AReleaseThatOverran_DoesNotShortenTheNextFrame()
        {
            double due = FrameLimiter.StartOf(0d, 0.005d, Interval);
            double released = due + 0.003d;
            double anchor = FrameLimiter.AnchorOf(due, released);
            double next = FrameLimiter.StartOf(anchor, anchor + 0.004d, Interval);

            Assert.AreEqual(Interval, next - released, 1e-12);
        }

        private static int WaitsInTheLoop(PlayerLoopSystem system)
        {
            int count = system.type == typeof(FrameLimiter) ? 1 : 0;

            if (system.subSystemList != null)
            {
                foreach (PlayerLoopSystem child in system.subSystemList)
                {
                    count += WaitsInTheLoop(child);
                }
            }

            return count;
        }

        private static void Call(string method)
        {
            typeof(FrameLimiter).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }

        private static bool TimerRaised =>
            (bool)typeof(FrameLimiter).GetField("timerRaised", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        /// <summary>
        /// Installing twice, which a recompile in play can now do, leaves one
        /// wait; the system timer is held only while a cap is; and taking it out
        /// hands the timer back.
        /// </summary>
        [Test]
        public void InstallingTwice_LeavesOneWait_AndTheTimerIsHeldOnlyUnderACap()
        {
            try
            {
                Call("Install");
                Call("Install");
                Assert.AreEqual(1, WaitsInTheLoop(PlayerLoop.GetCurrentPlayerLoop()));
                Assert.IsFalse(TimerRaised, "uncapped");

                FrameLimiter.TargetFps = 60;
                Assert.IsTrue(TimerRaised, "capped");

                FrameLimiter.TargetFps = 0;
                Assert.IsFalse(TimerRaised, "the cap gone to VSync, or off");

                FrameLimiter.TargetFps = 60;
                Call("Uninstall");
                Assert.AreEqual(0, WaitsInTheLoop(PlayerLoop.GetCurrentPlayerLoop()));
                Assert.IsFalse(TimerRaised, "uninstalled");
                Assert.AreEqual(0, FrameLimiter.TargetFps);

                FrameLimiter.TargetFps = 60;
                Assert.IsFalse(TimerRaised, "a cap with no wait to hold it asks for nothing");
            }
            finally
            {
                Call("Uninstall");
            }
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
