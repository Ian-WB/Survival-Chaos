using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// IGameInput backed by the legacy Input Manager. This is the implementation
    /// that ships today; it preserves the original call semantics exactly,
    /// including GetAxis smoothing (not GetAxisRaw) and the release-edge - not
    /// press-edge - trigger on the direction toggle.
    /// </summary>
    public sealed class LegacyGameInput : IGameInput
    {
        public float Horizontal => Input.GetAxis("Horizontal");

        public float Vertical => Input.GetAxis("Vertical");

        public bool ToggleDirectionReleased => Input.GetKeyUp(KeyCode.LeftShift);

        public bool PausePressed => Input.GetKeyDown(KeyCode.Escape);

        public bool DebugLevelUpPressed => Input.GetKeyDown(KeyCode.F7);
    }
}
