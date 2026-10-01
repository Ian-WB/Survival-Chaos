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
    /// Counted as it happens rather than derived at the end. Enemies destroyed is
    /// not recoverable from anything at all once they are back in the pool.
    /// Experience used not to be recoverable either, while every level-up
    /// emptied the bar; the bar carries over now, but the debug menu's level-ups
    /// cost nothing, so working it back from the thresholds would still be wrong.
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

        /// <summary>The frame the clock stopped on, or -1 while it runs.</summary>
        private static int endedFrame = -1;

        /// <summary>
        /// The game clock and frame count the stats read. Unity's own, except
        /// in the edit mode tests, where neither moves and a test of "the
        /// clock stopped" or "that came a frame too late" could not fail.
        /// </summary>
        public static System.Func<float> Clock = UnityClock;
        public static System.Func<int> Frame = UnityFrame;

        private static float UnityClock() => Time.time;
        private static int UnityFrame() => Time.frameCount;

        /// <summary>
        /// True once the frame that decided the run is over. The card reports
        /// the run as it stood at the deciding hit, like its clock: until 1
        /// October 2026 a kill or a level landing in the beat afterwards still
        /// counted, under a time that had already stopped (ChatGPT's scan of
        /// that day). What happens in the deciding frame itself counts,
        /// which is what lets the Leviathan's own kill and experience in: it
        /// settles the run first and pays out second.
        /// </summary>
        public static bool Closed => ClosedAt(endedFrame, Frame());

        public static bool ClosedAt(int endedOnFrame, int frame)
        {
            return endedOnFrame >= 0 && frame > endedOnFrame;
        }

        /// <summary>
        /// How long the run lasted, in seconds, frozen once it ends.
        ///
        /// Scaled time, so the clock stops while the game is paused. Surviving is
        /// something done with the game running; a player who spends four minutes
        /// on the pause screen has not survived four minutes.
        /// </summary>
        public static float Seconds =>
            Mathf.Max(0f, (endedAt >= 0f ? endedAt : Clock()) - startedAt);

        /// <summary>Skills picked, in the order they were first taken.</summary>
        public static IReadOnlyList<string> SkillOrder => skillOrder;

        public static int PicksOf(string skill)
        {
            return skill != null && skillCounts.TryGetValue(skill, out int taken) ? taken : 0;
        }

        public static void RecordKill()
        {
            if (Closed)
            {
                return;
            }

            EnemiesDestroyed++;
        }

        /// <summary>
        /// Experience as the player's bar took it, after the multiplier. Kept
        /// apart from the kill count because it is the player that scales it:
        /// totalled from the rewards before scaling, it read a third of what the
        /// bar had filled with at the multiplier of 3.
        /// </summary>
        public static void RecordExperience(int amount)
        {
            if (Closed)
            {
                return;
            }

            ExperienceEarned += Mathf.Max(0, amount);
        }

        /// <summary>
        /// Highest rather than latest, so this cannot be walked backwards by
        /// anything that reports a level out of order.
        /// </summary>
        public static void RecordLevel(int level)
        {
            if (!Closed && level > LevelReached)
            {
                LevelReached = level;
            }
        }

        public static void RecordSkill(string skill)
        {
            if (Closed || string.IsNullOrEmpty(skill))
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
        /// <summary>
        /// What the killing hit came from, for the Ship Lost card's first line.
        /// Null in a run nothing has ended, and in a won one.
        /// </summary>
        public static string KilledBy { get; private set; }

        /// <summary>Records what ended the run. The first one stands, like the ending.</summary>
        public static void RecordKiller(string source)
        {
            if (KilledBy == null && !string.IsNullOrEmpty(source))
            {
                KilledBy = source;
            }
        }

        public static void Stop()
        {
            if (endedAt < 0f)
            {
                endedAt = Clock();
                endedFrame = Frame();
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
            Clock = UnityClock;
            Frame = UnityFrame;
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
            KilledBy = null;

            startedAt = Clock();
            endedAt = -1f;
            endedFrame = -1;
        }
    }
}
