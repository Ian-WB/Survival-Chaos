using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The turn decision that replaced six trigger volumes per enemy prefab.
    ///
    /// The sign is the whole risk here. Getting it backwards produces enemies
    /// that flee rather than chase, which looks like a movement bug rather than
    /// a comparison bug, so it is pinned twice: once against the direction the
    /// old trigger volumes asked for, and once against the direction that
    /// actually shortens the gap.
    /// </summary>
    public class RingChaseTests
    {
        private const float Band = RingChase.DefaultDeadbandDegrees;

        private static float Bearing(float x, float z)
        {
            return PickupPlacement.BearingOf(new Vector3(x, 70f, z), Vector3.zero);
        }

        /// <summary>
        /// The old geometry, read back as bearings. An enemy on +Z carried its
        /// leftOrRight = false volume out along world -X, so a player over there
        /// is what used to ask for false. Anything that flips the comparison
        /// breaks this and nothing else.
        /// </summary>
        [Test]
        public void MatchesTheDirectionTheOldTriggerVolumesAskedFor()
        {
            float enemy = Bearing(0f, 137.2f);

            Assert.IsFalse(
                RingChase.ShouldTravelLeft(enemy, Bearing(-137.2f, 0f), true, Band),
                "A player to world -X of an enemy on +Z asked for TravellingLeft = false.");

            Assert.IsTrue(
                RingChase.ShouldTravelLeft(enemy, Bearing(137.2f, 0f), false, Band),
                "And the mirrored volume asked for true.");
        }

        /// <summary>
        /// Travelling left is a positive rotation about +Y, which carries +Z
        /// towards +X - the direction BearingOf counts in. So chasing a player
        /// at the higher bearing has to mean travelling left, or the enemy is
        /// opening the gap rather than closing it.
        /// </summary>
        [Test]
        public void TurnsTowardsTheShorterWayRound()
        {
            Assert.IsTrue(RingChase.ShouldTravelLeft(0f, 90f, false, Band));
            Assert.IsFalse(RingChase.ShouldTravelLeft(0f, -90f, true, Band));
        }

        [Test]
        public void ComparesBearingsTheShortWayRoundAcrossTheWrap()
        {
            // 40 degrees apart, straddling the wrap. Subtracting the raw numbers
            // gives +320 and would send the enemy the long way round. Both cases
            // pass in the opposite of the expected answer as the current
            // direction, so holding course cannot make them pass.
            Assert.IsFalse(RingChase.ShouldTravelLeft(-170f, 150f, true, Band));
            Assert.IsTrue(RingChase.ShouldTravelLeft(170f, -150f, false, Band));
        }

        [Test]
        public void HoldsCourseWhileSittingOnThePlayer()
        {
            // Inside the band the shorter way flips every time the enemy
            // overtakes, so the answer has to be whatever it already was.
            Assert.IsTrue(RingChase.ShouldTravelLeft(0f, Band - 1f, true, Band));
            Assert.IsFalse(RingChase.ShouldTravelLeft(0f, Band - 1f, false, Band));
            Assert.IsTrue(RingChase.ShouldTravelLeft(0f, -(Band - 1f), true, Band));
            Assert.IsFalse(RingChase.ShouldTravelLeft(0f, -(Band - 1f), false, Band));
        }

        [Test]
        public void HoldsCourseAcrossTheAntipode()
        {
            // The far side flips just as readily, and for the same reason: at a
            // separation of 180 neither way round is shorter.
            Assert.IsTrue(RingChase.ShouldTravelLeft(0f, 180f - (Band - 1f), true, Band));
            Assert.IsFalse(RingChase.ShouldTravelLeft(0f, 180f - (Band - 1f), false, Band));
            Assert.IsTrue(RingChase.ShouldTravelLeft(0f, -(180f - (Band - 1f)), true, Band));
            Assert.IsFalse(RingChase.ShouldTravelLeft(0f, -(180f - (Band - 1f)), false, Band));
        }

        [Test]
        public void TurnsOnTheInstantWithNoBand()
        {
            Assert.IsTrue(RingChase.ShouldTravelLeft(0f, 1f, false, 0f));
            Assert.IsFalse(RingChase.ShouldTravelLeft(0f, -1f, true, 0f));
        }

        /// <summary>
        /// The two ambiguous zones grow from opposite ends, so a band of 90 or
        /// more would close the gap between them and leave an enemy that can
        /// never turn again. The clamp is what stops a careless number in the
        /// inspector doing that.
        /// </summary>
        [Test]
        public void StaysAbleToTurnAtAnyBandSetting()
        {
            Assert.IsTrue(RingChase.ShouldTravelLeft(0f, 90f, false, 200f));
            Assert.IsFalse(RingChase.ShouldTravelLeft(0f, -90f, true, 200f));
        }

        /// <summary>
        /// The coverage claim, as a test. Sweeping the ring, every bearing
        /// outside the two bands has to produce a decision - which is the whole
        /// difference from the trigger volumes, whose four working boxes covered
        /// 25.6 degrees of the 360 between them.
        /// </summary>
        [Test]
        public void DecidesEverywhereOutsideTheBands()
        {
            int decided = 0;

            for (int degrees = -179; degrees <= 180; degrees++)
            {
                float separation = Mathf.Abs(Mathf.DeltaAngle(0f, degrees));
                if (separation <= Band || separation >= 180f - Band)
                {
                    continue;
                }

                // Decided means the answer does not depend on what came before.
                bool fromLeft = RingChase.ShouldTravelLeft(0f, degrees, true, Band);
                bool fromRight = RingChase.ShouldTravelLeft(0f, degrees, false, Band);

                Assert.AreEqual(fromLeft, fromRight, "Undecided at {0} degrees.", degrees);
                decided++;
            }

            // 278 of the 360, against the 25.6 degrees the four working trigger
            // volumes covered between them.
            Assert.Greater(decided, 250);
        }
    }
}
