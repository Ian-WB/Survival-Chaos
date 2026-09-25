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
    /// </summary>
    public static class RunTime
    {
        public static float RequestedSpeed { get; private set; } = 1f;

        /// <summary>The Slow Mo ability's share of the speed: 1 when it is not running.</summary>
        public static float AbilityScale { get; private set; } = 1f;

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

        public static void CycleSlowMotion()
        {
            SetSpeed(RequestedSpeed > 0.9f ? 0.5f : RequestedSpeed > 0.4f ? 0.25f : 1f);
        }

        public static void Apply()
        {
            Time.timeScale = PauseMenu.GameIsPaused || RunOutcome.RunEnded ? 0f : RequestedSpeed * AbilityScale;
        }

        /// <summary>A retry starts at normal speed, including with domain reload disabled.</summary>
        public static void ResetForNewRun()
        {
            RequestedSpeed = 1f;
            AbilityScale = 1f;
            PauseMenu.GameIsPaused = false;
            Time.timeScale = 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
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
