using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalChaos
{
    /// <summary>
    /// Run-level events that scene objects listen for.
    ///
    /// The boss is instantiated from a prefab mid-run, so it cannot hold a
    /// reference to anything in the scene. Rather than have it hunt for a menu
    /// by tag, it reports what happened and whatever cares subscribes.
    /// </summary>
    public static class RunOutcome
    {
        /// <summary>Raised once the boss has been destroyed.</summary>
        public static event Action BossDefeated;

        /// <summary>
        /// True once the run has finished, win or lose.
        ///
        /// Both endings stop time and put a screen up, but neither told the pause
        /// menu - which kept running, so Esc opened the pause screen over the
        /// death screen and resuming from it restarted a run the player had
        /// already lost, health at zero and all. This is what the pause menu
        /// checks so that cannot happen.
        /// </summary>
        public static bool RunEnded { get; private set; }

        /// <summary>Marks the run as over. Called by the death and victory screens.</summary>
        public static void ReportRunEnded()
        {
            RunEnded = true;
        }

        public static void ReportBossDefeated()
        {
            if (BossDefeated == null)
            {
                // Nothing subscribed. The usual cause is a listener sitting on
                // an inactive GameObject: OnEnable never runs there, so it never
                // subscribed. Without this the run would just carry on silently.
                Debug.LogWarning(
                    "Boss defeated, but nothing is listening, so the run will not end. " +
                    "Check that VictoryMenu is on an ACTIVE object and that its panel " +
                    "reference points at the inactive panel - not at itself.");
                return;
            }

            BossDefeated.Invoke();
        }

        /// <summary>Drops every subscriber, and marks the run as not yet over.</summary>
        public static void Clear()
        {
            BossDefeated = null;
            RunEnded = false;
        }

        /// <summary>
        /// Static events outlive a scene, and outlive play mode entirely when
        /// domain reload is disabled. Clearing on subsystem registration keeps
        /// a stale listener from a previous run out of the next one.
        ///
        /// The scene hook is what resets <see cref="RunEnded"/> between runs
        /// within one session: loading a scene is the only way a new run starts,
        /// by any route, so nothing has to remember to clear it.
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
            // The flag only - deliberately not Clear(). sceneLoaded fires after
            // the new scene's OnEnable has already run, so clearing the event
            // here would drop the subscription VictoryMenu just made and the
            // victory screen would never appear. Subscribers from the old scene
            // remove themselves in OnDisable as it unloads.
            RunEnded = false;
        }
    }
}
