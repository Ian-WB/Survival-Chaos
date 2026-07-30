namespace SurvivalChaos
{
    /// <summary>
    /// Single access point for input. Gameplay scripts read GameInput.Horizontal
    /// rather than touching a Unity input API directly, so the backend can be
    /// replaced in one place.
    /// </summary>
    public static class GameInput
    {
        private static IGameInput source = new LegacyGameInput();

        /// <summary>
        /// The active backend. Assigning null restores the legacy backend, so a
        /// test that forgets to clean up cannot leave the game without input.
        /// </summary>
        public static IGameInput Source
        {
            get => source;
            set => source = value ?? new LegacyGameInput();
        }

        public static float Horizontal => source.Horizontal;

        public static float Vertical => source.Vertical;

        public static bool ToggleDirectionReleased => source.ToggleDirectionReleased;

        public static bool PausePressed => source.PausePressed;

        public static bool DebugLevelUpPressed => source.DebugLevelUpPressed;
    }
}
