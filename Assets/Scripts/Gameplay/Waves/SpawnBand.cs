using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Whether a spawn stream puts enemies where the player can get at them.
    ///
    /// The player's vertical travel is clamped by <see cref="ApplyBounds"/> to a
    /// box authored in the scene. The heights enemies arrive at are authored in a
    /// wave asset. Nothing connects the two, and moving the box does not move the
    /// streams - so raising the floor by 3.3 units once left four streams
    /// spawning entirely beneath it, and nothing said so.
    ///
    /// Whether that strands an enemy or merely inconveniences it depends on what
    /// the prefab carries, which is why this reports rather than corrects.
    /// EnemyMovement climbs towards the player once inside its chase radius, so
    /// it recovers on its own within a few seconds. ObstacleScript has no chase
    /// branch and never changes height at all, so it holds its spawn height for
    /// its whole life - out of reach, unkillable, and still drawn.
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
    }
}
