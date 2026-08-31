using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The fight's three acts and the order they happen in.
    ///
    /// Every rule here is a "once, and never backwards" rule, and those are the
    /// ones that cannot be checked by playing: a phase that can be re-entered
    /// looks exactly like one that cannot until the frame where two bullets land
    /// together. That frame is reachable - the widest player spread fires six
    /// bullets in one instant at one target.
    /// </summary>
    public class BossPhaseStateTests
    {
        private static BossPhaseState State(int emplacements = 3, int scuttleThreshold = 30)
        {
            return new BossPhaseState(emplacements, scuttleThreshold);
        }

        [Test]
        public void TheFight_StartsArmoured()
        {
            BossPhaseState state = State();

            Assert.AreEqual(BossPhase.Armoured, state.Phase);
            Assert.IsFalse(state.HullVulnerable);
            Assert.AreEqual(3, state.EmplacementsStanding);
        }

        [Test]
        public void ABossWithNoEmplacements_StartsExposed()
        {
            // Otherwise it would be invulnerable for the whole fight, waiting for
            // a weak point that was never authored onto it.
            BossPhaseState state = State(emplacements: 0);

            Assert.AreEqual(BossPhase.Exposed, state.Phase);
            Assert.IsTrue(state.HullVulnerable);
        }

        [Test]
        public void WreckingSomeEmplacements_DoesNotExposeTheHull()
        {
            BossPhaseState state = State();

            Assert.IsFalse(state.ReportEmplacementDestroyed());
            Assert.IsFalse(state.ReportEmplacementDestroyed());

            Assert.AreEqual(BossPhase.Armoured, state.Phase);
            Assert.IsFalse(state.HullVulnerable);
            Assert.AreEqual(1, state.EmplacementsStanding);
        }

        [Test]
        public void WreckingTheLastEmplacement_ExposesTheHull()
        {
            BossPhaseState state = State();

            state.ReportEmplacementDestroyed();
            state.ReportEmplacementDestroyed();

            Assert.IsTrue(state.ReportEmplacementDestroyed(), "the phase changed");
            Assert.AreEqual(BossPhase.Exposed, state.Phase);
            Assert.IsTrue(state.HullVulnerable);
        }

        [Test]
        public void OnlyTheLastEmplacement_ReportsTheChange()
        {
            // The caller hangs the phase-entry beat off this - the silence, the
            // armour dropping - so a second true would play it twice.
            BossPhaseState state = State(emplacements: 1);

            Assert.IsTrue(state.ReportEmplacementDestroyed());
            Assert.IsFalse(state.ReportEmplacementDestroyed(), "already exposed");
        }

        [Test]
        public void ExtraEmplacementReports_DoNotDriveTheCountNegative()
        {
            BossPhaseState state = State(emplacements: 1);

            state.ReportEmplacementDestroyed();
            state.ReportEmplacementDestroyed();
            state.ReportEmplacementDestroyed();

            Assert.AreEqual(0, state.EmplacementsStanding);
            Assert.AreEqual(BossPhase.Exposed, state.Phase);
        }

        [Test]
        public void HealthAtTheThreshold_StartsTheScuttle()
        {
            BossPhaseState state = State(emplacements: 0, scuttleThreshold: 30);

            Assert.IsFalse(state.ReportHealth(31));
            Assert.IsTrue(state.ReportHealth(30), "at the threshold, not past it");
            Assert.AreEqual(BossPhase.Scuttle, state.Phase);
        }

        [Test]
        public void TheScuttle_IsReportedOnlyOnce()
        {
            // It fires every gun on the ship. Twice would double the cadence for
            // the rest of the fight, and the fight ends inside it.
            BossPhaseState state = State(emplacements: 0, scuttleThreshold: 30);

            Assert.IsTrue(state.ReportHealth(30));
            Assert.IsFalse(state.ReportHealth(29));
            Assert.IsFalse(state.ReportHealth(1));
        }

        [Test]
        public void TheScuttle_CannotStartWhileArmoured()
        {
            // Armour means the hull takes nothing, so health this low during the
            // first act says the emplacements were authored to cost more than the
            // boss has. Skipping to the finale is not the right answer to that -
            // the emplacements are still standing and still shooting.
            BossPhaseState state = State(emplacements: 3, scuttleThreshold: 30);

            Assert.IsFalse(state.ReportHealth(5));
            Assert.AreEqual(BossPhase.Armoured, state.Phase);
        }

        [Test]
        public void TheScuttle_SurvivesLaterEmplacementReports()
        {
            // An emplacement wrecked by a stray shot after the hull opened must
            // not walk the fight back to the middle act.
            BossPhaseState state = State(emplacements: 1, scuttleThreshold: 30);

            state.ReportEmplacementDestroyed();
            state.ReportHealth(10);

            Assert.IsFalse(state.ReportEmplacementDestroyed());
            Assert.AreEqual(BossPhase.Scuttle, state.Phase);
        }

        [Test]
        public void AZeroThreshold_HoldsTheScuttleUntilTheEnd()
        {
            BossPhaseState state = State(emplacements: 0, scuttleThreshold: 0);

            Assert.IsFalse(state.ReportHealth(1));
            Assert.IsTrue(state.ReportHealth(0));
        }

        [Test]
        public void TheMask_MatchesOnlyItsOwnPhase()
        {
            Assert.IsTrue(BossPhaseMask.Armoured.Includes(BossPhase.Armoured));
            Assert.IsFalse(BossPhaseMask.Armoured.Includes(BossPhase.Exposed));
            Assert.IsFalse(BossPhaseMask.Armoured.Includes(BossPhase.Scuttle));
        }

        [Test]
        public void TheMask_CanCarryAnAttackAcrossPhases()
        {
            BossPhaseMask mask = BossPhaseMask.Exposed | BossPhaseMask.Scuttle;

            Assert.IsFalse(mask.Includes(BossPhase.Armoured));
            Assert.IsTrue(mask.Includes(BossPhase.Exposed));
            Assert.IsTrue(mask.Includes(BossPhase.Scuttle));
        }

        [Test]
        public void AnEmptyMask_FiresInNoPhase()
        {
            // The value a freshly added field deserializes to if nothing sets it,
            // so it is worth knowing that it reads as silence rather than as all.
            Assert.IsFalse(BossPhaseMask.None.Includes(BossPhase.Armoured));
            Assert.IsFalse(BossPhaseMask.None.Includes(BossPhase.Exposed));
            Assert.IsFalse(BossPhaseMask.None.Includes(BossPhase.Scuttle));
        }

        [Test]
        public void TheAllMask_FiresInEveryPhase()
        {
            Assert.IsTrue(BossPhaseMask.All.Includes(BossPhase.Armoured));
            Assert.IsTrue(BossPhaseMask.All.Includes(BossPhase.Exposed));
            Assert.IsTrue(BossPhaseMask.All.Includes(BossPhase.Scuttle));
        }
    }
}
