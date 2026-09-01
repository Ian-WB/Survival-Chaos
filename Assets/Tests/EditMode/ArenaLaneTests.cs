using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Easing a projectile onto the lane the player flies in.
    ///
    /// Worth pinning because the failure is invisible from the outside. A shot
    /// that converges on the wrong thing still looks like a shot: it flies, it
    /// expires, it just never touches anybody, and the only symptom is a boss
    /// that feels thinner than the volley it is firing.
    /// </summary>
    public class ArenaLaneTests
    {
        private static readonly Vector3 Centre = Vector3.zero;

        private const float Radius = ArenaGeometry.OrbitRadius;

        /// <summary>The furthest a muzzle on the boss sits outside the lane.</summary>
        private static Vector3 FarOut => new Vector3(150.2f, 70f, 0f);

        [Test]
        public void ClosesOnTheLaneWithoutReachingItInOneStep()
        {
            Vector3 next = ArenaGeometry.EaseOntoOrbit(FarOut, Centre, Radius, 5f, 1f / 60f);
            float radius = new Vector2(next.x, next.z).magnitude;

            Assert.Less(radius, 150.2f, "should have closed on the lane");
            Assert.Greater(radius, Radius, "should not have arrived in a single frame");
        }

        [Test]
        public void KeepsBearingAndHeight()
        {
            var start = new Vector3(0f, 91.7f, 148f);
            Vector3 next = ArenaGeometry.EaseOntoOrbit(start, Centre, Radius, 5f, 1f / 60f);

            Assert.AreEqual(91.7f, next.y, 0.0001f, "height is the shot's own business");
            Assert.AreEqual(0f, next.x, 0.0001f, "bearing must not change");
            Assert.Greater(next.z, Radius);
            Assert.Less(next.z, 148f);
        }

        /// <summary>
        /// The number the boss is authored against: the worst-placed muzzle has
        /// to be inside the band the player can be touched in - 137.2 give or
        /// take 3.29, from the player's own hitbox and the shot's - before the
        /// shot has flown far enough to have missed its chance.
        /// </summary>
        [Test]
        public void ReachesTheHittableBandWithinAQuarterSecond()
        {
            Vector3 shot = FarOut;

            for (int step = 0; step < 18; step++)
            {
                shot = ArenaGeometry.EaseOntoOrbit(shot, Centre, Radius, 5f, 1f / 60f);
            }

            float radius = new Vector2(shot.x, shot.z).magnitude;

            Assert.Less(radius - Radius, 3.29f, "0.3s in, the worst muzzle's shot can hit");
        }

        [Test]
        public void ClosesFromInsideTheLaneToo()
        {
            var start = new Vector3(131.6f, 53.8f, 0f);
            Vector3 next = ArenaGeometry.EaseOntoOrbit(start, Centre, Radius, 5f, 1f / 60f);

            Assert.Greater(next.x, 131.6f);
            Assert.Less(next.x, Radius);
        }

        [Test]
        public void SettlesAndStays()
        {
            Vector3 shot = FarOut;

            for (int step = 0; step < 600; step++)
            {
                shot = ArenaGeometry.EaseOntoOrbit(shot, Centre, Radius, 5f, 1f / 60f);
            }

            Assert.AreEqual(Radius, new Vector2(shot.x, shot.z).magnitude, 0.01f);
        }

        /// <summary>
        /// ShipMotion's convention, inherited deliberately: a response of zero is
        /// a snap, not a stop. Callers that want it switched off test for that
        /// themselves, and this pins the behaviour they are testing against.
        /// </summary>
        [Test]
        public void ZeroResponseSnapsRatherThanHolding()
        {
            Vector3 next = ArenaGeometry.EaseOntoOrbit(FarOut, Centre, Radius, 0f, 1f / 60f);

            Assert.AreEqual(Radius, new Vector2(next.x, next.z).magnitude, 0.0001f);
        }
    }
}
