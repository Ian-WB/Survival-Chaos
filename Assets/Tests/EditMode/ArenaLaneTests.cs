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

        // The boss's geometry, as BuildBossRig measured it on the rig: muzzles
        // from 13.16 to 15.02 from the axis, and a player who can only be touched
        // between about 13.48 and 13.93. Written as distances from the lane rather than
        // as positions, because positions are what broke these tests last time -
        // they were 10x literals that went stale when the world came back to 1x,
        // and a start of 131.6 was no longer inside the lane but ten lanes out.

        /// <summary>How far the outermost muzzle sits outside the lane.</summary>
        private const float OutermostMuzzle = 1.30f;

        /// <summary>How far the innermost muzzle sits inside it.</summary>
        private const float InnermostMuzzle = 0.56f;

        /// <summary>
        /// How far outside the lane a shot can still touch the player, from the
        /// player's own hitbox and the shot's. It was 0.33 until the player's
        /// ship was halved on 11 September 2026; the band is mostly the ship.
        /// </summary>
        private const float HittableOuterEdge = 0.21f;

        /// <summary>A height inside the flight band, which runs 4.42 to 13.32.</summary>
        private const float BandHeight = 7f;

        /// <summary>The furthest a muzzle on the boss sits outside the lane.</summary>
        private static Vector3 FarOut => new Vector3(Radius + OutermostMuzzle, BandHeight, 0f);

        [Test]
        public void ClosesOnTheLaneWithoutReachingItInOneStep()
        {
            Vector3 next = ArenaGeometry.EaseOntoOrbit(FarOut, Centre, Radius, 5f, 1f / 60f);
            float radius = new Vector2(next.x, next.z).magnitude;

            Assert.Less(radius, Radius + OutermostMuzzle, "should have closed on the lane");
            Assert.Greater(radius, Radius, "should not have arrived in a single frame");
        }

        [Test]
        public void KeepsBearingAndHeight()
        {
            var start = new Vector3(0f, 9.17f, Radius + 1.08f);
            Vector3 next = ArenaGeometry.EaseOntoOrbit(start, Centre, Radius, 5f, 1f / 60f);

            Assert.AreEqual(start.y, next.y, 0.0001f, "height is the shot's own business");
            Assert.AreEqual(0f, next.x, 0.0001f, "bearing must not change");
            Assert.Greater(next.z, Radius);
            Assert.Less(next.z, start.z);
        }

        /// <summary>
        /// The number the boss is authored against: the worst-placed muzzle's
        /// shot has to be inside the band the player can be touched in before it
        /// has flown far enough to have missed its chance. BuildBossRig's
        /// RoundLaneResponse of 5 gets it there in about 0.36 seconds against
        /// the half-size ship; this checks it at 0.4, twenty-four frames at 60.
        /// </summary>
        [Test]
        public void ReachesTheHittableBandWithinFourTenthsOfASecond()
        {
            Vector3 shot = FarOut;

            for (int step = 0; step < 24; step++)
            {
                shot = ArenaGeometry.EaseOntoOrbit(shot, Centre, Radius, 5f, 1f / 60f);
            }

            float radius = new Vector2(shot.x, shot.z).magnitude;

            Assert.Less(radius - Radius, HittableOuterEdge, "0.4s in, the worst muzzle's shot can hit");
        }

        [Test]
        public void ClosesFromInsideTheLaneToo()
        {
            var start = new Vector3(Radius - InnermostMuzzle, BandHeight, 0f);
            Vector3 next = ArenaGeometry.EaseOntoOrbit(start, Centre, Radius, 5f, 1f / 60f);

            Assert.Greater(next.x, start.x);
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
