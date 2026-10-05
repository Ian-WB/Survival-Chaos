using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// How a crown torpedo flies and chases.
    ///
    /// The whole design is that it can miss, so these pin both halves of that:
    /// it arrives on a player who holds still or moves too soon, and it
    /// overshoots one who moves as it closes - and then that it handles like a
    /// torpedo, with a turning circle, a motor and a second pass.
    ///
    /// The fight is run twice. Once on the numbers these were written against
    /// (<see cref="AsWritten"/>), which pins the steering and cannot drift. And
    /// once on the fight as it is today (<see cref="Current"/>): the band and
    /// the player's hit box off the Game scene, the torpedo off the boss prefab.
    /// Until 26 September 2026 there was only the first, and it went on passing
    /// after the band grew to 10.5 and the ships by a quarter, while saying
    /// nothing about the torpedo against the bigger player (audit of that day).
    /// </summary>
    public class TorpedoSteerTests
    {
        /// <summary>
        /// The band these were written in, on 22 September 2026. The steering
        /// tests below that are not about the fight use it too: any band would
        /// do for them.
        /// </summary>
        private const float Floor = 4.42f;
        private const float Ceiling = 13.32f;

        /// <summary>The crown round the torpedo replaced: 80 degrees a second at the 18.72 lane.</summary>
        private const float OldRoundSpeed = 26.1f;

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

        /// <summary>Everything about the fight that decides whether a torpedo hits.</summary>
        private sealed class Fight
        {
            public string Name;
            public float Floor;
            public float Ceiling;

            /// <summary>The player's speed each way.</summary>
            public float PlayerSpeed;

            /// <summary>
            /// Half the heights of the player's hit box and the torpedo's
            /// together, and half their lengths along the ring: a hit is the two
            /// boxes overlapping, centre to centre.
            /// </summary>
            public float HitHeight;
            public float HitLength;

            public TorpedoHandling Crown;
        }

        /// <summary>
        /// The fight on 22 September 2026: the player's hit box 0.16 tall and
        /// 0.33 long, a half-size torpedo's 0.09 and 0.52, in the 4.42 to 13.32
        /// band, the player at 5.6.
        /// </summary>
        private static readonly Fight AsWritten = new Fight
        {
            Name = "as written",
            Floor = Floor,
            Ceiling = Ceiling,
            PlayerSpeed = 5.6f,
            HitHeight = 0.125f,
            HitLength = 0.425f,
            Crown = Crown,
        };

        /// <summary>
        /// The fight as the project has it now. The player and the band live in
        /// the Game scene rather than a prefab, and are read from the scene as
        /// saved, which is what a build gets: see <see cref="SavedScene"/>.
        /// </summary>
        private static Fight Current()
        {
            SavedScene scene = SavedScene.Load("Assets/Scenes/Game.unity");
            string player = scene.GameObjectNamed("Player");
            Assert.That(player, Is.Not.Null, "no Player in the Game scene");

            Vector3 ship = Vector3.Scale(
                scene.Vector(scene.Component(player, "BoxCollider"), "m_Size"),
                scene.WorldScale(scene.Component(player, "Transform")));
            float climb = scene.Float(scene.ScriptWith(player, "climbSpeed"), "climbSpeed");
            scene.Band("Player", out float floor, out float ceiling);

            GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss.prefab");
            var attacks = (List<BossAttack>)typeof(BossEmitter)
                .GetField("attacks", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(boss.GetComponent<BossEmitter>());
            BossAttack crown = attacks.Find(attack => attack.HomeSeconds > 0f);
            Assert.That(crown, Is.Not.Null, "no attack on the boss fires torpedoes");

            // A torpedo is its round at HomeScale, hit box and all.
            Vector3 torpedo = Vector3.zero;
            crown.EachProjectile(round =>
            {
                Vector3 size = Vector3.Scale(round.GetComponent<BoxCollider>().size, round.transform.localScale)
                               * crown.HomeScale;
                torpedo = Vector3.Max(torpedo, size);
            });

            Assert.That(ceiling > floor && ship.x > 0f && ship.y > 0f && torpedo.x > 0f && torpedo.y > 0f,
                "read nonsense: band " + floor + " to " + ceiling + ", ship " + ship + ", torpedo " + torpedo);
            TestContext.WriteLine("Today: band " + floor + " to " + ceiling + ", ship " + ship.x + " long and " +
                                  ship.y + " tall at " + climb + ", torpedo " + torpedo.x + " by " + torpedo.y);

            // The round flies nose along the ring: its box's X is its length.
            return new Fight
            {
                Name = "today",
                Floor = floor,
                Ceiling = ceiling,
                PlayerSpeed = climb,
                HitHeight = (ship.y + torpedo.y) * 0.5f,
                HitLength = (ship.x + torpedo.x) * 0.5f,
                Crown = crown.Torpedo,
            };
        }

        private delegate Vector2 Mover(float time);

        /// <summary>
        /// Flies one torpedo from the muzzle along the ring at a player moving as
        /// <paramref name="player"/> says, and reports how close it came and when.
        /// </summary>
        private static float Closest(Mover player, float seconds, out float when, float frame = Frame)
        {
            return Fly(AsWritten, player, seconds, out when, out _, frame);
        }

        private static float Closest(Fight fight, Mover player, float seconds, out float when)
        {
            return Fly(fight, player, seconds, out when, out _, Frame);
        }

        /// <summary>Whether it hits within <paramref name="seconds"/>.</summary>
        private static bool Hits(Fight fight, Mover player, float seconds)
        {
            Fly(fight, player, seconds, out _, out bool hit, Frame);
            return hit;
        }

        private static float Fly(Fight fight, Mover player, float seconds, out float when, out bool hit, float frame)
        {
            Torpedo torpedo = Torpedo.Launch(Muzzle, 0f, player(0f));
            float closest = float.MaxValue;
            when = 0f;
            hit = false;

            for (float time = 0f; time < seconds; time += frame)
            {
                TorpedoSteer.Step(ref torpedo, player(time), fight.Crown, fight.Floor, fight.Ceiling, frame);

                Vector2 apart = player(time + frame) - torpedo.Position;
                hit |= Mathf.Abs(apart.y) < fight.HitHeight && Mathf.Abs(apart.x) < fight.HitLength;

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
                Mathf.Clamp(8f + direction * AsWritten.PlayerSpeed * Mathf.Max(0f, time - start), Floor, Ceiling));
        }

        /// <summary>
        /// A player 20 along the ring who moves <paramref name="units"/> up (or
        /// down, negative) at their full speed from <paramref name="start"/>, and
        /// then holds.
        /// </summary>
        private static Mover MovingBy(Fight fight, float units, float start)
        {
            return time => new Vector2(20f, Mathf.Clamp(
                8f + Mathf.Sign(units) * Mathf.Clamp(fight.PlayerSpeed * (time - start), 0f, Mathf.Abs(units)),
                fight.Floor, fight.Ceiling));
        }

        /// <summary>When the torpedo reaches a player who never moves from 20 along.</summary>
        private static float Arrival(Fight fight)
        {
            Closest(fight, StillAt(new Vector2(20f, 8f)), 5f, out float when);
            return when;
        }

        private static void HoldingStill_IsHit(Fight fight)
        {
            Assert.IsTrue(Hits(fight, StillAt(new Vector2(20f, 9.5f)), fight.Crown.Fuel), fight.Name);
        }

        [Test]
        public void HitsAPlayerWhoHoldsStill() => HoldingStill_IsHit(AsWritten);

        [Test]
        public void HitsAPlayerWhoHoldsStill_Today() => HoldingStill_IsHit(Current());

        /// <summary>Moving too soon gives it time to follow you in, up or down.</summary>
        private static void DodgingTooEarly_IsHit(Fight fight)
        {
            float arrival = Arrival(fight);
            Assert.IsTrue(Hits(fight, MovingBy(fight, 2f, arrival - 1.5f), arrival + 0.6f), fight.Name + ", up");
            Assert.IsTrue(Hits(fight, MovingBy(fight, -2f, arrival - 1.5f), arrival + 0.6f), fight.Name + ", down");
        }

        [Test]
        public void HitsAPlayerWhoDodgesTooEarly() => DodgingTooEarly_IsHit(AsWritten);

        [Test]
        public void HitsAPlayerWhoDodgesTooEarly_Today() => DodgingTooEarly_IsHit(Current());

        /// <summary>
        /// The miss: moving a couple of units in the last half second or so
        /// leaves it heading for where the player was, and it cannot turn hard
        /// enough to follow - up or down.
        /// </summary>
        private static void DodgingAsItCloses_Misses(Fight fight)
        {
            float arrival = Arrival(fight);

            foreach (float units in new[] { 2f, -2f })
            {
                foreach (float lead in new[] { 0.7f, 0.5f, 0.3f })
                {
                    Assert.IsFalse(Hits(fight, MovingBy(fight, units, arrival - lead), arrival + 0.6f),
                        fight.Name + ": moving " + units + " from " + lead + "s before it arrives");
                }
            }
        }

        [Test]
        public void MissesAPlayerWhoDodgesAsItCloses() => DodgingAsItCloses_Misses(AsWritten);

        [Test]
        public void MissesAPlayerWhoDodgesAsItCloses_Today() => DodgingAsItCloses_Misses(Current());

        /// <summary>
        /// Diving all the way to the floor is a timed escape, like the short
        /// dodge. It used to be none: at a cruise of 7.5 against a player at 7,
        /// one that dived half a second out was followed down and run into along
        /// the floor. Since 22 September (cruise 8.5, player 5.6) a dive started
        /// half a second to a second before it arrives gets away. Too early and it
        /// follows you down; too late and you are still in its path.
        /// </summary>
        private static void DivingToTheFloor(Fight fight, float[] escapes, float[] caught)
        {
            float arrival = Arrival(fight);

            foreach (float lead in escapes)
            {
                Assert.IsFalse(Hits(fight, MovingBy(fight, -10f, arrival - lead), fight.Crown.Fuel),
                    fight.Name + ": diving " + lead + "s before it arrives");
            }

            foreach (float lead in caught)
            {
                Assert.IsTrue(Hits(fight, MovingBy(fight, -10f, arrival - lead), fight.Crown.Fuel),
                    fight.Name + ": diving " + lead + "s before it arrives");
            }
        }

        [Test]
        public void DivingToTheFloor_EscapesOnlyIfTimed() =>
            DivingToTheFloor(AsWritten, new[] { 1f, 0.7f, 0.5f }, new[] { 1.5f, 0.3f });

        /// <summary>
        /// Still a timed escape, but a later one. The band deepened on 25
        /// September 2026 and the floor went 1.6 further down, which gives a
        /// torpedo room to follow a diver down where it used to have to pull
        /// out: a dive now has to start 0.8 to 1.15s before it arrives, against
        /// 0.4 to 1.15s before (worked through in 0.05s steps on 26 September).
        /// The bigger ship made no difference to it, and the short dodge above
        /// barely moved. Nobody chose the change; whether to win the early half
        /// of the window back is a tuning question.
        ///
        /// On 5 October 2026 the band came in to 9.23, for a camera 9 out, and
        /// the floor rose 0.63. The late edge did not move and the early one
        /// did: a dive escapes from 0.8 to 1.5s before it arrives now (0.05s
        /// steps again), so it is the widest the window has been. A climb to
        /// the ceiling escapes from 0.85 to 1.35s.
        /// </summary>
        [Test]
        public void DivingToTheFloor_EscapesOnlyIfTimed_Today() =>
            DivingToTheFloor(Current(), new[] { 1.4f, 1.2f, 1f, 0.9f }, new[] { 2f, 0.5f, 0.3f });

        /// <summary>
        /// A player behind the muzzle at launch: it runs out, turns round along
        /// the ceiling and comes back.
        /// </summary>
        [Test]
        public void TurnsRoundForAPlayerBehindIt()
        {
            Assert.IsTrue(Hits(AsWritten, StillAt(new Vector2(-15f, 8f)), Crown.Fuel));
        }

        /// <summary>
        /// After a successful dodge it does not fly off: it comes about and
        /// heads back along the ring at the player, while it has fuel.
        /// </summary>
        [Test]
        public void ComesBackAfterAMiss()
        {
            float arrival = Arrival(AsWritten);
            float start = arrival - 0.5f;

            Mover player = time => new Vector2(20f,
                Mathf.Min(Ceiling, 8f + AsWritten.PlayerSpeed * Mathf.Clamp(time - start, 0f, 0.3f)));

            Assert.IsFalse(Hits(AsWritten, player, arrival + 0.6f), "the first pass should miss");

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
        /// Far slower than the round it replaced, about half again the player's
        /// speed since the player slowed to 5.6 on 22 September, and since 28
        /// September it catches a player who runs flat out along the ring from
        /// 20 away. Until then it could not: its ghost trailed a runner by their
        /// speed over its perception, so it closed to about 2.7, passed the
        /// ghost and came about with the player still ahead. Running buys time
        /// now; it does not end the chase.
        /// </summary>
        private static void ARunner_IsCaught(Fight fight)
        {
            Assert.Less(fight.Crown.CruiseSpeed, OldRoundSpeed, fight.Name);
            Assert.Greater(fight.Crown.CruiseSpeed, fight.PlayerSpeed * 1.4f, fight.Name);
            Assert.Less(fight.Crown.CruiseSpeed, fight.PlayerSpeed * 1.6f, fight.Name);

            Mover fleeing = time => new Vector2(20f + fight.PlayerSpeed * time, 8f);
            Assert.IsTrue(Hits(fight, fleeing, fight.Crown.Fuel),
                fight.Name + ": a runner should be caught before the fuel runs out");
        }

        [Test]
        public void CatchesAPlayerWhoRuns() => ARunner_IsCaught(AsWritten);

        [Test]
        public void CatchesAPlayerWhoRuns_Today() => ARunner_IsCaught(Current());

        /// <summary>
        /// Eight Move Speed picks, the most a run can take, make the ship 1.8
        /// times as fast. The boss fires its torpedoes at that pace
        /// (<see cref="TorpedoHandling.Scaled"/>), and the chase is the same
        /// chase: the runner is caught. At the torpedo's own pace they get away,
        /// which is what a player did on 28 September 2026.
        /// </summary>
        [Test]
        public void CatchesARunnerWithEveryMoveSpeedPick_OnlyAtTheirPace()
        {
            const float pace = 1.8f;
            Fight fast = new Fight
            {
                Name = "every pick",
                Floor = Floor,
                Ceiling = Ceiling,
                PlayerSpeed = AsWritten.PlayerSpeed * pace,
                HitHeight = AsWritten.HitHeight,
                HitLength = AsWritten.HitLength,
                Crown = Crown.Scaled(pace),
            };

            Mover fleeing = time => new Vector2(20f + fast.PlayerSpeed * time, 8f);
            Assert.IsTrue(Hits(fast, fleeing, fast.Crown.Fuel), "at the ship's pace it should catch them");

            fast.Crown = Crown;
            Assert.IsFalse(Hits(fast, fleeing, fast.Crown.Fuel), "at its own pace the faster ship should get away");
        }

        [Test]
        public void Scaled_SpeedsUpTheChase_AndLeavesItsTimingAlone()
        {
            TorpedoHandling scaled = Crown.Scaled(1.5f);

            Assert.AreEqual(Crown.LaunchSpeed * 1.5f, scaled.LaunchSpeed, 1e-4f);
            Assert.AreEqual(Crown.CruiseSpeed * 1.5f, scaled.CruiseSpeed, 1e-4f);
            Assert.AreEqual(Crown.TurnRate * 1.5f, scaled.TurnRate, 1e-4f);
            Assert.AreEqual(Crown.Perception, scaled.Perception);
            Assert.AreEqual(Crown.SpinUp, scaled.SpinUp);
            Assert.AreEqual(Crown.ArmSeconds, scaled.ArmSeconds);
            Assert.AreEqual(Crown.Fuel, scaled.Fuel);
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
