using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Slow Mo's timing. Its rules are what the player is promised - three
    /// seconds, once every thirty - and a wait measured from the wrong end is
    /// the difference between every thirty seconds and every thirty-three.
    /// </summary>
    public class SlowMoCycleTests
    {
        private static SlowMoCycle Cycle(float duration = 3f, float cooldown = 30f)
        {
            return new SlowMoCycle(duration, cooldown);
        }

        [Test]
        public void ANewCycle_IsReady_AndNotSlowing()
        {
            SlowMoCycle cycle = Cycle();

            Assert.IsTrue(cycle.IsReady(0f));
            Assert.IsFalse(cycle.IsSlowing(0f));
            Assert.AreEqual(1f, cycle.Gauge(0f));
        }

        [Test]
        public void AUse_SlowsForItsDuration_AndNoLonger()
        {
            SlowMoCycle cycle = Cycle();

            Assert.IsTrue(cycle.TryBegin(10f));

            Assert.IsTrue(cycle.IsSlowing(10f));
            Assert.IsTrue(cycle.IsSlowing(12.9f));
            Assert.IsFalse(cycle.IsSlowing(13f));
        }

        [Test]
        public void TheWait_RunsFromTheStartOfOneUseToTheStartOfTheNext()
        {
            SlowMoCycle cycle = Cycle();
            cycle.TryBegin(10f);

            Assert.IsFalse(cycle.TryBegin(39.9f));
            Assert.IsTrue(cycle.TryBegin(40f));
        }

        [Test]
        public void ARefusedUse_DoesNotPushTheWaitBack()
        {
            SlowMoCycle cycle = Cycle();
            cycle.TryBegin(0f);

            cycle.TryBegin(20f);

            Assert.IsTrue(cycle.IsReady(30f));
            Assert.IsFalse(cycle.IsSlowing(20f));
        }

        [Test]
        public void AWaitShorterThanTheSlowdown_CannotStartOneInsideAnother()
        {
            SlowMoCycle cycle = Cycle(duration: 3f, cooldown: 1f);
            cycle.TryBegin(0f);

            Assert.IsFalse(cycle.TryBegin(2f));
            Assert.IsTrue(cycle.TryBegin(3f));
        }

        [Test]
        public void TheGauge_DrainsOverTheSlowdown_ThenFillsOverTheRestOfTheWait()
        {
            SlowMoCycle cycle = Cycle(duration: 3f, cooldown: 30f);
            cycle.TryBegin(0f);

            Assert.AreEqual(1f, cycle.Gauge(0f), 1e-5f);
            Assert.AreEqual(0.5f, cycle.Gauge(1.5f), 1e-5f);
            Assert.AreEqual(0f, cycle.Gauge(3f), 1e-5f);
            Assert.AreEqual(0.5f, cycle.Gauge(16.5f), 1e-5f);
            Assert.AreEqual(1f, cycle.Gauge(30f), 1e-5f);
        }

        [Test]
        public void AShorterWait_AppliesToOneAlreadyRunning()
        {
            // A second Slow Mo pick, taken while the first is recharging.
            SlowMoCycle cycle = Cycle(duration: 3f, cooldown: 30f);
            cycle.TryBegin(0f);

            cycle.SetCooldown(24f);

            Assert.IsFalse(cycle.IsReady(23.9f));
            Assert.IsTrue(cycle.IsReady(24f));
        }

        [Test]
        public void Reset_MakesItReadyAgain()
        {
            SlowMoCycle cycle = Cycle();
            cycle.TryBegin(0f);

            cycle.Reset();

            Assert.IsTrue(cycle.IsReady(1f));
            Assert.IsFalse(cycle.IsSlowing(1f));
        }
    }
}
