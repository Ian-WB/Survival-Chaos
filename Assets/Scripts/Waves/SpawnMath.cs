using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The spawn cadence rules, with no Unity object attached so they can be
    /// tested directly.
    ///
    /// The original Spawner ran a second coroutine that repeatedly multiplied
    /// its own delay field, meaning the schedule could only be understood by
    /// simulating it. The closed form here produces the same curve from elapsed
    /// time alone.
    /// </summary>
    public static class SpawnMath
    {
        /// <summary>
        /// The gap between spawns at a given point in the run.
        /// </summary>
        public static float IntervalAt(
            float baseInterval,
            float intervalScale,
            float rampEvery,
            float minInterval,
            float elapsedSeconds)
        {
            float interval = baseInterval;

            bool ramps = rampEvery > 0f
                         && intervalScale > 0f
                         && !Mathf.Approximately(intervalScale, 1f)
                         && elapsedSeconds > 0f;

            if (ramps)
            {
                int steps = Mathf.FloorToInt(elapsedSeconds / rampEvery);
                if (steps > 0)
                {
                    interval = baseInterval * Mathf.Pow(intervalScale, steps);
                }
            }

            return Mathf.Max(interval, Mathf.Max(minInterval, 0f));
        }
    }
}
