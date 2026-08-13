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
