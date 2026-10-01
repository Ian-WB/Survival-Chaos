using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalChaos
{
    /// <summary>
    /// Remembers the simulation speed independently of a pause or ending.
    /// Changing slow motion while paused updates the requested speed only;
    /// neither that change nor Resume can restart a finished run.
    ///
    /// Two speeds multiply: the debug menu's, and the player's Slow Mo ability
    /// (<see cref="AbilityScale"/>). Kept apart so the ability ending does not
    /// undo a debug speed, and a debug speed chosen mid-slowdown does not end
    /// the slowdown.
    ///
    /// Two short moments override both. The beat after the killing hit runs
    /// at <see cref="EndingSpeed"/> until the death card stops time, and a
    /// hit-stop (<see cref="Freeze"/>) all but stops the game for a few
    /// hundredths of a second when the Leviathan loses a part.
    /// </summary>
    public static class RunTime
    {
        /// <summary>
        /// The game's speed during a hit-stop. Not zero: several scripts read
        /// a stopped clock as a pause and drop the input they would otherwise
        /// take, and a dash pressed in the freeze should still go.
        /// </summary>
        public const float FreezeScale = 0.02f;

        public static float RequestedSpeed { get; private set; } = 1f;

        /// <summary>The Slow Mo ability's share of the speed: 1 when it is not running.</summary>
        public static float AbilityScale { get; private set; } = 1f;

        /// <summary>The speed of the beat between the killing hit and the death card.</summary>
        public static float EndingSpeed { get; private set; } = 0.25f;

        /// <summary>True while a hit-stop holds the game.</summary>
        public static bool Frozen { get; private set; }

        /// <summary>
        /// True while the loading screen holds the game still. Nothing else
        /// restarts time under it: until 1 October 2026 the screen set the
        /// time scale itself, and backing out of the pause menu behind it
        /// resumed the run that was being replaced.
        /// </summary>
        public static bool Held { get; private set; }

        /// <summary>When the hit-stop ends, in unscaled seconds.</summary>
        private static float frozenUntil;

        /// <summary>
        /// The speed sounds follow: the game's, without the hit-stop. A freeze
        /// is a visual beat, and a sound pitched to a fiftieth of itself for 70
        /// ms would be heard as the audio breaking.
        /// </summary>
        public static float SoundSpeed => RequestedSpeed * AbilityScale;

        public static void SetSpeed(float speed)
        {
            if (RunOutcome.RunEnded)
            {
                Time.timeScale = 0f;
                return;
            }

            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
            {
                return;
            }

            RequestedSpeed = Mathf.Clamp(speed, 0.1f, 4f);
            Apply();
        }

        /// <summary>
        /// Sets the Slow Mo ability's share, 1 to end it. Clamped to (0, 1]: the
        /// ability only ever slows, and a zero here would be a pause nobody can
        /// resume. Ignored when not a number.
        /// </summary>
        public static void SetAbilityScale(float scale)
        {
            if (float.IsNaN(scale) || scale <= 0f)
            {
                return;
            }

            AbilityScale = Mathf.Clamp(scale, 0.05f, 1f);
            Apply();
        }

        /// <summary>
        /// Sets the ending beat's speed and applies it. Called by
        /// <see cref="RunOutcome.ReportEnding"/>, which owns the flag.
        /// </summary>
        internal static void SetEndingSpeed(float speed)
        {
            if (!float.IsNaN(speed))
            {
                // Down to the hit-stop's own scale: the Leviathan's death
                // opens on a freeze, and a floor of 0.05 ran it two and a
                // half times faster than it asked for.
                EndingSpeed = Mathf.Clamp(speed, FreezeScale, 1f);
            }

            Apply();
        }

        /// <summary>
        /// Holds the game for <paramref name="seconds"/> of real time. A longer
        /// freeze already running is not cut short. Nothing happens once the
        /// run is decided, or under the pause menu.
        /// </summary>
        public static void Freeze(float seconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || RunOutcome.Decided || PauseMenu.GameIsPaused)
            {
                return;
            }

            frozenUntil = Mathf.Max(frozenUntil, Time.unscaledTime + seconds);
            Frozen = true;
            RunTimeTicker.Ensure();
            Apply();
        }

        /// <summary>Ends a hit-stop whose time is up. Called every frame by the ticker.</summary>
        public static void Tick(float now)
        {
            if (Frozen && now >= frozenUntil)
            {
                Frozen = false;
                Apply();
            }
        }

        /// <summary>Stops the game for a load, or gives it back once the new scene is ready.</summary>
        public static void SetHold(bool held)
        {
            Held = held;
            Apply();
        }

        public static void CycleSlowMotion()
        {
            SetSpeed(RequestedSpeed > 0.9f ? 0.5f : RequestedSpeed > 0.4f ? 0.25f : 1f);
        }

        public static void Apply()
        {
            if (Held || PauseMenu.GameIsPaused || RunOutcome.RunEnded)
            {
                Time.timeScale = 0f;
                return;
            }

            if (RunOutcome.Ending)
            {
                Time.timeScale = EndingSpeed;
                return;
            }

            float speed = RequestedSpeed * AbilityScale;
            Time.timeScale = Frozen ? speed * FreezeScale : speed;
        }

        /// <summary>A retry starts at normal speed, including with domain reload disabled.</summary>
        public static void ResetForNewRun()
        {
            RequestedSpeed = 1f;
            AbilityScale = 1f;
            EndingSpeed = 0.25f;
            Frozen = false;
            frozenUntil = 0f;
            PauseMenu.GameIsPaused = false;

            // A load's hold outlives the scene it replaces: this runs as the
            // old scene unloads, in the middle of the load.
            Time.timeScale = Held ? 0f : 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            Held = false;
            ResetForNewRun();
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            ResetForNewRun();
        }
    }
}
