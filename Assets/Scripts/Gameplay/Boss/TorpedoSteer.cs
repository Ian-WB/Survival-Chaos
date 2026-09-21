using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// How a boss torpedo handles: what it can do in one step, with nothing
    /// about the ring or the boss in it.
    /// </summary>
    public struct TorpedoHandling
    {
        /// <summary>Units a second as it leaves the muzzle.</summary>
        public float LaunchSpeed;

        /// <summary>Units a second once its motor is up to speed.</summary>
        public float CruiseSpeed;

        /// <summary>Seconds from launch to cruise.</summary>
        public float SpinUp;

        /// <summary>Seconds it runs straight out of the muzzle before it steers.</summary>
        public float ArmSeconds;

        /// <summary>The fastest it turns, in degrees a second.</summary>
        public float TurnRate;

        /// <summary>How quickly its idea of where the player is catches up, per second.</summary>
        public float Perception;

        /// <summary>Seconds from launch it steers for. After that it flies straight.</summary>
        public float Fuel;
    }

    /// <summary>
    /// One torpedo in flight, in the ring laid out flat: x is distance round
    /// the ring, positive the way a positive orbit speed carries a round, and y
    /// is height.
    /// </summary>
    public struct Torpedo
    {
        public Vector2 Position;

        /// <summary>Degrees from +x toward +y: 0 is along the ring, 90 straight up.</summary>
        public float Heading;

        /// <summary>Where it believes the player is.</summary>
        public Vector2 Ghost;

        /// <summary>Seconds since launch.</summary>
        public float Age;

        /// <summary>
        /// Which way round it is turning to come about, once it has chosen: +1
        /// anticlockwise in the flat ring, -1 clockwise, 0 while the player is
        /// in front of it and there is nothing to come about for.
        /// </summary>
        public int ComingAbout;

        /// <summary>
        /// A torpedo leaving the muzzle along <paramref name="heading"/>, having
        /// seen the player at <paramref name="target"/> as it fired.
        /// </summary>
        public static Torpedo Launch(Vector2 position, float heading, Vector2 target)
        {
            return new Torpedo { Position = position, Heading = heading, Ghost = target, Age = 0f };
        }
    }

    /// <summary>
    /// How a boss torpedo steers after the player, with no Unity object
    /// attached so the chase can be tested directly.
    ///
    /// It flies like a game torpedo rather than a bullet that slides toward
    /// your height. It has a heading and a speed. It leaves the muzzle slowly
    /// and straight, spins up to cruise, and turns toward the player no faster
    /// than a fixed turn rate, so it swings through wide curves rather than
    /// correcting on the spot. At 7.5 units a second and 90 degrees a second
    /// its turning circle is about 10 units across, more than the band's height,
    /// so one that has to turn round near an edge pulls out tight along it.
    /// One that misses swings round and comes back at you while its fuel
    /// lasts - though with the player inside its turning circle it tends to
    /// circle past rather than connect, which is what homing missiles do.
    ///
    /// Delayed on purpose, because a torpedo that tracks perfectly cannot be
    /// dodged, only outrun. It keeps its own idea of where the player is - the
    /// ghost - which catches up with the real position over a fraction of a
    /// second, and it steers at the ghost. A player who holds still gets hit,
    /// and so does one who makes an ordinary dodge a second and a half or more
    /// early, because it has time to follow. A player who moves a unit or more
    /// in the last second or so leaves it heading for where they were, and too
    /// committed to the turn to follow. The hit boxes make it an intercept: the
    /// player's is 0.16 tall and a half-size torpedo's 0.09, so one an eighth
    /// of a unit high passes over.
    ///
    /// Everything else in the band bounces off the floor and ceiling, which
    /// the player's centre is clamped to. A torpedo does not bounce, because
    /// the snap would read as a glitch, and it does not scrape along an edge
    /// nose-first either, which is what it did in play on 21 September 2026 -
    /// a twentieth of all torpedo frames had the nose over 20 degrees off the
    /// way it was going. It pulls out before the edge, at a tighter turn than
    /// it steers with, so it arrives running along it.
    /// </summary>
    public static class TorpedoSteer
    {
        /// <summary>
        /// How much tighter than it steers a torpedo pulls out before an edge.
        /// Tight enough to leave it most of the band to chase in - at 7.5 units
        /// a second it pulls out of a vertical climb in 1.6 - and loose enough
        /// that the pull-out still reads as a turn.
        /// </summary>
        public const float EdgeTurnScale = 3f;

        /// <summary>
        /// Past about 20 degrees of climb or dive a torpedo is committed to the
        /// way round it is already turning.
        /// </summary>
        private const float SteepSine = 0.34f;

        /// <summary>
        /// Advances one torpedo by <paramref name="delta"/> seconds, chasing
        /// <paramref name="target"/> in the same flat coordinates.
        /// </summary>
        public static void Step(ref Torpedo torpedo, Vector2 target, in TorpedoHandling handling,
                                float floor, float ceiling, float delta)
        {
            if (delta <= 0f)
            {
                return;
            }

            float age = torpedo.Age;
            bool band = ceiling > floor;

            // Exponential rather than linear, so the catch-up is the same shape
            // at any frame rate.
            float catchUp = 1f - Mathf.Exp(-Mathf.Max(0f, handling.Perception) * delta);
            torpedo.Ghost = Vector2.Lerp(torpedo.Ghost, target, catchUp);

            float speed = Speed(handling, age);
            float edgeRate = EdgeTurnScale * Mathf.Max(0f, handling.TurnRate);
            bool pulledOut = band && PullOut(ref torpedo, speed, edgeRate, floor, ceiling, delta);

            if (!pulledOut && Steering(handling, age))
            {
                Vector2 toGhost = torpedo.Ghost - torpedo.Position;

                if (toGhost.sqrMagnitude > 1e-6f)
                {
                    float desired = Mathf.Atan2(toGhost.y, toGhost.x) * Mathf.Rad2Deg;
                    float above = band ? ceiling - torpedo.Position.y : 1f;
                    float below = band ? torpedo.Position.y - floor : 1f;

                    torpedo.Heading = Turn(torpedo.Heading, desired,
                        Mathf.Max(0f, handling.TurnRate) * delta, ref torpedo.ComingAbout, above, below);
                }
            }

            float radians = torpedo.Heading * Mathf.Deg2Rad;
            Vector2 next = torpedo.Position + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (speed * delta);

            // The pull-out is judged a frame ahead, so a long frame can still
            // carry it over. It stops at the edge rather than passing through it.
            if (band)
            {
                next.y = Mathf.Clamp(next.y, floor, ceiling);
            }

            torpedo.Position = next;
            torpedo.Age = age + delta;
        }

        /// <summary>
        /// Levels a torpedo off before it reaches the edge it is heading for,
        /// turning at <paramref name="edgeRate"/>, and reports whether it did -
        /// in which case it does not steer this frame. Levels off onto the
        /// horizontal it is turning toward: coming about over the top, that is
        /// the far one, so the pull-out finishes the turn instead of undoing it.
        ///
        /// Levelling off from a heading on a circle of radius r climbs
        /// r x |cos heading - cos level| more, so that is the room it needs.
        /// </summary>
        private static bool PullOut(ref Torpedo torpedo, float speed, float edgeRate,
                                    float floor, float ceiling, float delta)
        {
            float radians = torpedo.Heading * Mathf.Deg2Rad;
            float climb = Mathf.Sin(radians);

            if (edgeRate <= 0f || Mathf.Abs(climb) < 1e-4f)
            {
                return false;
            }

            float radius = speed / (edgeRate * Mathf.Deg2Rad);
            float room = climb > 0f ? ceiling - torpedo.Position.y : torpedo.Position.y - floor;
            float level = Level(torpedo.Heading, torpedo.ComingAbout);
            float needed = radius * Mathf.Abs(Mathf.Cos(radians) - Mathf.Cos(level * Mathf.Deg2Rad));

            // Committed to coming about through an edge it no longer has the
            // room to get round - diving at the floor, say, with the player
            // now behind. Level off the near way instead and come about the
            // other way round. Left to the committed turn it hit the floor at
            // 49 degrees on a long chase.
            if (torpedo.ComingAbout != 0 && room < needed)
            {
                level = Level(torpedo.Heading, 0);
                needed = radius * Mathf.Abs(Mathf.Cos(radians) - Mathf.Cos(level * Mathf.Deg2Rad));
                torpedo.ComingAbout = Mathf.DeltaAngle(torpedo.Heading, level) >= 0f ? 1 : -1;
            }

            if (room > needed + Mathf.Abs(climb) * speed * delta)
            {
                return false;
            }

            float limit = edgeRate * delta;
            torpedo.Heading = Normalise(torpedo.Heading +
                Mathf.Clamp(Mathf.DeltaAngle(torpedo.Heading, level), -limit, limit));

            return true;
        }

        /// <summary>
        /// The horizontal a heading is turning toward: the next one round in
        /// the direction it is coming about, or the nearest when it is not.
        /// </summary>
        private static float Level(float heading, int comingAbout)
        {
            float halfTurns = heading / 180f;

            if (comingAbout > 0)
            {
                return Mathf.Ceil(halfTurns) * 180f;
            }

            if (comingAbout < 0)
            {
                return Mathf.Floor(halfTurns) * 180f;
            }

            return Mathf.Round(halfTurns) * 180f;
        }

        /// <summary>
        /// How hard the motor burns out of the muzzle, as a share of full: enough
        /// to show the torpedo is under power from the first frame.
        /// </summary>
        public const float LaunchBurn = 0.35f;

        /// <summary>
        /// How hard the motor is burning at <paramref name="age"/>, 0 to 1, for
        /// <see cref="TorpedoExhaust"/>: <see cref="LaunchBurn"/> out of the
        /// muzzle, rising with the speed to full at cruise, and out the moment
        /// the fuel is - the coast is unpowered, and looks it.
        /// </summary>
        public static float Thrust(in TorpedoHandling handling, float age)
        {
            if (age >= handling.Fuel)
            {
                return 0f;
            }

            if (handling.SpinUp <= 0f || age >= handling.SpinUp)
            {
                return 1f;
            }

            return Mathf.Lerp(LaunchBurn, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, age) / handling.SpinUp));
        }

        /// <summary>Whether a torpedo this old steers: armed, and not out of fuel.</summary>
        public static bool Steering(in TorpedoHandling handling, float age)
        {
            return age >= handling.ArmSeconds && age < handling.Fuel;
        }

        /// <summary>
        /// Units a second at <paramref name="age"/>: easing from the launch
        /// speed up to cruise over the spin-up, and cruise from then on.
        /// </summary>
        public static float Speed(in TorpedoHandling handling, float age)
        {
            float cruise = Mathf.Max(0f, handling.CruiseSpeed);

            if (handling.SpinUp <= 0f || age >= handling.SpinUp)
            {
                return cruise;
            }

            return Mathf.Lerp(Mathf.Max(0f, handling.LaunchSpeed), cruise,
                Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, age) / handling.SpinUp));
        }

        /// <summary>
        /// A heading turned toward <paramref name="desired"/> by no more than
        /// <paramref name="maxTurn"/> degrees.
        ///
        /// The short way round while the player is in front. With the player
        /// behind it has to come about, through straight up or straight down,
        /// and the band is too shallow for that to be left to chance. Climbing
        /// or diving already, it carries on round the way it is going; level,
        /// it goes toward whichever edge has more room. Either way it keeps
        /// going round that way until the player is in front again. Without the commitment it dithers -
        /// each few degrees of climb takes room from above, which makes down
        /// look better, which takes room from below - and never comes round.
        /// </summary>
        /// <param name="comingAbout">Which way round it has committed to, kept between frames.</param>
        /// <param name="roomAbove">Height left to the ceiling.</param>
        /// <param name="roomBelow">Height left to the floor.</param>
        public static float Turn(float heading, float desired, float maxTurn, ref int comingAbout,
                                 float roomAbove, float roomBelow)
        {
            float wanted = Mathf.DeltaAngle(heading, desired);

            if (Mathf.Abs(wanted) <= 90f)
            {
                comingAbout = 0;
            }
            else
            {
                if (comingAbout == 0)
                {
                    if (Mathf.Abs(Mathf.Sin(heading * Mathf.Deg2Rad)) > SteepSine)
                    {
                        // Already climbing or diving hard: carry on round the
                        // way it is going, over the top or under, however little
                        // room there is. The pull-out makes the turn tight at the
                        // edge. Reversing a steep climb to go round the other way
                        // is the long way round, and in play it ran a torpedo out
                        // of fuel before it came back.
                        comingAbout = wanted >= 0f ? 1 : -1;
                    }
                    else
                    {
                        // Near level, either way is as short: go toward the room.
                        // Anticlockwise raises the nose when it points along +x
                        // and lowers it when it points back along -x.
                        float along = Mathf.Cos(heading * Mathf.Deg2Rad) >= 0f ? 1f : -1f;
                        comingAbout = roomAbove >= roomBelow ? (int)along : -(int)along;
                    }
                }

                if (Mathf.Sign(wanted) != comingAbout)
                {
                    wanted -= 360f * Mathf.Sign(wanted);
                }
            }

            return Normalise(heading + Mathf.Clamp(wanted, -maxTurn, maxTurn));
        }

        /// <summary>
        /// Degrees to roll a round about its own forward, once it has been
        /// turned to face the arena axis, so its nose points along
        /// <paramref name="heading"/>.
        ///
        /// Facing the axis leaves the ring running along the round's local X,
        /// with +x of the flat ring on its local -X, and a round's nose where
        /// it was modelled: on -X for one fired with a positive orbit speed and
        /// on +X for one fired with a negative one, which is the way each was
        /// always drawn flying.
        /// </summary>
        public static float Roll(float heading, bool noseOnNegativeX)
        {
            return Normalise(noseOnNegativeX ? -heading : 180f - heading);
        }

        /// <summary>Into -180 to 180, so headings compare and print sensibly.</summary>
        private static float Normalise(float degrees)
        {
            return Mathf.Repeat(degrees + 180f, 360f) - 180f;
        }
    }
}
