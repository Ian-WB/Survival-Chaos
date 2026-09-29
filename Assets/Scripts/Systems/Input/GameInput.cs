using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Single access point for input. Gameplay scripts read GameInput.Horizontal
    /// rather than touching a Unity input API directly, so the backend can be
    /// replaced in one place.
    /// </summary>
    public static class GameInput
    {
        private static IGameInput source = CreateDefault();

        /// <summary>
        /// The active backend. Assigning null restores the default, which is how
        /// a test puts real input back in its teardown. Nothing does that on its
        /// own: a test that never assigns null leaves its fake installed.
        /// </summary>
        public static IGameInput Source
        {
            get => source;
            set => source = value ?? CreateDefault();
        }

        /// <summary>
        /// A fresh default on entering play. Play mode keeps the domain since 29
        /// September 2026, so without this a fake a test forgot to take out, or the
        /// axis ramps where the last session let go of them, would carry into the
        /// next session. The playtest bot hands input back itself when play ends.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            source = CreateDefault();
        }

        /// <summary>
        /// The Input System, which is the only backend the game has.
        ///
        /// There was a second, LegacyGameInput, for the old Input Manager. The
        /// project stopped compiling it when Player Settings went to the Input
        /// System alone, and by then it had fallen behind: pause only on Escape,
        /// no left trigger for Slow Mo, no ninth debug shortcut, and no way for
        /// menus to win focus back. Deleted on 26 September 2026 rather than
        /// kept as a backend that looked equivalent and was not.
        /// </summary>
        private static IGameInput CreateDefault()
        {
#if ENABLE_INPUT_SYSTEM
            return new InputSystemGameInput();
#else
#error Survival Chaos reads input through the Input System package only. Set Player Settings > Active Input Handling to Input System Package (New) or Both.
#endif
        }

        public static float Horizontal => source.Horizontal;

        public static float Vertical => source.Vertical;

        public static bool ToggleDirectionReleased => source.ToggleDirectionReleased;

        public static bool PausePressed => source.PausePressed;

        public static bool BackPressed => source.BackPressed;

        public static bool DashPressed => source.DashPressed;

        public static bool SlowMoPressed => source.SlowMoPressed;

        public static bool DebugLevelUpPressed => source.DebugLevelUpPressed;

        public static bool DebugOverlayTogglePressed => source.DebugOverlayTogglePressed;

        public static bool DebugCopyReportPressed => source.DebugCopyReportPressed;

        public static bool DebugMenuTogglePressed => source.DebugMenuTogglePressed;

        public static int DebugShortcutPressed => source.DebugShortcutPressed;
    }
}
