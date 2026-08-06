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
        /// The active backend. Assigning null restores the default, so a test
        /// that forgets to clean up cannot leave the game without input.
        /// </summary>
        public static IGameInput Source
        {
            get => source;
            set => source = value ?? CreateDefault();
        }

        /// <summary>
        /// Picks the backend matching Player Settings' active input handling.
        /// With "Both" selected, ENABLE_INPUT_SYSTEM wins - reading through the
        /// legacy API when the new one is available offers nothing.
        /// </summary>
        private static IGameInput CreateDefault()
        {
#if ENABLE_INPUT_SYSTEM
            return new InputSystemGameInput();
#else
            return new LegacyGameInput();
#endif
        }

        public static float Horizontal => source.Horizontal;

        public static float Vertical => source.Vertical;

        public static bool ToggleDirectionReleased => source.ToggleDirectionReleased;

        public static bool PausePressed => source.PausePressed;

        public static bool DebugLevelUpPressed => source.DebugLevelUpPressed;

        public static bool DebugOverlayTogglePressed => source.DebugOverlayTogglePressed;

        public static bool DebugCopyReportPressed => source.DebugCopyReportPressed;
    }
}
