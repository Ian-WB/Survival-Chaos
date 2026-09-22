using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Salvage is paced by need: a hurt player sees it often, a healthy one
    /// never, and a long quiet spell pays out once rather than all at once.
    /// </summary>
    public class SalvageClockTests
    {
        private const float HalfHealthSeconds = 12f;

        /// <summary>Runs the clock in frame-sized steps, the way Update does.</summary>
        private static void Run(SalvageClock clock, float seconds, int current, int max)
        {
            const float frame = 1f / 60f;
            for (float t = frame; t <= seconds + 1e-4f; t += frame)
            {
                clock.Advance(frame, current, max, HalfHealthSeconds);
            }
        }

        [Test]
        public void AtHalfHealth_IsDueAfterTheConfiguredWait()
        {
            var clock = new SalvageClock();

            Run(clock, HalfHealthSeconds - 0.1f, 10, 20);
            Assert.IsFalse(clock.Ready, "due early");

            Run(clock, 0.2f, 10, 20);
            Assert.IsTrue(clock.Ready, "not due after the half-health wait");
        }

        [Test]
        public void WaitScalesWithHowMuchHealthIsMissing()
        {
            var quarterMissing = new SalvageClock();
            var threeQuartersMissing = new SalvageClock();

            Run(quarterMissing, 2f * HalfHealthSeconds - 0.1f, 15, 20);
            Run(threeQuartersMissing, HalfHealthSeconds * 2f / 3f + 0.1f, 5, 20);

            Assert.IsFalse(quarterMissing.Ready, "a quarter missing should wait twice as long");
            Assert.IsTrue(threeQuartersMissing.Ready, "three quarters missing should wait two thirds as long");
        }

        [Test]
        public void NeverCharges_AtFullHealth_OrWhenDead()
        {
            var full = new SalvageClock();
            var dead = new SalvageClock();

            Run(full, 600f, 20, 20);
            Run(dead, 600f, 0, 20);

            Assert.AreEqual(0f, full.Charge);
            Assert.AreEqual(0f, dead.Charge);
        }

        [Test]
        public void SpendingEmptiesTheClock_AndOnlyOnePieceIsOwed()
        {
            var clock = new SalvageClock();

            Run(clock, 10f * HalfHealthSeconds, 10, 20);

            Assert.IsTrue(clock.TrySpend(10, 20));
            Assert.AreEqual(0f, clock.Charge);
            Assert.IsFalse(clock.TrySpend(10, 20), "a long wait banked a second piece");
        }

        [Test]
        public void NotSpent_BeforeItIsDue()
        {
            var clock = new SalvageClock();

            Run(clock, HalfHealthSeconds / 2f, 10, 20);
            float charge = clock.Charge;

            Assert.IsFalse(clock.TrySpend(10, 20));
            Assert.AreEqual(charge, clock.Charge, "a refused spend cost charge");
        }

        [Test]
        public void KeepsItsCharge_WhenThePlayerIsBackAtFullHealth()
        {
            var clock = new SalvageClock();
            Run(clock, HalfHealthSeconds + 0.5f, 10, 20);

            // A Max HP pick raises current with the ceiling, so full health can
            // arrive while a piece is due.
            Assert.IsFalse(clock.TrySpend(25, 25), "spent on a player with nothing to repair");
            Assert.IsTrue(clock.Ready);
            Assert.IsTrue(clock.TrySpend(24, 25), "the charge was lost instead of kept");
        }
    }
}
