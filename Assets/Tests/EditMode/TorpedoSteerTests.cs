using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// How a crown torpedo flies and chases.
    ///
    /// The whole design is that it can miss, so these pin both halves of that
    /// with the numbers BuildBossRig authors: it arrives on a player who holds
    /// still or moves too soon, and it overshoots one who moves as it closes -
    /// and then that it handles like a torpedo, with a turning circle, a motor
    /// and a second pass.
    /// </summary>
    public class TorpedoSteerTests
    {
        private const float Floor = 4.42f;
        private const float Ceiling = 13.32f;

        /// <summary>The player's speed each way.</summary>
        private const float PlayerSpeed = 5.6f;

        /// <summary>The crown round the torpedo replaced: 80 degrees a second at the 18.72 lane.</summary>
        private const float OldRoundSpeed = 26.1f;

        /// <summary>
        /// Half the heights of the player's hit box (0.16) and a half-size
        /// torpedo's (0.09) together, and half their lengths along the ring
        /// (0.33 and 0.52): a hit is the two boxes overlapping, centre to centre.
        /// </summary>
        private const float HitHeight = 0.125f;
        private const float HitLength = 0.425f;

        private const float Frame = 1f / 60f;

        // What BuildBossRig gives the crown.
        private static readonly TorpedoHandling Crown = new TorpedoHandling
        {
            LaunchSpeed = 3f,
            CruiseSpeed = 8.5f,
            SpinUp = 0.6f,
            ArmSeconds = 0.3f,
            TurnRate = 90f,
            Perception = 1.6f,
            Fuel = 10f,
        };

        private static readonly Vector2 Muzzle = new Vector2(0f, 8f);

        private delegate Vector2 Mover(float time);

        /// <summary>
        /// Flies one torpedo from the muzzle along the ring at a player moving as
        /// <paramref name="player"/> says, and reports how close it came and when.
        /// </summary>
        private static float Closest(Mover player, float seconds, out float when, float frame = Frame)
        {
            return Fly(player, seconds, out when, out _, frame);
        }

        /// <summary>Whether it hits within <paramref name="seconds"/>.</summary>
        private static bool Hits(Mover player, float seconds)
        {
            Fly(player, seconds, out _, out bool hit);
            return hit;
        }

        private static float Fly(Mover player, float seconds, out float when, out bool hit, float frame = Frame)
        {
            Torpedo torpedo = Torpedo.Launch(Muzzle, 0f, player(0f));
            float closest = float.MaxValue;
            when = 0f;
            hit = false;

            for (float time = 0f; time < seconds; time += frame)
            {
                TorpedoSteer.Step(ref torpedo, player(time), Crown, Floor, Ceiling, frame);

                Vector2 apart = player(time + frame) - torpedo.Position;
                hit |= Mathf.Abs(apart.y) < HitHeight && Mathf.Abs(apart.x) < HitLength;

                if (apart.magnitude < closest)
                {
                    closest = apart.magnitude;
                    when = time + frame;
                }
            }

            return closest;
        }

        private static Mover StillAt(Vector2 place)
        {
            return time => place;
        }

        /// <summary>A player 20 along the ring who climbs flat out from <paramref name="start"/>.</summary>
        private static Mover ClimbingFrom(float start, float direction = 1f)
        {
            return time => new Vector2(20f,
                Mathf.Clamp(8f + direction * PlayerSpeed * Mathf.Max(0f, time - start), Floor, Ceiling));
        }

        /// <summary>
        /// A player 20 along the ring who moves <paramref name="units"/> up (or
        /// down, negative) at their full speed from <paramref name="start"/>, and
        /// then holds.
        /// </summary>
        private static Mover MovingBy(float units, float start)
        {
            return time => new Vector2(20f, Mathf.Clamp(
                8f + Mathf.Sign(units) * Mathf.Clamp(PlayerSpeed * (time - start), 0f, Mathf.Abs(units)),
                Floor, Ceiling));
        }

        /// <summary>When the torpedo reaches a player who never moves from 20 along.</summary>
        private static float Arrival()
        {
            Closest(StillAt(new Vector2(20f, 8f)), 5f, out float when);
            return when;
        }

        [Test]
        public void HitsAPlayerWhoHoldsStill()
        {
            Assert.IsTrue(Hits(StillAt(new Vector2(20f, 9.5f)), Crown.Fuel));
        }

        /// <summary>Moving too soon gives it time to follow you in, up or down.</summary>
        [Test]
        public void HitsAPlayerWhoDodgesTooEarly()
        {
            float arrival = Arrival();
            Assert.IsTrue(Hits(MovingBy(2f, arrival - 1.5f), arrival + 0.6f), "up");
            Assert.IsTrue(Hits(MovingBy(-2f, arrival - 1.5f), arrival + 0.6f), "down");
        }

        /// <summary>
        /// The miss: moving a couple of units in the last half second or so
        /// leaves it heading for where the player was, and it cannot turn hard
        /// enough to follow - up or down.
        /// </summary>
        [Test]
        public void MissesAPlayerWhoDodgesAsItCloses()
        {
            float arrival = Arrival();

            foreach (float units in new[] { 2f, -2f })
            {
                foreach (float lead in new[] { 0.7f, 0.5f, 0.3f })
                {
                    Assert.IsFalse(Hits(MovingBy(units, arrival - lead), arrival + 0.6f),
                        "moving " + units + " from " + lead + "s before it arrives");
                }
            }
        }

        /// <summary>
        /// Diving all the way to the floor is a timed escape, like the short
        /// dodge. It used to be none: at a cruise of 7.5 against a player at 7,
        /// one that dived half a second out was followed down and run into along
        /// the floor. Since 22 September (cruise 8.5, player 5.6) a dive started
        /// half a second to a second before it arrives gets away. Too early and it
        /// follows you down; too late and you are still in its path.
        /// </summary>
        [Test]
        public void DivingToTheFloor_EscapesOnlyIfTimed()
        {
            float arrival = Arrival();
            Assert.IsTrue(Hits(MovingBy(-10f, arrival - 1.5f), Crown.Fuel), "diving 1.5s before it arrives");

            foreach (float lead in new[] { 1f, 0.7f, 0.5f })
            {
                Assert.IsFalse(Hits(MovingBy(-10f, arrival - lead), Crown.Fuel),
                    "diving " + lead + "s before it arrives");
            }

            Assert.IsTrue(Hits(MovingBy(-10f, arrival - 0.3f), Crown.Fuel), "diving 0.3s before it arrives");
        }

        /// <summary>
        /// A player behind the muzzle at launch: it runs out, turns round along
        /// the ceiling and comes back.
        /// </summary>
        [Test]
        public void TurnsRoundForAPlayerBehindIt()
        {
            Assert.IsTrue(Hits(StillAt(new Vector2(-15f, 8f)), Crown.Fuel));
        }

        /// <summary>
        /// After a successful dodge it does not fly off: it comes about and
        /// heads back along the ring at the player, while it has fuel.
        /// </summary>
        [Test]
        public void ComesBackAfterAMiss()
        {
            float arrival = Arrival();
            float start = arrival - 0.5f;

            Mover player = time => new Vector2(20f,
                Mathf.Min(Ceiling, 8f + PlayerSpeed * Mathf.Clamp(time - start, 0f, 0.3f)));

            Assert.IsFalse(Hits(player, arrival + 0.6f), "the first pass should miss");

            Torpedo torpedo = Torpedo.Launch(Muzzle, 0f, player(0f));
            bool turnedBack = false;

            for (float time = 0f; time < Crown.Fuel; time += Frame)
            {
                TorpedoSteer.Step(ref torpedo, player(time), Crown, Floor, Ceiling, Frame);

                if (time > arrival && torpedo.Position.x > 20f && Mathf.Cos(torpedo.Heading * Mathf.Deg2Rad) < -0.7f)
                {
                    turnedBack = true;
                }
            }

            Assert.IsTrue(turnedBack, "it should come about and head back at the player");
        }

        /// <summary>
        /// Far slower than the round it replaced, and about half again the
        /// player's speed since the player slowed to 5.6 on 22 September - yet
        /// still no catch for a player who runs flat out along the ring from 20
        /// away. It steers at where it last saw the player, which runs a little
        /// behind them, so it closes to about 2.7 after six and a half seconds,
        /// passes that point and has to come about. Running buys time; it does
        /// not end the chase.
        /// </summary>
        [Test]
        public void GainsOnAPlayerWhoRuns_ButNeverCatchesThem()
        {
            Assert.Less(Crown.CruiseSpeed, OldRoundSpeed);
            Assert.Greater(Crown.CruiseSpeed, PlayerSpeed * 1.4f);
            Assert.Less(Crown.CruiseSpeed, PlayerSpeed * 1.6f);

            Mover fleeing = time => new Vector2(20f + PlayerSpeed * time, 8f);
            float closest = Closest(fleeing, Crown.Fuel, out _);
            Assert.Less(closest, 5f, "it should gain on them");
            Assert.Greater(closest, HitLength * 4f, "it should not reach them");
            Assert.IsFalse(Hits(fleeing, Crown.Fuel));
        }

        [Test]
        public void Thrust_LightsLowAtLaunch_BurnsFullAtCruise_AndGoesOutWithTheFuel()
        {
            Assert.AreEqual(TorpedoSteer.LaunchBurn, TorpedoSteer.Thrust(Crown, 0f), 1e-4f);
            Assert.AreEqual(1f, TorpedoSteer.Thrust(Crown, Crown.SpinUp), 1e-4f);
            Assert.AreEqual(1f, TorpedoSteer.Thrust(Crown, Crown.Fuel - 0.01f), 1e-4f);
            Assert.AreEqual(0f, TorpedoSteer.Thrust(Crown, Crown.Fuel), 1e-4f);

            float previous = 0f;
            for (float age = 0f; age <= Crown.SpinUp; age += 0.05f)
            {
                float burn = TorpedoSteer.Thrust(Crown, age);
                Assert.GreaterOrEqual(burn, previous);
                previous = burn;
            }
        }

        [Test]
        public void LeavesTheMuzzleStraightUntilArmed()
        {
            Torpedo torpedo = Torpedo.Launch(Muzzle, 0f, new Vector2(20f, 12f));

            for (float time = 0f; time + Frame < Crown.ArmSeconds; time += Frame)
            {
                TorpedoSteer.Step(ref torpedo, new Vector2(20f, 12f), Crown, Floor, Ceiling, Frame);
                Assert.AreEqual(0f, torpedo.Heading, 1e-4f);
            }

            Assert.AreEqual(Muzzle.y, torpedo.Position.y, 1e-4f);
        }

        [Test]
        public void SpinsUpFromLaunchSpeedToCruise()
        {
            Assert.AreEqual(Crown.LaunchSpeed, TorpedoSteer.Speed(Crown, 0f), 1e-4f);
            Assert.AreEqual(Crown.CruiseSpeed, TorpedoSteer.Speed(Crown, Crown.SpinUp), 1e-4f);
            Assert.AreEqual(Crown.CruiseSpeed, TorpedoSteer.Speed(Crown, 10f), 1e-4f);

            float previous = 0f;
            for (float age = 0f; age <= Crown.SpinUp; age += 0.05f)
            {
                float speed = TorpedoSteer.Speed(Crown, age);
                Assert.GreaterOrEqual(speed, previous);
                previous = speed;
            }
        }

        /// <summary>In a band too tall to need a pull-out, so only the steering turns it.</summary>
        [Test]
        public void NeverTurnsFasterThanItsTurnRate()
        {
            Torpedo torpedo = Torpedo.Launch(new Vector2(0f, 8.8f), 0f, new Vector2(-10f, 8.9f));

            for (int i = 0; i < 120; i++)
            {
                float before = torpedo.Heading;
                TorpedoSteer.Step(ref torpedo, new Vector2(-10f, 8.9f), Crown, -1000f, 1000f, Frame);

                Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(before, torpedo.Heading)),
                    Crown.TurnRate * Frame + 1e-3f, "step " + i);
            }
        }

        /// <summary>
        /// Even pulling out before an edge is a turn, not a snap - a snap reads
        /// as a glitch.
        /// </summary>
        [Test]
        public void NeverSnapsItsHeading_EvenAtAnEdge()
        {
            float limit = TorpedoSteer.EdgeTurnScale * Crown.TurnRate * Frame + 1e-3f;

            foreach (Vector2 player in new[] { new Vector2(-15f, 8f), new Vector2(-15f, 12.5f), new Vector2(20f, 4.5f) })
            {
                Torpedo torpedo = Torpedo.Launch(Muzzle, 0f, player);

                for (float time = 0f; time < Crown.Fuel; time += Frame)
                {
                    float before = torpedo.Heading;
                    TorpedoSteer.Step(ref torpedo, player, Crown, Floor, Ceiling, Frame);
                    Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(before, torpedo.Heading)), limit);
                }
            }
        }

        [Test]
        public void StaysInsideTheBand()
        {
            Torpedo torpedo = Torpedo.Launch(Muzzle, 80f, new Vector2(0f, Ceiling));

            for (int i = 0; i < 400; i++)
            {
                TorpedoSteer.Step(ref torpedo, new Vector2(0f, Ceiling), Crown, Floor, Ceiling, Frame);
                Assert.LessOrEqual(torpedo.Position.y, Ceiling);
                Assert.GreaterOrEqual(torpedo.Position.y, Floor);
            }
        }

        /// <summary>
        /// The pull-out: however steeply it heads for an edge, it arrives
        /// running along it rather than nose-first into it - chasing a player on
        /// the ceiling, and coming about for one behind, over the top and under.
        /// </summary>
        [Test]
        public void ArrivesAtAnEdgeRunningAlongIt()
        {
            var starts = new[]
            {
                (Torpedo.Launch(new Vector2(0f, 6f), 70f, new Vector2(30f, Ceiling)), new Vector2(30f, Ceiling)),
                (Torpedo.Launch(new Vector2(0f, 11f), 0f, new Vector2(-20f, 12f)), new Vector2(-20f, 12f)),
                (Torpedo.Launch(new Vector2(0f, 6f), 0f, new Vector2(-20f, 5f)), new Vector2(-20f, 5f)),
            };

            foreach (var (launched, player) in starts)
            {
                Torpedo torpedo = launched;

                for (float time = 0f; time < Crown.Fuel; time += Frame)
                {
                    TorpedoSteer.Step(ref torpedo, player, Crown, Floor, Ceiling, Frame);

                    if (torpedo.Position.y >= Ceiling - 0.02f || torpedo.Position.y <= Floor + 0.02f)
                    {
                        Assert.Less(Mathf.Abs(Mathf.Sin(torpedo.Heading * Mathf.Deg2Rad)), Mathf.Sin(15f * Mathf.Deg2Rad),
                            "at " + torpedo.Position + " heading " + torpedo.Heading);
                    }
                }
            }
        }

        /// <summary>
        /// Coming about for a player behind it, it goes toward whichever edge
        /// has more room - under with the ceiling close, over with the floor
        /// close.
        /// </summary>
        [Test]
        public void ComingAbout_GoesTowardTheEdgeWithMoreRoom()
        {
            int comingAbout = 0;
            Assert.Less(TorpedoSteer.Turn(0f, 170f, 5f, ref comingAbout, 1f, 7f), 0f);

            comingAbout = 0;
            Assert.Greater(TorpedoSteer.Turn(0f, -170f, 5f, ref comingAbout, 7f, 1f), 0f);
        }

        /// <summary>
        /// And keeps going the way it chose, even as climbing takes the room
        /// away from above - or it dithers and never comes round.
        /// </summary>
        [Test]
        public void ComingAbout_KeepsTheWayItChose()
        {
            int comingAbout = 0;
            float heading = TorpedoSteer.Turn(0f, 180f, 5f, ref comingAbout, 4.5f, 4.4f);
            Assert.AreEqual(5f, heading, 1e-4f);

            for (int i = 0; i < 20; i++)
            {
                float before = heading;
                heading = TorpedoSteer.Turn(heading, 180f, 5f, ref comingAbout, 1f, 8f);
                Assert.Greater(Mathf.DeltaAngle(before, heading), 0f, "turn " + i);
            }
        }

        /// <summary>With the player in front there is nothing to come about for: the short way.</summary>
        [Test]
        public void WithThePlayerInFront_TurnsTheShortWay()
        {
            int comingAbout = 0;
            Assert.AreEqual(5f, TorpedoSteer.Turn(0f, 60f, 5f, ref comingAbout, 1f, 8f), 1e-4f);
            Assert.AreEqual(0, comingAbout);
            Assert.AreEqual(-5f, TorpedoSteer.Turn(0f, -60f, 5f, ref comingAbout, 8f, 1f), 1e-4f);
        }

        [Test]
        public void OutOfFuel_FliesStraight()
        {
            Torpedo torpedo = Torpedo.Launch(Muzzle, 30f, new Vector2(0f, 8f));
            torpedo.Age = Crown.Fuel;

            for (int i = 0; i < 30; i++)
            {
                TorpedoSteer.Step(ref torpedo, new Vector2(-20f, 5f), Crown, Floor, Ceiling, Frame);
                Assert.AreEqual(30f, torpedo.Heading, 1e-4f);
            }
        }

        [Test]
        public void TheSameChase_AtAnyFrameRate()
        {
            Mover player = ClimbingFrom(1.5f);

            float fast = Closest(player, 3f, out float fastWhen, 1f / 120f);
            float slow = Closest(player, 3f, out float slowWhen, 1f / 30f);

            Assert.AreEqual(fast, slow, 0.35f);
            Assert.AreEqual(fastWhen, slowWhen, 0.1f);
        }

        [Test]
        public void ANoughtFrame_ChangesNothing()
        {
            Torpedo torpedo = Torpedo.Launch(Muzzle, 0f, new Vector2(20f, 12f));
            TorpedoSteer.Step(ref torpedo, new Vector2(20f, 12f), Crown, Floor, Ceiling, 0f);

            Assert.AreEqual(Muzzle, torpedo.Position);
            Assert.AreEqual(0f, torpedo.Age);
        }

        /// <summary>
        /// The roll puts the nose along the heading for both of the crown's
        /// rounds: nose on -X for the one fired with a positive orbit speed, on
        /// +X for the other. Facing the axis, +x of the flat ring is local -X,
        /// so a heading's direction in the round's own space is (-cos, sin).
        /// </summary>
        [Test]
        public void Roll_PointsTheNoseAlongTheHeading()
        {
            foreach (float heading in new[] { 0f, 30f, 90f, 150f, 180f, -45f, -120f })
            {
                float radians = heading * Mathf.Deg2Rad;
                Vector3 along = new Vector3(-Mathf.Cos(radians), Mathf.Sin(radians), 0f);

                Vector3 negativeNose = Quaternion.Euler(0f, 0f, TorpedoSteer.Roll(heading, true)) * Vector3.left;
                Vector3 positiveNose = Quaternion.Euler(0f, 0f, TorpedoSteer.Roll(heading, false)) * Vector3.right;

                Assert.Less(Vector3.Distance(along, negativeNose), 1e-4f, "nose on -X at " + heading);
                Assert.Less(Vector3.Distance(along, positiveNose), 1e-4f, "nose on +X at " + heading);
            }
        }

        /// <summary>At launch each round's nose is where it was always drawn: no roll.</summary>
        [Test]
        public void Roll_IsNoneAtLaunch()
        {
            Assert.AreEqual(0f, TorpedoSteer.Roll(0f, true), 1e-4f);
            Assert.AreEqual(0f, TorpedoSteer.Roll(180f, false), 1e-4f);
        }
    }
}
