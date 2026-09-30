using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Ends <see cref="Rumble"/>'s pulses on time and stops the motors when the
    /// game goes away. Made on the first pulse, and kept across scenes like the
    /// settings store, so a pulse started as a scene ends still stops.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class RumbleHost : MonoBehaviour
    {
        private static RumbleHost host;

        internal static void Ensure()
        {
            if (host != null || !Application.isPlaying)
            {
                return;
            }

            GameObject go = new GameObject("Rumble");
            host = go.AddComponent<RumbleHost>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            Rumble.Tick(Time.unscaledTime);
        }

        // A pad left buzzing while the player is in another window is the one
        // failure here that is worse than no rumble at all.
        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                Rumble.Stop();
            }
        }

        private void OnApplicationQuit()
        {
            Rumble.Stop();
        }

        private void OnDestroy()
        {
            Rumble.Stop();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            host = null;
        }
    }
}
