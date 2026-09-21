using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// A boss round thrown at an angle, bouncing off the band's floor and ceiling.
    ///
    /// Pinned against the real band and the throw BuildBossRig authors, because
    /// what matters is spatial: the disc leaves from its muzzle, never leaves the
    /// band once inside it, turns back at each edge rather than passing through,
    /// and is never thrown straight out of the fight from a muzzle that sits
    /// below the floor.
    /// </summary>
    public class RoundRouteTests
    {
        /// <summary>The player's band, off the ApplyBounds box in the Game scene.</summary>
        private const float Floor = 4.42f;
        private const float Ceiling = 13.32f;

        /// <summary>What BuildBossRig throws the keel's discs at.</summary>
        private const float Throw = 4f;

        /// <summary>The player's climb speed, which a thrown wall must stay under.</summary>
        private const float PlayerClimb = 7f;

        private const float Tolerance = 1e-3f;

        [Test]
        public void LeavesFromItsMuzzle()
        {
            Assert.AreEqual(8f, RoundRoute.Height(8f, Throw, 0f, Floor, Ceiling), Tolerance);
        }

        [Test]
        public void ClimbsAtItsThrowSpeed_BeforeReachingAnEdge()
        {
            Assert.AreEqual(9f, RoundRoute.Height(8f, Throw, 0.25f, Floor, Ceiling), Tolerance);
            Assert.AreEqual(7f, RoundRoute.Height(8f, -Throw, 0.25f, Floor, Ceiling), Tolerance);
        }

        /// <summary>
        /// Thrown up from 12, it meets the ceiling after 0.33s and comes back
        /// down: half a second in it has covered 2 units, 1.32 up and 0.68 back.
        /// </summary>
        [Test]
        public void TurnsBackOffTheCeiling()
        {
            Assert.AreEqual(Ceiling - 0.68f, RoundRoute.Height(12f, Throw, 0.5f, Floor, Ceiling), Tolerance);
        }

        [Test]
        public void TurnsBackOffTheFloor()
        {
            Assert.AreEqual(Floor + 0.42f, RoundRoute.Height(5f, -Throw, 0.25f, Floor, Ceiling), Tolerance);
        }

        [Test]
        public void NeverLeavesTheBand_OnceInside()
        {
            for (float t = 0f; t < 20f; t += 0.013f)
            {
                float height = RoundRoute.Height(6f, Throw, t, Floor, Ceiling);

                Assert.GreaterOrEqual(height, Floor - Tolerance, "at t = " + t);
                Assert.LessOrEqual(height, Ceiling + Tolerance, "at t = " + t);
            }
        }

        /// <summary>
        /// Worked out from elapsed time, so a long frame lands where the short
        /// ones would have - it cannot carry a disc through an edge.
        /// </summary>
        [Test]
        public void ALongFrameCannotCarryItThroughAnEdge()
        {
            Assert.LessOrEqual(RoundRoute.Height(13f, Throw, 3f, Floor, Ceiling), Ceiling + Tolerance);
        }

        /// <summary>Up the band and back down again returns it to where it started.</summary>
        [Test]
        public void ComesBackToItsStart_AfterOneRoundTrip()
        {
            float roundTrip = 2f * (Ceiling - Floor) / Throw;

            Assert.AreEqual(7f, RoundRoute.Height(7f, Throw, roundTrip, Floor, Ceiling), Tolerance);
        }

        /// <summary>
        /// Two discs thrown at the same speed keep their distance until one of
        /// them reaches an edge - the arithmetic the fan's matching slots on two
        /// rows rely on to stay a row apart.
        /// </summary>
        [Test]
        public void TwoDiscsThrownAlike_KeepTheirDistance_UntilAnEdge()
        {
            float low = 5f;
            float high = 7.4f;

            for (float t = 0f; t < 1.4f; t += 0.05f)
            {
                float gap = RoundRoute.Height(high, Throw, t, Floor, Ceiling)
                            - RoundRoute.Height(low, Throw, t, Floor, Ceiling);

                Assert.AreEqual(high - low, gap, Tolerance, "at t = " + t);
            }
        }

        [Test]
        public void FromUnderTheFloor_TravelsStraightUntilItEnters()
        {
            Assert.AreEqual(3.42f, RoundRoute.Height(2.42f, Throw, 0.25f, Floor, Ceiling), Tolerance);
        }

        [Test]
        public void FromUnderTheFloor_BouncesOnceInside()
        {
            for (float t = 1f; t < 20f; t += 0.05f)
            {
                float height = RoundRoute.Height(1f, Throw, t, Floor, Ceiling);

                Assert.GreaterOrEqual(height, Floor - Tolerance, "at t = " + t);
                Assert.LessOrEqual(height, Ceiling + Tolerance, "at t = " + t);
            }
        }

        [Test]
        public void UnderTheFloor_IsAlwaysThrownUpward()
        {
            Assert.Greater(RoundRoute.SpeedInto(1f, -Throw, Floor, Ceiling), 0f);
        }

        [Test]
        public void OverTheCeiling_IsAlwaysThrownDownward()
        {
            Assert.Less(RoundRoute.SpeedInto(15f, Throw, Floor, Ceiling), 0f);
        }

        [Test]
        public void InsideTheBand_IsThrownAsGiven()
        {
            Assert.AreEqual(Throw, RoundRoute.SpeedInto(8f, Throw, Floor, Ceiling));
            Assert.AreEqual(-1.5f, RoundRoute.SpeedInto(8f, -1.5f, Floor, Ceiling));
        }

        /// <summary>
        /// A keel row is four muzzles at one height. Every one of them has to
        /// leave on a different path, or two discs leave together - which is
        /// what a row thrown at one angle did in play.
        /// </summary>
        [Test]
        public void AFourMuzzleRow_FansIntoFourDifferentThrows()
        {
            var seen = new System.Collections.Generic.HashSet<float>();

            for (int slot = 0; slot < 4; slot++)
            {
                Assert.IsTrue(seen.Add(RoundRoute.FanSpeed(Throw, slot, 4)), "slot " + slot + " repeats a throw");
            }
        }

        [Test]
        public void TheFan_RunsFromSteepUpToSteepDown()
        {
            Assert.AreEqual(Throw, RoundRoute.FanSpeed(Throw, 0, 4), Tolerance);
            Assert.AreEqual(Throw / 3f, RoundRoute.FanSpeed(Throw, 1, 4), Tolerance);
            Assert.AreEqual(-Throw / 3f, RoundRoute.FanSpeed(Throw, 2, 4), Tolerance);
            Assert.AreEqual(-Throw, RoundRoute.FanSpeed(Throw, 3, 4), Tolerance);
        }


        [Test]
        public void TheFan_NeverThrowsFasterThanTheSteepEnd()
        {
            for (int slot = 0; slot < 4; slot++)
            {
                Assert.LessOrEqual(Mathf.Abs(RoundRoute.FanSpeed(Throw, slot, 4)), Throw + Tolerance);
            }
        }

        [Test]
        public void ARowOfOne_IsThrownAtFull()
        {
            Assert.AreEqual(Throw, RoundRoute.FanSpeed(Throw, 0, 1), Tolerance);
        }

        [Test]
        public void NoThrow_HoldsHeight()
        {
            Assert.AreEqual(8f, RoundRoute.Height(8f, 0f, 3f, Floor, Ceiling));
        }

        /// <summary>A thrown wall has to be outflyable, or it is not dodgeable.</summary>
        [Test]
        public void TheThrow_IsSlowerThanThePlayerCanClimb()
        {
            Assert.Less(Throw, PlayerClimb);
        }
    }
}
