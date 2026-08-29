namespace SurvivalChaos
{
    /// <summary>
    /// Every input the game reads, expressed independently of which Unity input
    /// backend is supplying it. Swapping to com.unity.inputsystem means adding a
    /// second implementation of this interface - no gameplay script changes.
    /// </summary>
    public interface IGameInput
    {
        /// <summary>Smoothed -1..1 steering axis.</summary>
        float Horizontal { get; }

        /// <summary>Smoothed -1..1 climb/dive axis.</summary>
        float Vertical { get; }

        /// <summary>True on the frame the flip-direction key is released.</summary>
        bool ToggleDirectionReleased { get; }

        /// <summary>True on the frame the pause key is pressed.</summary>
        bool PausePressed { get; }

        /// <summary>True on the frame the debug "force level up" key is pressed.</summary>
        bool DebugLevelUpPressed { get; }

        /// <summary>True on the frame the performance overlay is toggled.</summary>
        bool DebugOverlayTogglePressed { get; }

        /// <summary>True on the frame the overlay is asked to export its report.</summary>
        bool DebugCopyReportPressed { get; }

        /// <summary>True on the frame the debug menu is toggled.</summary>
        bool DebugMenuTogglePressed { get; }

        /// <summary>
        /// The number key pressed this frame, 1 to 8, or 0 for none. Drives the
        /// debug menu's shortcuts.
        ///
        /// One member rather than eight booleans because the caller wants to
        /// switch on which key it was, and eight properties would be eight more
        /// things for a new IGameInput implementation to forget.
        /// </summary>
        int DebugShortcutPressed { get; }
    }
}
