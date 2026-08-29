using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The dash's two windows. Worth pinning because both of them are invisible
    /// while playing: a burst that runs a frame long is a frame of free
    /// invincibility, and a cooldown measured from the wrong end is a dash that
    /// comes back sooner than the number in the inspector says.
    /// </summary>
    public class DashCycleTests
    {
        private static DashCycle Cycle(float duration = 0.2f, float cooldown = 1f)
        {
            return new DashCycle(duration, cooldown);
        }

        /// <summary>
        /// How far either side of a boundary these tests probe.
        ///
        /// The windows are compared as single-precision sums of a timestamp and a
        /// duration, so their edges are not at the decimal number they look like:
        /// 10f + 0.2f is 10.2000004, and asking whether 10.2f is inside it is a
        /// question about the last bit of a float rather than about the dash.
        /// Time.time never lands on an edge in play either. So each test probes
        /// clear of the boundary on both sides, which still catches every error
        /// worth catching - the smallest real defect here is a whole burst
        /// length, five hundred times this.
        /// </summary>
        private const float Margin = 0.01f;

        [Test]
        public void ANewCycle_IsReadyAndNotDashing()
        {
            DashCycle cycle = Cycle();

            Assert.IsTrue(cycle.IsReady(0f));
            Assert.IsFalse(cycle.IsDashing(0f));
        }

        [Test]
        public void ANewCycle_IsReadyAtAnyStartingTime()
        {
            // Time.time is whatever the session has been running for, so the
            // first dash of a run is never asked for at zero.
            DashCycle cycle = Cycle();

            Assert.IsTrue(cycle.IsReady(1837.5f));
        }

        [Test]
        public void Begin_StartsTheBurst()
        {
            DashCycle cycle = Cycle(duration: 0.2f);

            Assert.IsTrue(cycle.TryBegin(10f));

            Assert.IsTrue(cycle.IsDashing(10f));
            Assert.IsTrue(cycle.IsDashing(10.19f));
        }

        [Test]
        public void TheBurst_EndsAfterItsDuration()
        {
            DashCycle cycle = Cycle(duration: 0.2f);
            cycle.TryBegin(10f);

            Assert.IsTrue(cycle.IsDashing(10.2f - Margin), "still inside the burst");
            Assert.IsFalse(cycle.IsDashing(10.2f + Margin), "the burst is over");
        }

        [Test]
        public void Begin_IsRefusedWhileDashing()
        {
            DashCycle cycle = Cycle(duration: 0.2f);
            cycle.TryBegin(10f);

            Assert.IsFalse(cycle.TryBegin(10.1f));
        }

        [Test]
        public void Begin_IsRefusedDuringTheCooldown()
        {
            DashCycle cycle = Cycle(duration: 0.2f, cooldown: 1f);
            cycle.TryBegin(10f);

            Assert.IsFalse(cycle.TryBegin(11.2f - Margin));
        }

        [Test]
        public void TheCooldown_RunsFromTheEndOfTheBurstNotItsStart()
        {
            DashCycle cycle = Cycle(duration: 0.2f, cooldown: 1f);
            cycle.TryBegin(10f);

            // Ready at 10 + 0.2 + 1, not at 10 + 1. The gap between those two
            // readings is a whole burst length - a fifth of a second of dash the
            // player never paid for, growing with the authored duration - so this
            // is nowhere near the float noise the margin covers.
            Assert.IsFalse(cycle.IsReady(11f), "measured from the start of the burst");
            Assert.IsFalse(cycle.IsReady(11.2f - Margin));
            Assert.IsTrue(cycle.IsReady(11.2f + Margin));
        }

        [Test]
        public void ARefusedBegin_DoesNotPushTheCooldownBack()
        {
            DashCycle cycle = Cycle(duration: 0.2f, cooldown: 1f);
            cycle.TryBegin(10f);

            // Holding the key down is the normal case, so a refused attempt that
            // restarted the timer would mean a player who never lets go never
            // dashes again.
            cycle.TryBegin(10.5f);
            cycle.TryBegin(11f);

            Assert.IsTrue(cycle.IsReady(11.2f + Margin));
        }

        [Test]
        public void Begin_SucceedsAgainOnceTheCooldownHasPassed()
        {
            DashCycle cycle = Cycle(duration: 0.2f, cooldown: 1f);
            cycle.TryBegin(10f);

            Assert.IsTrue(cycle.TryBegin(11.2f + Margin));
            Assert.IsTrue(cycle.IsDashing(11.3f));
        }

        [Test]
        public void ZeroCooldown_AllowsAnotherBurstTheInstantTheLastOneEnds()
        {
            DashCycle cycle = Cycle(duration: 0.2f, cooldown: 0f);
            cycle.TryBegin(10f);

            Assert.IsFalse(cycle.TryBegin(10.1f));
            Assert.IsTrue(cycle.TryBegin(10.2f + Margin));
        }

        [Test]
        public void ReadyFraction_IsOneBeforeAnyDash()
        {
            Assert.AreEqual(1f, Cycle().ReadyFraction(0f));
        }

        [Test]
        public void ReadyFraction_IsZeroForTheWholeBurst()
        {
            DashCycle cycle = Cycle(duration: 0.2f, cooldown: 1f);
            cycle.TryBegin(10f);

            Assert.AreEqual(0f, cycle.ReadyFraction(10f));
            Assert.AreEqual(0f, cycle.ReadyFraction(10.19f));
        }

        [Test]
        public void ReadyFraction_RampsAcrossTheCooldown()
        {
            DashCycle cycle = Cycle(duration: 0.2f, cooldown: 1f);
            cycle.TryBegin(10f);

            Assert.AreEqual(0.5f, cycle.ReadyFraction(10.7f), 0.0001f);
            Assert.AreEqual(1f, cycle.ReadyFraction(11.2f), 0.0001f);
        }

        [Test]
        public void ReadyFraction_DoesNotRunPastOne()
        {
            DashCycle cycle = Cycle(duration: 0.2f, cooldown: 1f);
            cycle.TryBegin(10f);

            Assert.AreEqual(1f, cycle.ReadyFraction(400f));
        }

        [Test]
        public void NegativeTimings_AreTreatedAsZero()
        {
            // Both are serialized fields with a Range attribute, so this is a
            // guard against a value edited into the asset rather than through the
            // inspector - not against the inspector itself.
            DashCycle cycle = new DashCycle(-1f, -1f);

            Assert.IsTrue(cycle.TryBegin(10f));
            Assert.IsFalse(cycle.IsDashing(10f));
            Assert.IsTrue(cycle.IsReady(10f));
        }

        [Test]
        public void Reset_PutsTheCycleBackToNeverDashed()
        {
            DashCycle cycle = Cycle(duration: 0.2f, cooldown: 1f);
            cycle.TryBegin(10f);

            cycle.Reset();

            Assert.IsTrue(cycle.IsReady(10.1f));
            Assert.IsFalse(cycle.IsDashing(10.1f));
        }
    }
}
