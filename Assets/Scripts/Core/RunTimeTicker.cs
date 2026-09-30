using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Ends <see cref="RunTime"/>'s hit-stops on time.
    ///
    /// RunTime is static and only acts when told, and a freeze has to end
    /// with nobody telling it. A coroutine on the boss would have been the
    /// obvious host, but the boss stops every coroutine it owns at the moment
    /// an act ends, which is one of the two moments that freeze - so the
    /// freeze would never have ended. This lives beside the scenes instead,
    /// made on first use like the settings store.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class RunTimeTicker : MonoBehaviour
    {
        private static RunTimeTicker host;

        internal static void Ensure()
        {
            // Edit-mode tests drive RunTime.Tick themselves, and an object
            // cannot be kept across scene loads outside play.
            if (host != null || !Application.isPlaying)
            {
                return;
            }

            GameObject go = new GameObject("Run Time Ticker");
            host = go.AddComponent<RunTimeTicker>();
            DontDestroyOnLoad(go);
        }

        // Real time: the clock it is ending a freeze of is the one that is frozen.
        private void Update()
        {
            RunTime.Tick(Time.unscaledTime);
        }

        /// <summary>A host from a previous play session is gone, but the static still points at it.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            host = null;
        }
    }
}
