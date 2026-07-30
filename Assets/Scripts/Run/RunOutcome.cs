using System;
using UnityEngine;

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

        /// <summary>Drops every subscriber.</summary>
        public static void Clear()
        {
            BossDefeated = null;
        }

        /// <summary>
        /// Static events outlive a scene, and outlive play mode entirely when
        /// domain reload is disabled. Clearing on subsystem registration keeps
        /// a stale listener from a previous run out of the next one.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Clear();
        }
    }
}
