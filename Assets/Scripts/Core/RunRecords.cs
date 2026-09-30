using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalChaos
{
    /// <summary>
    /// The player's best runs: the longest survived, the highest level
    /// reached, and the fastest Leviathan kill. Kept in PlayerPrefs beside the
    /// settings, and saved the same way.
    ///
    /// Until 30 September 2026 the end card summed up a run and Try Again
    /// threw all of it away, so nothing in the game said whether a run had
    /// gone better than the last.
    ///
    /// A run the debug menu touched is not a record. Winding the clock on,
    /// handing out levels or a boss kit makes the numbers mean something else,
    /// and the builds a tester plays carry the debug menu, so without this the
    /// tester's own bests would be overwritten by their testing.
    /// </summary>
    public static class RunRecords
    {
        private const string Prefix = "SurvivalChaos.Records.";
        private const string LongestKey = Prefix + "LongestSeconds";
        private const string LevelKey = Prefix + "HighestLevel";
        private const string FastestKey = Prefix + "FastestWinSeconds";

        /// <summary>Which records a run beat.</summary>
        [Flags]
        public enum Beaten
        {
            None = 0,
            Longest = 1,
            Level = 2,
            Fastest = 4
        }

        /// <summary>What happened to the run just ended. Read by the end card.</summary>
        public static Beaten LastBeaten { get; private set; }

        /// <summary>Whether the debug menu has changed this run.</summary>
        public static bool Assisted { get; private set; }

        /// <summary>Whether this run has been submitted, so the first ending is the one that counts.</summary>
        private static bool submitted;

        /// <summary>The longest run survived, in seconds. 0 when there is none.</summary>
        public static float LongestSeconds => PlayerPrefs.GetFloat(LongestKey, 0f);

        /// <summary>The highest level reached in any run. 0 when there is none.</summary>
        public static int HighestLevel => PlayerPrefs.GetInt(LevelKey, 0);

        /// <summary>The fastest Leviathan kill, in seconds from the start of the run. 0 when there is none.</summary>
        public static float FastestWinSeconds => PlayerPrefs.GetFloat(FastestKey, 0f);

        /// <summary>True once any record exists.</summary>
        public static bool Any => LongestSeconds > 0f || HighestLevel > 0 || FastestWinSeconds > 0f;

        /// <summary>Called by every debug action that changes how a run goes.</summary>
        public static void MarkAssisted()
        {
            Assisted = true;
        }

        /// <summary>
        /// Records a finished run and reports what it beat. Only the first call
        /// in a run counts: the two endings can land in one frame, and the one
        /// on screen is the first.
        /// </summary>
        public static Beaten Submit(float seconds, int level, bool won)
        {
            if (submitted)
            {
                return LastBeaten;
            }

            submitted = true;
            LastBeaten = Assisted ? Beaten.None : Compare(seconds, level, won,
                LongestSeconds, HighestLevel, FastestWinSeconds);

            if ((LastBeaten & Beaten.Longest) != 0) { PlayerPrefs.SetFloat(LongestKey, seconds); }
            if ((LastBeaten & Beaten.Level) != 0) { PlayerPrefs.SetInt(LevelKey, level); }
            if ((LastBeaten & Beaten.Fastest) != 0) { PlayerPrefs.SetFloat(FastestKey, seconds); }

            if (LastBeaten != Beaten.None)
            {
                SettingsStore.MarkDirty();
            }

            return LastBeaten;
        }

        /// <summary>
        /// Which of the three a run beats, against the bests so far. A best of
        /// 0 means none yet, so any run sets it. Kept apart from PlayerPrefs so
        /// it can be tested directly.
        /// </summary>
        public static Beaten Compare(float seconds, int level, bool won,
            float longest, int highest, float fastest)
        {
            Beaten beaten = Beaten.None;

            // Whole seconds, which is what the card shows: a "new best" that
            // reads the same as the old one is a claim the player cannot check.
            if (Mathf.Floor(seconds) > Mathf.Floor(longest))
            {
                beaten |= Beaten.Longest;
            }

            if (level > highest)
            {
                beaten |= Beaten.Level;
            }

            if (won && seconds > 0f && (fastest <= 0f || Mathf.Floor(seconds) < Mathf.Floor(fastest)))
            {
                beaten |= Beaten.Fastest;
            }

            return beaten;
        }

        /// <summary>The three bests on one line, for the title screen. Empty when there are none.</summary>
        public static string Describe()
        {
            if (!Any)
            {
                return string.Empty;
            }

            var line = new StringBuilder("BEST");

            if (LongestSeconds > 0f)
            {
                line.Append("   SURVIVED ").Append(Clock(LongestSeconds));
            }

            if (HighestLevel > 0)
            {
                line.Append("   LEVEL ").Append(HighestLevel);
            }

            if (FastestWinSeconds > 0f)
            {
                line.Append("   LEVIATHAN DOWN IN ").Append(Clock(FastestWinSeconds));
            }

            return line.ToString();
        }

        /// <summary>Minutes and seconds, as the end card writes them.</summary>
        public static string Clock(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (whole / 60) + ":" + (whole % 60).ToString("00");
        }

        /// <summary>A fresh run: nothing submitted, and nothing assisted yet.</summary>
        public static void NewRun()
        {
            submitted = false;
            Assisted = false;
            LastBeaten = Beaten.None;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            NewRun();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // On load, like RunStats: a scene load is the only way a run starts.
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            NewRun();
        }
    }
}
