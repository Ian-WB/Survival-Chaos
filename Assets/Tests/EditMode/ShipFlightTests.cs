using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    public class ShipFlightTests
    {
        private const float Tolerance = 0.001f;

        private static readonly ShipBasis[] Bases = { ShipBasis.NoseAlongX, ShipBasis.NoseAlongZ };

        [Test]
        public void AtRest_TheModelIsNotTurned()
        {
            foreach (ShipBasis basis in Bases)
            {
                Quaternion rotation = ShipFlight.Rotation(basis, 0f, 0f, 0f);
                Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, rotation), Tolerance);
            }
        }

        [Test]
        public void Pitch_LiftsTheNose_WhicheverWayTheShipFaces([Values(0f, 90f, 180f, -90f)] float yaw)
        {
            // The bug this replaced: the player's climb dropped the nose facing
            // one way and lifted it facing the other.
            foreach (ShipBasis basis in Bases)
            {
                Vector3 nose = ShipFlight.Rotation(basis, yaw, 20f, 0f) * basis.Nose;
                Assert.AreEqual(Mathf.Sin(20f * Mathf.Deg2Rad), nose.y, Tolerance);
            }
        }

        [Test]
        public void Roll_BanksToThePilotsRight()
        {
            foreach (ShipBasis basis in Bases)
            {
                Quaternion rotation = ShipFlight.Rotation(basis, 0f, 0f, 30f);
                Vector3 roof = rotation * basis.Up;
                Vector3 nose = rotation * basis.Nose;

                Assert.Greater(Vector3.Dot(roof, basis.Right), 0.4f, "the roof tips to the right");
                Assert.AreEqual(1f, Vector3.Dot(nose, basis.Nose), Tolerance, "about the nose");
            }
        }

        [Test]
        public void Yaw_TurnsToThePilotsRight()
        {
            foreach (ShipBasis basis in Bases)
            {
                Vector3 nose = ShipFlight.NoseAt(basis, 90f);
                Assert.AreEqual(1f, Vector3.Dot(nose, basis.Right), Tolerance);
            }
        }

        [Test]
        public void TheEnemyFacings_AreTheOnesTheModelsWereSetTo()
        {
            // EnemySpaceShip wrote Euler(0, -90, 0) travelling left and
            // (0, 90, 0) travelling right; the rig has to land on the same.
            Assert.AreEqual(0f, Quaternion.Angle(
                Quaternion.Euler(0f, -90f, 0f), ShipFlight.Rotation(ShipBasis.NoseAlongZ, -90f, 0f, 0f)), Tolerance);
            Assert.AreEqual(0f, Quaternion.Angle(
                Quaternion.Euler(0f, 90f, 0f), ShipFlight.Rotation(ShipBasis.NoseAlongZ, 90f, 0f, 0f)), Tolerance);
        }

        [Test]
        public void ThePlayersFlip_IsTheOneTheModelWasSetTo()
        {
            Assert.AreEqual(0f, Quaternion.Angle(
                Quaternion.Euler(0f, 180f, 0f), ShipFlight.Rotation(ShipBasis.NoseAlongX, 180f, 0f, 0f)), Tolerance);
        }

        [Test]
        public void ABarrelRoll_GoesAllTheWayRound_WithoutMovingTheNose()
        {
            ShipBasis basis = ShipBasis.NoseAlongX;

            for (int i = 0; i <= 20; i++)
            {
                float angle = ShipFlight.BarrelRoll(i / 20f, 1);
                Vector3 nose = ShipFlight.Rotation(basis, 0f, 15f, angle) * basis.Nose;
                Vector3 level = ShipFlight.Rotation(basis, 0f, 15f, 0f) * basis.Nose;

                Assert.AreEqual(0f, Vector3.Distance(nose, level), Tolerance);
            }
        }

        [Test]
        public void ABarrelRoll_StartsAndEndsLevel_AndNeverGoesBack()
        {
            Assert.AreEqual(0f, ShipFlight.BarrelRoll(0f, 1), Tolerance);
            Assert.AreEqual(360f, ShipFlight.BarrelRoll(1f, 1), Tolerance);
            Assert.AreEqual(-720f, ShipFlight.BarrelRoll(1f, -2), Tolerance);
            Assert.AreEqual(180f, ShipFlight.BarrelRoll(0.5f, 1), Tolerance);

            float last = 0f;
            for (int i = 1; i <= 50; i++)
            {
                float angle = ShipFlight.BarrelRoll(i / 50f, 1);
                Assert.GreaterOrEqual(angle, last);
                last = angle;
            }
        }

        [Test]
        public void Surge_IsTheChangeInThrottle_AndFadesAsTheShipSettles()
        {
            // Setting off: all of it is new.
            Assert.AreEqual(1f, ShipFlight.Surge(1f, 0f, 0.25f), Tolerance);

            // Settled at speed: only the held share is left.
            Assert.AreEqual(0.25f, ShipFlight.Surge(1f, 1f, 0.25f), Tolerance);

            // Letting go: the ship rocks back the other way.
            Assert.AreEqual(-1f, ShipFlight.Surge(0f, 1f, 0f), Tolerance);

            // A hold of 1 is the old behaviour, a lean for as long as the key.
            Assert.AreEqual(1f, ShipFlight.Surge(1f, 1f, 1f), Tolerance);
            Assert.AreEqual(1f, ShipFlight.Surge(1f, 0.3f, 1f), Tolerance);
        }

        [Test]
        public void AnEnemyReversing_TurnsThroughTheCamera_BothWays()
        {
            const float left = -90f, right = 90f, outward = 180f;

            float toRight = ShipFlight.TurnThrough(left, right, outward);
            Assert.AreEqual(-270f, toRight, Tolerance, "left to right goes down through -180");

            float backToLeft = ShipFlight.TurnThrough(toRight, left, outward);
            Assert.AreEqual(-90f, backToLeft, Tolerance, "and comes back the way it went");
        }

        [Test]
        public void ThePlayersFlip_TurnsThroughTheCamera_BothWays()
        {
            float flipped = ShipFlight.TurnThrough(0f, 180f, 90f);
            Assert.AreEqual(180f, flipped, Tolerance);

            float back = ShipFlight.TurnThrough(flipped, 0f, 90f);
            Assert.AreEqual(0f, back, Tolerance);
        }

        [Test]
        public void ASmallTurn_GoesTheShortWay()
        {
            // The fly-in ends facing the island and turns a quarter onto the ring.
            Assert.AreEqual(-90f, ShipFlight.TurnThrough(0f, -90f, 180f), Tolerance);
            Assert.AreEqual(90f, ShipFlight.TurnThrough(0f, 90f, 180f), Tolerance);

            // Already there, by any name for the angle.
            Assert.AreEqual(-270f, ShipFlight.TurnThrough(-270f, 90f, 180f), Tolerance);
        }

        [Test]
        public void TheFacing_NeverRunsAway_OverManyReversals()
        {
            float facing = -90f;

            for (int i = 0; i < 200; i++)
            {
                facing = ShipFlight.TurnThrough(facing, i % 2 == 0 ? 90f : -90f, 180f);
            }

            Assert.LessOrEqual(Mathf.Abs(facing), 360f);
        }

        [Test]
        public void Share_IsASpeedAgainstFull_AndStopsAtOne()
        {
            Assert.AreEqual(0.5f, ShipFlight.Share(1f, 2f), Tolerance);
            Assert.AreEqual(-1f, ShipFlight.Share(-9f, 2f), Tolerance);
            Assert.AreEqual(0f, ShipFlight.Share(3f, 0f), Tolerance);
        }
    }
}
