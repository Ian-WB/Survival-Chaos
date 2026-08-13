using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Covers the rules that make an offer a choice: the pickups are spread so
    /// they cannot all be reached, and none of them lands on the player.
    ///
    /// The clamp gets the most attention here because it is the part that is
    /// wrong in the obvious implementation. Asking for a clearance wider than
    /// half the gap between neighbours rotates the whole set far enough that its
    /// tail comes back round onto the player - so the request for more clearance
    /// produces less of it.
    /// </summary>
    public class PickupPlacementTests
    {
        private const float Radius = ArenaGeometry.OrbitRadius;
        private static readonly Vector3 Center = new Vector3(3f, 0f, -7f);

        [Test]
        public void PointAt_IsTheInverseOfBearingOf()
        {
            foreach (float bearing in new[] { -179f, -90f, 0f, 37.5f, 90f, 180f })
            {
                Vector3 point = PickupPlacement.PointAt(bearing, Center, Radius, height: 4f);
                float returned = PickupPlacement.BearingOf(point, Center);

                // Compared as an angular difference, not as two numbers. Directly
                // behind the arena centre comes back as -180 where the input was
                // +180: the same direction, described by values 360 apart. Raw
                // equality calls that a failure, and any caller that did the same
                // would have the same bug - which is why Separation exists and
                // why nothing in the spawner compares bearings by subtraction.
                Assert.AreEqual(
                    0f,
                    Mathf.DeltaAngle(bearing, returned),
                    0.001f,
                    $"round trip failed at {bearing} degrees (came back as {returned})");
            }
        }

        [Test]
        public void PointAt_PutsThePickupOnTheOrbitCircleAtTheGivenHeight()
        {
            Vector3 point = PickupPlacement.PointAt(63f, Center, Radius, height: 9f);

            Vector3 flat = point - Center;
            flat.y = 0f;

            Assert.AreEqual(Radius, flat.magnitude, 0.001f, "not on the orbit circle");
            Assert.AreEqual(9f, point.y, 0.001f, "height was not applied");
        }

        [Test]
        public void BearingOf_FallsBackToTheStartingBearing_OnTheAxis()
        {
            // Degenerate rather than wrong: a point on the axis has no bearing.
            Assert.AreEqual(180f, PickupPlacement.BearingOf(Center, Center), 0.001f);
        }

        [Test]
        public void Bearings_ReturnsOnePerSkill()
        {
            Assert.AreEqual(3, PickupPlacement.Bearings(0f, 3, 55f, true).Length);
            Assert.AreEqual(1, PickupPlacement.Bearings(0f, 1, 55f, true).Length);
        }

        [Test]
        public void Bearings_ReturnsEmpty_WhenNothingIsOffered()
        {
            Assert.AreEqual(0, PickupPlacement.Bearings(0f, 0, 55f, true).Length);
            Assert.AreEqual(0, PickupPlacement.Bearings(0f, -2, 55f, true).Length);
        }

        [Test]
        public void Bearings_SpacesNeighboursEvenlyAroundTheRing()
        {
            float[] bearings = PickupPlacement.Bearings(0f, 3, 55f, clockwise: true);

            for (int i = 1; i < bearings.Length; i++)
            {
                Assert.AreEqual(
                    120f,
                    PickupPlacement.Separation(bearings[i - 1], bearings[i]),
                    0.001f,
                    $"gap {i} was not a third of the ring");
            }
        }

        [Test]
        public void Bearings_KeepEveryPickupClearOfThePlayer()
        {
            const float playerBearing = 20f;
            const float requested = 55f;

            float[] bearings = PickupPlacement.Bearings(playerBearing, 3, requested, true);

            foreach (float bearing in bearings)
            {
                Assert.GreaterOrEqual(
                    PickupPlacement.Separation(playerBearing, bearing),
                    requested - 0.001f,
                    "a pickup landed inside the clearance");
            }
        }

        [Test]
        public void Bearings_ClampTheClearance_SoTheFarEndDoesNotWrapOntoThePlayer()
        {
            // 90 degrees is more than half the 120 degree gap between three
            // pickups. Applied literally the set would sit at +90/+210/+330, and
            // that last one is only 30 degrees from the player - less clearance
            // than the caller asked for, in the direction they did not look.
            float[] bearings = PickupPlacement.Bearings(0f, 3, minSeparationDegrees: 90f, clockwise: true);

            foreach (float bearing in bearings)
            {
                Assert.GreaterOrEqual(
                    PickupPlacement.Separation(0f, bearing),
                    60f - 0.001f,
                    "the clamp did not hold the far end off the player");
            }
        }

        [Test]
        public void Bearings_MirrorWhenTheOfferGoesTheOtherWay()
        {
            float[] clockwise = PickupPlacement.Bearings(0f, 3, 55f, clockwise: true);
            float[] anticlockwise = PickupPlacement.Bearings(0f, 3, 55f, clockwise: false);

            for (int i = 0; i < clockwise.Length; i++)
            {
                Assert.AreEqual(
                    Mathf.DeltaAngle(0f, -clockwise[i]),
                    anticlockwise[i],
                    0.001f,
                    $"pickup {i} was not mirrored");
            }
        }

        [Test]
        public void Bearings_StayInTheRangeUnityAngleHelpersUse()
        {
            // Placed relative to a player near the wrap point, so the raw sums
            // run past 180 before being folded back.
            float[] bearings = PickupPlacement.Bearings(170f, 4, 40f, clockwise: true);

            foreach (float bearing in bearings)
            {
                Assert.That(bearing, Is.GreaterThan(-180.001f).And.LessThanOrEqualTo(180.001f));
            }
        }

        [Test]
        public void Separation_TakesTheShortWayRoundThroughTheWrapPoint()
        {
            // The raw difference is 350 degrees; the actual distance is 10.
            Assert.AreEqual(10f, PickupPlacement.Separation(175f, -175f), 0.001f);
        }
    }
}
