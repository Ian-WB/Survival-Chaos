using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Where pickups go on the orbit ring.
    ///
    /// Everything in this arena sits on one cylinder: a fixed distance from the
    /// axis, free to move around it and up and down. So a position out here is
    /// really two numbers - a bearing around the axis and a height - and the
    /// maths is kept in those terms rather than in world vectors.
    ///
    /// That is deliberate. Straight-line distance between two points on a ring
    /// is not the distance travelled to reach one from the other, and at
    /// opposite sides of the ring the two differ by the whole diameter. Working
    /// in bearings means a separation of ninety degrees is a quarter turn of
    /// travel, whatever the radius happens to be.
    /// </summary>
    public static class PickupPlacement
    {
        /// <summary>
        /// The bearing of a point around the arena axis, in degrees, measured
        /// from +Z the way <see cref="PointAt"/> reads it back.
        /// </summary>
        public static float BearingOf(Vector3 position, Vector3 center)
        {
            Vector3 offset = position - center;
            offset.y = 0f;

            if (offset.sqrMagnitude < 0.000001f)
            {
                // On the axis there is no bearing to measure. -Z matches where
                // ArenaGeometry puts anything that lands in the same spot.
                return 180f;
            }

            return Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The world point at a bearing, on the orbit circle, at a given height.
        /// The exact inverse of <see cref="BearingOf"/>.
        /// </summary>
        public static Vector3 PointAt(float bearingDegrees, Vector3 center, float radius, float height)
        {
            float radians = bearingDegrees * Mathf.Deg2Rad;

            Vector3 point = center + new Vector3(
                Mathf.Sin(radians) * radius,
                0f,
                Mathf.Cos(radians) * radius);

            point.y = height;
            return point;
        }

        /// <summary>
        /// Bearings for a set of pickups offered at once, spread evenly around
        /// the ring and set clear of where the player is standing.
        ///
        /// Even spreading is what makes the offer a decision. Three pickups a
        /// hundred and twenty degrees apart cannot all be collected - committing
        /// to one is giving up the others, and that is the whole point of
        /// handing upgrades out this way rather than granting them outright.
        ///
        /// The clearance matters for the same reason. A pickup on top of the
        /// player is not a choice, it is a delayed automatic grant.
        /// </summary>
        /// <param name="playerBearing">Where the player is now, in degrees.</param>
        /// <param name="count">How many to place. Below one returns empty.</param>
        /// <param name="minSeparationDegrees">
        /// How far the nearest one sits from the player. Clamped so it can never
        /// exceed half the gap between neighbours, which would otherwise push
        /// the far end of the set back around onto the player.
        /// </param>
        /// <param name="clockwise">Which way the set is offset. Varies the offer.</param>
        public static float[] Bearings(
            float playerBearing,
            int count,
            float minSeparationDegrees,
            bool clockwise)
        {
            if (count < 1)
            {
                return new float[0];
            }

            float step = 360f / count;

            // Half a step is the most that can be given and still leave the last
            // pickup of the set the same distance away on the other side. Ask for
            // more than that and the set rotates far enough that its tail end
            // comes back round to the player - the opposite of what was wanted.
            float separation = Mathf.Clamp(minSeparationDegrees, 0f, step * 0.5f);
            float direction = clockwise ? 1f : -1f;

            var bearings = new float[count];

            for (int i = 0; i < count; i++)
            {
                bearings[i] = Normalize(playerBearing + direction * (separation + step * i));
            }

            return bearings;
        }

        /// <summary>
        /// <see cref="Bearings"/>, turned clear of pickups already on the ring.
        ///
        /// A level-up does not wait for the last one to be answered, and offers
        /// stay out for 28 seconds, so a second one often lands while the first
        /// is still there. Placed from the player's bearing alone, with the usual
        /// 55 degree clearance, three pickups sit on the only three places that
        /// clearance allows: a second offer before the player had moved landed
        /// exactly on the first, or ten degrees off it when it went the other
        /// way. Labels piled up, and flying through took one from each offer
        /// (scan of 28 September 2026).
        ///
        /// The set keeps its even spacing, which is what makes it a choice, and
        /// turns as a whole. Where the usual placement already leaves every live
        /// pickup and the player the usual clearance, it stands. Otherwise the
        /// turn is the one that leaves the most room to whichever is nearest,
        /// counting the player as one more thing to keep clear of, and among
        /// equally good turns the one nearest the usual placement.
        /// </summary>
        /// <param name="live">Bearings of the pickups already out. May be null or empty.</param>
        public static float[] BearingsClearOf(
            float playerBearing,
            int count,
            float minSeparationDegrees,
            bool clockwise,
            IReadOnlyList<float> live)
        {
            float[] usual = Bearings(playerBearing, count, minSeparationDegrees, clockwise);

            if (usual.Length == 0 || live == null || live.Count == 0)
            {
                return usual;
            }

            // The most room worth asking for: what the player alone is given.
            float wanted = Mathf.Clamp(minSeparationDegrees, 0f, 180f / count);
            float step = 360f / count;

            float bestTurn = 0f;
            float bestRoom = RoomAfter(usual, 0f, playerBearing, live, wanted);

            // Every half degree over one gap, nearest the usual placement first,
            // so a later turn has to be strictly better to win.
            for (int i = 1; i <= step; i++)
            {
                if (bestRoom >= wanted)
                {
                    break;
                }

                foreach (float turn in new[] { i * 0.5f, -i * 0.5f })
                {
                    float room = RoomAfter(usual, turn, playerBearing, live, wanted);
                    if (room > bestRoom)
                    {
                        bestRoom = room;
                        bestTurn = turn;
                    }
                }
            }

            var turned = new float[usual.Length];
            for (int i = 0; i < usual.Length; i++)
            {
                turned[i] = Normalize(usual[i] + bestTurn);
            }

            return turned;
        }

        /// <summary>
        /// How far the nearest of the player and the live pickups would sit from
        /// the set turned by <paramref name="turn"/>, capped at the room wanted.
        /// </summary>
        private static float RoomAfter(
            float[] usual,
            float turn,
            float playerBearing,
            IReadOnlyList<float> live,
            float wanted)
        {
            float room = wanted;

            foreach (float bearing in usual)
            {
                float placed = bearing + turn;
                room = Mathf.Min(room, Separation(placed, playerBearing));

                for (int i = 0; i < live.Count; i++)
                {
                    room = Mathf.Min(room, Separation(placed, live[i]));
                }
            }

            return room;
        }

        /// <summary>
        /// The shorter way round between two bearings, in degrees, always
        /// positive. This is the one to compare against a reach or a threshold -
        /// the difference of the raw numbers counts the long way round whenever
        /// the pair straddles the wrap point.
        /// </summary>
        public static float Separation(float fromDegrees, float toDegrees)
        {
            return Mathf.Abs(Mathf.DeltaAngle(fromDegrees, toDegrees));
        }

        /// <summary>
        /// Folds a bearing into [-180, 180], the range Unity's angle helpers use.
        ///
        /// Both ends are inclusive and they are the same direction, so a bearing
        /// of straight-behind may come back as either. Compare bearings with
        /// <see cref="Separation"/> rather than by subtracting them - the two
        /// endpoints are 360 apart as numbers and zero apart as directions.
        /// </summary>
        private static float Normalize(float degrees)
        {
            return Mathf.DeltaAngle(0f, degrees);
        }
    }
}
