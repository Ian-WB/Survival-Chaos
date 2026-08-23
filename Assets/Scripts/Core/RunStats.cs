using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalChaos
{
    /// <summary>
    /// What happened during this run, for the screen that reports it afterwards.
    ///
    /// Static and scene-scoped, alongside RunOutcome, because the things that
    /// know these numbers cannot hold a reference to the thing that shows them:
    /// enemies arrive from a pool mid-run, and the screen that reads their totals
    /// is on a panel that has been inactive since the scene loaded.
    ///
    /// Counted as it happens rather than derived at the end. Experience earned is
    /// not recoverable from the player's level and bar - levels cost a different
    /// amount each time and the bar is reset on every one of them - and enemies
    /// destroyed is not recoverable from anything at all once they are back in
    /// the pool.
    /// </summary>
    public static class RunStats
    {
        public static int EnemiesDestroyed { get; private set; }

        public static int ExperienceEarned { get; private set; }

        /// <summary>The highest level reached. Starts at 1, which is where Player starts.</summary>
        public static int LevelReached { get; private set; } = 1;

        private static readonly List<string> skillOrder = new List<string>();
        private static readonly Dictionary<string, int> skillCounts = new Dictionary<string, int>();

        private static float startedAt;
        private static float endedAt = -1f;

        /// <summary>
        /// How long the run lasted, in seconds, frozen once it ends.
        ///
        /// Scaled time, so the clock stops while the game is paused. Surviving is
        /// something done with the game running; a player who spends four minutes
        /// on the pause screen has not survived four minutes.
        /// </summary>
        public static float Seconds =>
            Mathf.Max(0f, (endedAt >= 0f ? endedAt : Time.time) - startedAt);

        /// <summary>Skills picked, in the order they were first taken.</summary>
        public static IReadOnlyList<string> SkillOrder => skillOrder;

        public static int PicksOf(string skill)
        {
            return skill != null && skillCounts.TryGetValue(skill, out int taken) ? taken : 0;
        }

        public static void RecordKill(int reward)
        {
            EnemiesDestroyed++;
            ExperienceEarned += Mathf.Max(0, reward);
        }

        /// <summary>
        /// Highest rather than latest, so this cannot be walked backwards by
        /// anything that reports a level out of order.
        /// </summary>
        public static void RecordLevel(int level)
        {
            if (level > LevelReached)
            {
                LevelReached = level;
            }
        }

        public static void RecordSkill(string skill)
        {
            if (string.IsNullOrEmpty(skill))
            {
                return;
            }

            if (skillCounts.TryGetValue(skill, out int taken))
            {
                skillCounts[skill] = taken + 1;
                return;
            }

            skillCounts[skill] = 1;
            skillOrder.Add(skill);
        }

        /// <summary>
        /// Stops the clock. Called from RunOutcome as the run ends, which is the
        /// one place both endings already pass through - and it happens there
        /// before time is stopped, so the reading is the run rather than zero.
        ///
        /// Latched, because the death screen can be reached with a victory
        /// already resolving in the same frame.
        /// </summary>
        public static void Stop()
        {
            if (endedAt < 0f)
            {
                endedAt = Time.time;
            }
        }

        /// <summary>
        /// Statics outlive a scene, and outlive play mode entirely when domain
        /// reload is disabled, so a second run would otherwise open with the
        /// first one's totals already on the board.
        ///
        /// On load rather than unload, unlike PlayerMovement, because this has a
        /// clock to start as well as counters to zero and the new scene's first
        /// frame is what it should be timed from. RunOutcome hooks the same event
        /// and deliberately does not clear there; nothing subscribes to this, so
        /// there is no subscription to lose.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Clear();

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Clear();
        }

        public static void Clear()
        {
            EnemiesDestroyed = 0;
            ExperienceEarned = 0;
            LevelReached = 1;

            skillOrder.Clear();
            skillCounts.Clear();

            startedAt = Time.time;
            endedAt = -1f;
        }
    }
}
