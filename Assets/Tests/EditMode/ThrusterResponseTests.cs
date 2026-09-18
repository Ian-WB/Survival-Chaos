using NUnit.Framework;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The thrusters are a visual, so most of what they do can only be judged by
    /// looking at them. These pin the part that cannot: which nozzle burns.
    ///
    /// The ship turns end for end at the player's command, and the input keeps
    /// its sign through that, so every one of these questions has an answer that
    /// is right on an unflipped ship and backwards on a flipped one. Finding that
    /// out in a build means watching exhaust come out of the nose.
    /// </summary>
    public class ThrusterResponseTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void UnflippedShip_ReadsTheThrottleAsGiven()
        {
            Assert.AreEqual(1f, ThrusterResponse.Noseward(1f, flipped: false), Tolerance);
            Assert.AreEqual(-0.4f, ThrusterResponse.Noseward(-0.4f, flipped: false), Tolerance);
        }

        [Test]
        public void FlippedShip_ReadsTheThrottleReversed()
        {
            Assert.AreEqual(-1f, ThrusterResponse.Noseward(1f, flipped: true), Tolerance);
            Assert.AreEqual(0.4f, ThrusterResponse.Noseward(-0.4f, flipped: true), Tolerance);
        }

        /// <summary>
        /// The whole reason <see cref="ThrusterResponse.Noseward"/> exists: the
        /// key that drives a flipped ship along its own nose is the opposite one,
        /// and the main engine still has to be the thing that lights.
        /// </summary>
        [Test]
        public void EitherWayRound_TravellingNoseFirst_BurnsTheMainEngine()
        {
            foreach (bool flipped in new[] { false, true })
            {
                float held = flipped ? -1f : 1f;
                float noseward = ThrusterResponse.Noseward(held, flipped);

                Assert.AreEqual(1f, Main(noseward), Tolerance, "flipped: " + flipped);
                Assert.AreEqual(0f, Retro(noseward), Tolerance, "flipped: " + flipped);
            }
        }

        [Test]
        public void BackingUp_BurnsTheRetrosAndNotTheMainEngine()
        {
            float noseward = ThrusterResponse.Noseward(-1f, flipped: false);

            Assert.AreEqual(0f, Main(noseward), Tolerance);
            Assert.AreEqual(1f, Retro(noseward), Tolerance);
        }

        [Test]
        public void AHalfHeldThrottle_BurnsHalfAsHard()
        {
            Assert.AreEqual(0.5f, Main(0.5f), Tolerance);
        }

        /// <summary>
        /// A dash straight up or down leaves both throttle axes reading zero, and
        /// that is exactly the manoeuvre that most needs an engine behind it.
        /// </summary>
        [Test]
        public void AVerticalDash_StillLightsBothNozzles()
        {
            float level = ThrusterResponse.NozzleLevel(0f, dashing: true, dashFloor: 0.7f, dashBoost: 2f);

            Assert.AreEqual(1.4f, level, Tolerance);
        }

        /// <summary>
        /// Seen in play on 18 September 2026 before the floor learned to fade:
        /// a forward dash fired the retros too, and a green spike out of the nose
        /// reads as a weapon rather than as thrust.
        /// </summary>
        [Test]
        public void ADashAlongTheRing_LeavesTheOpposingNozzleDark()
        {
            float noseward = ThrusterResponse.Noseward(1f, flipped: false);

            Assert.AreEqual(2f, ThrusterResponse.NozzleLevel(noseward, true, 0.7f, 2f), Tolerance);
            Assert.AreEqual(0f, ThrusterResponse.NozzleLevel(-noseward, true, 0.7f, 2f), Tolerance);
        }

        /// <summary>A burst half off the ring keeps half of its floor.</summary>
        [Test]
        public void ADiagonalDash_FadesTheOpposingNozzleRatherThanCuttingIt()
        {
            float level = ThrusterResponse.NozzleLevel(-0.5f, dashing: true, dashFloor: 0.7f, dashBoost: 2f);

            Assert.AreEqual(0.7f, level, Tolerance);
        }

        [Test]
        public void ADashAlongTheRing_OvershootsAFullThrottle()
        {
            float held = ThrusterResponse.NozzleLevel(1f, dashing: false, dashFloor: 0.7f, dashBoost: 2f);
            float dashed = ThrusterResponse.NozzleLevel(1f, dashing: true, dashFloor: 0.7f, dashBoost: 2f);

            Assert.AreEqual(1f, held, Tolerance);
            Assert.AreEqual(2f, dashed, Tolerance);
            Assert.That(dashed, Is.GreaterThan(held));
        }

        [Test]
        public void ABoostBelowOne_IsIgnoredRatherThanDimmingTheDash()
        {
            float level = ThrusterResponse.NozzleLevel(1f, dashing: true, dashFloor: 0.7f, dashBoost: 0.2f);

            Assert.AreEqual(1f, level, Tolerance);
        }

        [Test]
        public void TheReadyLight_IsOutWhileTheDashIsSpent()
        {
            Assert.AreEqual(0f, ThrusterResponse.ReadyLevel(0f, carry: 0.25f), Tolerance);
        }

        [Test]
        public void TheReadyLight_FillsDimlyThroughTheCooldown()
        {
            Assert.AreEqual(0.125f, ThrusterResponse.ReadyLevel(0.5f, carry: 0.25f), Tolerance);
            Assert.AreEqual(0.2375f, ThrusterResponse.ReadyLevel(0.95f, carry: 0.25f), Tolerance);
        }

        [Test]
        public void TheReadyLight_ComesFullyOnOnlyWhenTheDashIsBack()
        {
            Assert.AreEqual(1f, ThrusterResponse.ReadyLevel(1f, carry: 0.25f), Tolerance);
        }

        [Test]
        public void TheFlash_StartsAtFullStrengthAndDecaysToNothing()
        {
            Assert.AreEqual(2.5f, ThrusterResponse.ReadyPop(0f, 0.25f, 1.5f), Tolerance);
            Assert.AreEqual(1.75f, ThrusterResponse.ReadyPop(0.125f, 0.25f, 1.5f), Tolerance);
            Assert.AreEqual(1f, ThrusterResponse.ReadyPop(0.25f, 0.25f, 1.5f), Tolerance);
            Assert.AreEqual(1f, ThrusterResponse.ReadyPop(10f, 0.25f, 1.5f), Tolerance);
        }

        /// <summary>
        /// The component seeds its timestamp at negative infinity, so the first
        /// frame of a run asks about a flash that happened infinitely long ago.
        /// </summary>
        [Test]
        public void AFlashThatNeverHappened_DoesNotFire()
        {
            Assert.AreEqual(1f, ThrusterResponse.ReadyPop(float.PositiveInfinity, 0.25f, 1.5f), Tolerance);
        }

        [Test]
        public void NoFlashLength_MeansNoFlash()
        {
            Assert.AreEqual(1f, ThrusterResponse.ReadyPop(0f, 0f, 1.5f), Tolerance);
        }

        private static float Main(float noseward)
        {
            return ThrusterResponse.NozzleLevel(noseward, dashing: false, dashFloor: 0.7f, dashBoost: 2f);
        }

        private static float Retro(float noseward)
        {
            return ThrusterResponse.NozzleLevel(-noseward, dashing: false, dashFloor: 0.7f, dashBoost: 2f);
        }
    }
}
