using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Whether a spawn stream puts enemies where the player can get at them.
    ///
    /// The player's vertical travel is clamped by <see cref="ApplyBounds"/> to a
    /// box authored in the scene. The heights enemies arrive at are authored in a
    /// wave asset. Until 25 September 2026 nothing connected the two, and moving
    /// the box did not move the streams - so raising the floor by 3.3 units once
    /// left four streams spawning entirely beneath it, and nothing said so, and
    /// lowering it 1.7 on 21 September left every stream but the boss's 1.7
    /// higher in the band than it was placed. Now the wave records the band its
    /// heights were placed against and <see cref="Carry"/> takes each spawn into
    /// the live one.
    ///
    /// What the checks here still catch is a stream placed outside that
    /// recorded band. Whether that strands an enemy or merely inconveniences it
    /// depends on what the prefab carries. EnemyMovement climbs towards the
    /// player once inside its chase radius, so it recovers on its own within a
    /// few seconds. ObstacleScript has no chase branch and never changes height
    /// at all, so it holds its spawn height for its whole life - out of reach,
    /// unkillable, and still drawn.
    ///
    /// Kept free of scene and asset types so the arithmetic can be tested
    /// directly, in the same way as <see cref="SpawnMath"/>.
    /// </summary>
    public static class SpawnBand
    {
        /// <summary>
        /// The span of heights a stream spawns into: its authored height, plus
        /// the low and high ends of its random vertical offset.
        /// </summary>
        /// <remarks>
        /// The offset is read with Min and Max rather than as (x = low, y = high)
        /// because nothing enforces that order. Random.Range hands back a value
        /// between its arguments whichever way round they are, so a stream
        /// authored backwards spawns over the same span and would otherwise be
        /// measured as an empty one.
        /// </remarks>
        public static void RangeOf(float height, Vector2 offsetRange,
            out float lowest, out float highest)
        {
            lowest = height + Mathf.Min(offsetRange.x, offsetRange.y);
            highest = height + Mathf.Max(offsetRange.x, offsetRange.y);
        }

        /// <summary>
        /// How far the lowest spawn falls below the floor, or zero when it does
        /// not fall below it at all.
        /// </summary>
        public static float BelowFloorBy(float lowest, float floor)
        {
            return Mathf.Max(0f, floor - lowest);
        }

        /// <summary>
        /// How far the highest spawn rises above the ceiling, or zero when it
        /// does not rise above it at all.
        /// </summary>
        public static float AboveCeilingBy(float highest, float ceiling)
        {
            return Mathf.Max(0f, highest - ceiling);
        }

        /// <summary>
        /// True when no part of the range is inside the band.
        ///
        /// Worth separating from "pokes out one end", because the two are
        /// different problems. A stream that straddles the floor loses some of
        /// its enemies; a stream wholly beneath it loses all of them, every time,
        /// for the whole run.
        /// </summary>
        public static bool IsWhollyOutside(float lowest, float highest,
            float floor, float ceiling)
        {
            return highest < floor || lowest > ceiling;
        }

        /// <summary>
        /// True when the whole range sits inside the band, which is the only
        /// state that needs no report.
        /// </summary>
        public static bool IsFullyInside(float lowest, float highest,
            float floor, float ceiling)
        {
            return lowest >= floor && highest <= ceiling;
        }

        /// <summary>
        /// Halfway between the floor and the ceiling. What the boss measures its
        /// height from, so that moving the band moves the boss with it.
        /// </summary>
        public static float Middle(float floor, float ceiling)
        {
            return (floor + ceiling) * 0.5f;
        }

        /// <summary>
        /// Carries a height from one band to another, keeping its place in it: a
        /// height a third of the way up the first band lands a third of the way
        /// up the second. What the waves use, so that moving PlayerBounds moves
        /// every stream with it and resizing it spreads or packs them to fit.
        /// </summary>
        /// <remarks>
        /// Proportional rather than a plain shift, which is what the boss uses,
        /// because a stream is a place in the band and the boss is a fixed hull.
        /// Shifted, a band made shorter would push its top and bottom streams
        /// out of it, and one made taller would leave its edges empty to hide
        /// in. Anything inside the first band lands inside the second.
        ///
        /// A first band with no height has no places to keep, so the height
        /// moves with its middle instead.
        /// </remarks>
        public static float Carry(float height, float fromFloor, float fromCeiling,
            float toFloor, float toCeiling)
        {
            float from = fromCeiling - fromFloor;

            if (from <= 0.0001f)
            {
                return height + (Middle(toFloor, toCeiling) - Middle(fromFloor, fromCeiling));
            }

            return toFloor + ((height - fromFloor) * ((toCeiling - toFloor) / from));
        }
    }
}
