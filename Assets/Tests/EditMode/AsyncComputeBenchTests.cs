using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The Checked build's async compute bench: what it reads off the command
    /// line, and the arithmetic of its verdict.
    /// </summary>
    public class AsyncComputeBenchTests
    {
        [Test]
        public void NoFlagsAskForNothing()
        {
            BenchRequest r = BenchRequest.Parse(new[] { "Survival-Chaos-main.exe" });
            Assert.IsFalse(r.Any);
            Assert.AreEqual(-1, r.GlobalIllumination);
        }

        [Test]
        public void ReadsTheFlags()
        {
            BenchRequest r = BenchRequest.Parse(new[] { "game.exe", "-asyncBench", "-benchGI", "6" });
            Assert.IsTrue(r.Async);
            Assert.IsFalse(r.Reflections);
            Assert.AreEqual((int)EffectQuality.RayTracedHigh, r.GlobalIllumination);

            Assert.IsTrue(BenchRequest.Parse(new[] { "-benchReflections" }).Reflections);
        }

        [Test]
        public void ReadsAFixedSetting()
        {
            Assert.AreEqual(-1, BenchRequest.Parse(new[] { "-asyncBench" }).Fixed);
            Assert.AreEqual(0, BenchRequest.Parse(new[] { "-benchFixed", "off" }).Fixed);
            Assert.AreEqual(1, BenchRequest.Parse(new[] { "-benchFixed", "on", "-benchGI", "6" }).Fixed);
            Assert.IsTrue(BenchRequest.Parse(new[] { "-benchFixed", "on" }).Any);
            Assert.AreEqual(-1, BenchRequest.Parse(new[] { "-benchFixed", "maybe" }).Fixed);
        }

        [TestCase("7")]
        [TestCase("-1")]
        [TestCase("high")]
        public void IgnoresAGiRungOffTheRow(string rung)
        {
            Assert.AreEqual(-1, BenchRequest.Parse(new[] { "-asyncBench", "-benchGI", rung }).GlobalIllumination);
        }

        [Test]
        public void MedianOfEvenAndOddCounts()
        {
            Assert.AreEqual(2d, BenchMath.Median(new[] { 3d, 1d, 2d }));
            Assert.AreEqual(2.5d, BenchMath.Median(new[] { 4d, 1d, 2d, 3d }));
        }

        [Test]
        public void OffWinsOnlyPastTheNoise()
        {
            // Pairs save 0.6, 0.5 and 0.4; neighbouring on phases differ by 0.2, 0.0 and 0.2.
            double[] on = { 15.0d, 15.2d, 15.2d, 15.0d };
            Assert.IsTrue(BenchMath.OffWins(on, new[] { 14.5d, 14.7d, 14.7d }, out double saving, out double noise));
            Assert.AreEqual(0.5d, saving, 1e-9);
            Assert.AreEqual(0.2d, noise, 1e-9);

            // The same savings next to on phases a whole millisecond apart are no win.
            Assert.IsFalse(BenchMath.OffWins(new[] { 15.0d, 16.0d, 15.0d, 16.0d }, new[] { 15.0d, 15.0d, 15.0d }, out _, out _));
        }

        [Test]
        public void OneJumpSpoilsOnlyItsOwnPairs()
        {
            // The GPU jumps to 24 ms for one on phase: the median of pairs still finds the 0.4 ms.
            double[] on = { 8.0d, 8.0d, 24.0d, 8.0d, 8.0d, 8.0d };
            double[] off = { 7.6d, 7.6d, 7.6d, 7.6d, 7.6d };
            Assert.IsTrue(BenchMath.OffWins(on, off, out double saving, out double noise));
            Assert.AreEqual(0.4d, saving, 1e-9);
            Assert.AreEqual(0d, noise, 1e-9);
        }

        [Test]
        public void NoPairsIsNoWin()
        {
            Assert.IsFalse(BenchMath.OffWins(new[] { 8.0d }, new double[0], out _, out _));
        }
    }
}
