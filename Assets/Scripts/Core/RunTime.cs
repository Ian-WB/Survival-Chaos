using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalChaos
{
    /// <summary>
    /// Remembers the simulation speed independently of a pause or ending.
    /// Changing slow motion while paused updates the requested speed only;
    /// neither that change nor Resume can restart a finished run.
    /// </summary>
    public static class RunTime
    {
        public static float RequestedSpeed { get; private set; } = 1f;

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

        public static void CycleSlowMotion()
        {
            SetSpeed(RequestedSpeed > 0.9f ? 0.5f : RequestedSpeed > 0.4f ? 0.25f : 1f);
        }

        public static void Apply()
        {
            Time.timeScale = PauseMenu.GameIsPaused || RunOutcome.RunEnded ? 0f : RequestedSpeed;
        }

        /// <summary>A retry starts at normal speed, including with domain reload disabled.</summary>
        public static void ResetForNewRun()
        {
            RequestedSpeed = 1f;
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
