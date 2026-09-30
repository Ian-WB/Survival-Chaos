using UnityEngine;
using UnityEngine.InputSystem;

namespace SurvivalChaos
{
    /// <summary>
    /// Short pulses on the pad's motors: a hit, a dash, a blocked hit, part of
    /// the Leviathan falling, and the ship lost.
    ///
    /// The screen deliberately stays still when the ship is hit - the health
    /// bar flinches instead, because the camera already orbits - so this says
    /// the same thing through the hands without moving anything on screen.
    ///
    /// Only for a player holding the pad: one lying on the desk while the
    /// keys are played does not buzz. Switched off on the Controls tab.
    /// The motors stop when a pulse ends, under the pause menu, when the
    /// window loses focus and on quitting, so a pad is never left running.
    /// </summary>
    public static class Rumble
    {
        private const string EnabledKey = "SurvivalChaos.Controls.Rumble";

        /// <summary>The pulses the game uses, weakest first.</summary>
        public enum Strength
        {
            /// <summary>A dash, or switching rumble on.</summary>
            Light,

            /// <summary>The deflector taking a hit for the ship.</summary>
            Block,

            /// <summary>The ship taking a hit.</summary>
            Hit,

            /// <summary>An emplacement going down, or an act ending.</summary>
            Heavy,

            /// <summary>The ship lost.</summary>
            Death
        }

        private static bool running;
        private static float until;
        private static float runningLow;
        private static float runningHigh;

        /// <summary>Whether rumble is on. On by default.</summary>
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                SettingsStore.MarkDirty();

                if (!value)
                {
                    Stop();
                }
            }
        }

        /// <summary>
        /// Low motor, high motor, and real seconds. The low motor is the heavy
        /// one, so the big moments lean on it and the small ones on the high.
        /// </summary>
        public static (float low, float high, float seconds) Of(Strength strength)
        {
            switch (strength)
            {
                case Strength.Light: return (0.12f, 0.25f, 0.08f);
                case Strength.Block: return (0.2f, 0.45f, 0.1f);
                case Strength.Hit: return (0.45f, 0.6f, 0.15f);
                case Strength.Heavy: return (0.6f, 0.35f, 0.22f);
                default: return (0.9f, 0.6f, 0.6f);
            }
        }

        public static void Pulse(Strength strength)
        {
            (float low, float high, float seconds) = Of(strength);
            Pulse(low, high, seconds);
        }

        /// <summary>
        /// Runs the motors for <paramref name="seconds"/> of real time. A pulse
        /// arriving over a stronger one keeps the stronger one's force and runs
        /// to whichever ends later.
        /// </summary>
        public static void Pulse(float low, float high, float seconds)
        {
            if (!Application.isPlaying || !Enabled || !GameInput.PadLastUsed || PauseMenu.GameIsPaused)
            {
                return;
            }

            Gamepad pad = Gamepad.current;
            if (pad == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (running && now < until)
            {
                low = Mathf.Max(low, runningLow);
                high = Mathf.Max(high, runningHigh);
                seconds = Mathf.Max(seconds, until - now);
            }

            running = true;
            runningLow = low;
            runningHigh = high;
            until = now + seconds;

            RumbleHost.Ensure();
            pad.SetMotorSpeeds(low, high);
        }

        /// <summary>Stops a pulse that has run its time, or any pulse under the pause menu.</summary>
        internal static void Tick(float now)
        {
            if (running && (now >= until || PauseMenu.GameIsPaused))
            {
                Stop();
            }
        }

        /// <summary>Stops every pad's motors now.</summary>
        public static void Stop()
        {
            if (!running)
            {
                return;
            }

            running = false;
            InputSystem.ResetHaptics();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            running = false;
            until = 0f;
            runningLow = 0f;
            runningHigh = 0f;
        }
    }
}
