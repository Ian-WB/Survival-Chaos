using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Gets settings onto disk without waiting for a clean shutdown.
    ///
    /// PlayerPrefs only flushes on a graceful quit. This game has a known crash
    /// mode, and a crash therefore discarded every setting changed since launch -
    /// including, at its worst, the VSync change a player had just made to stop
    /// the crash. That is the one setting you cannot afford to lose.
    ///
    /// Saving on every write is not the answer either: the sharpness slider
    /// writes continuously while it is being dragged, which would be a file write
    /// per frame. So writes mark the store dirty and the actual save lands a
    /// moment after the last change, plus immediately whenever the game loses
    /// focus or goes away.
    ///
    /// Creates itself on first use - nothing to place in a scene, matching how
    /// the two directors that use it work.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsStore : MonoBehaviour
    {
        /// <summary>
        /// Quiet time after the last change before the save happens. Long enough
        /// to cover a slider drag, short enough that a crash cannot take much.
        /// </summary>
        private const float FlushDelay = 0.75f;

        private static SettingsStore host;
        private static bool dirty;
        private static float flushAt;

        /// <summary>Records that a setting changed. The save follows shortly.</summary>
        public static void MarkDirty()
        {
            dirty = true;
            flushAt = Time.unscaledTime + FlushDelay;
            EnsureHost();
        }

        /// <summary>Writes now, if anything is waiting. Safe to call at any time.</summary>
        public static void Flush()
        {
            if (!dirty)
            {
                return;
            }

            dirty = false;
            PlayerPrefs.Save();
        }

        private static void EnsureHost()
        {
            if (host != null)
            {
                return;
            }

            GameObject go = new GameObject("Settings Store");
            host = go.AddComponent<SettingsStore>();
            DontDestroyOnLoad(go);
        }

        // Unscaled: every settings screen in the game runs at a stopped
        // timeScale, so a scaled timer would never come due.
        private void Update()
        {
            if (dirty && Time.unscaledTime >= flushAt)
            {
                Flush();
            }
        }

        // Alt-tabbing away is the last moment reliably available before a machine
        // sleeps, a driver falls over, or the player kills the process.
        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                Flush();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Flush();
            }
        }

        private void OnApplicationQuit()
        {
            Flush();
        }

        /// <summary>
        /// Static state outlives play mode when domain reload is disabled, and a
        /// host from a previous session has already been destroyed.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            host = null;
            dirty = false;
            flushAt = 0f;
        }
    }
}
