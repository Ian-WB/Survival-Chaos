using System;
using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// These numbers get sent to other people as evidence of how the game runs,
    /// so being quietly wrong is the worst outcome - a plausible frame rate that
    /// misrepresents a machine is harder to catch than an obvious one.
    /// </summary>
    public class FrameTimeStatsTests
    {
        private const float TenMs = 0.010f;
        private const float HundredMs = 0.100f;

        [Test]
        public void Empty_ReportsZeroRatherThanDividingByZero()
        {
            FrameTimeStats stats = new FrameTimeStats(16);

            Assert.AreEqual(0, stats.Count);
            Assert.AreEqual(0f, stats.AverageMs);
            Assert.AreEqual(0f, stats.AverageFps);
            Assert.AreEqual(0f, stats.WorstMs);
            Assert.AreEqual(0f, stats.BestMs);
            Assert.AreEqual(0f, stats.LowMs(0.01f));
        }

        [Test]
        public void Average_IsTheMeanFrameTime()
        {
            FrameTimeStats stats = new FrameTimeStats(16);
            stats.Add(TenMs);
            stats.Add(0.020f);

            Assert.AreEqual(15f, stats.AverageMs, 0.001f);
            Assert.AreEqual(1000f / 15f, stats.AverageFps, 0.01f);
        }

        [Test]
        public void BestAndWorst_AreTheExtremes()
        {
            FrameTimeStats stats = new FrameTimeStats(16);
            stats.Add(TenMs);
            stats.Add(HundredMs);
            stats.Add(0.005f);

            Assert.AreEqual(5f, stats.BestMs, 0.001f);
            Assert.AreEqual(100f, stats.WorstMs, 0.001f);
        }

        [Test]
        public void Window_OverwritesTheOldestSample()
        {
            FrameTimeStats stats = new FrameTimeStats(2);
            stats.Add(HundredMs);
            stats.Add(TenMs);
            stats.Add(TenMs);

            Assert.AreEqual(2, stats.Count, "the window must not grow past its capacity");
            Assert.AreEqual(10f, stats.WorstMs, 0.001f, "the spike should have aged out");
        }

        [Test]
        public void OnePercentLow_AveragesTheSlowestHundredth()
        {
            // 990 good frames and 10 bad ones: the 1% low is exactly those ten.
            FrameTimeStats stats = new FrameTimeStats(1000);
            for (int i = 0; i < 990; i++)
            {
                stats.Add(TenMs);
            }

            for (int i = 0; i < 10; i++)
            {
                stats.Add(HundredMs);
            }

            Assert.AreEqual(100f, stats.LowMs(0.01f), 0.001f);
            Assert.AreEqual(10f, stats.LowFps(0.01f), 0.01f);
        }

        [Test]
        public void OnePercentLow_IsWorseThanTheAverage()
        {
            // The whole reason the overlay reports it: a run that averages well
            // can still stutter, and only the low shows it.
            FrameTimeStats stats = new FrameTimeStats(100);
            for (int i = 0; i < 99; i++)
            {
                stats.Add(TenMs);
            }

            stats.Add(HundredMs);

            Assert.Less(stats.AverageMs, stats.LowMs(0.01f));
            Assert.AreEqual(100f, stats.LowMs(0.01f), 0.001f);
        }

        [Test]
        public void Low_AlwaysConsidersAtLeastOneFrame()
        {
            // 10 x 0.001 rounds to zero frames; reporting nothing would be wrong.
            FrameTimeStats stats = new FrameTimeStats(16);
            for (int i = 0; i < 9; i++)
            {
                stats.Add(TenMs);
            }

            stats.Add(HundredMs);

            Assert.AreEqual(100f, stats.LowMs(0.001f), 0.001f);
        }

        [Test]
        public void Add_IgnoresValuesThatWouldPoisonTheAverage()
        {
            FrameTimeStats stats = new FrameTimeStats(16);
            stats.Add(TenMs);
            stats.Add(0f);
            stats.Add(-1f);
            stats.Add(float.NaN);
            stats.Add(float.PositiveInfinity);

            Assert.AreEqual(1, stats.Count);
            Assert.AreEqual(10f, stats.AverageMs, 0.001f);
        }

        [Test]
        public void Clear_EmptiesTheWindow()
        {
            FrameTimeStats stats = new FrameTimeStats(16);
            stats.Add(TenMs);

            stats.Clear();

            Assert.AreEqual(0, stats.Count);
            Assert.AreEqual(0f, stats.AverageMs);
        }

        [Test]
        public void Capacity_MustBePositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FrameTimeStats(0));
        }
    }
}
