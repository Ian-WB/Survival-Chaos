#if ENABLE_LEGACY_INPUT_MANAGER
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

        public bool DashPressed => Input.GetKeyDown(KeyCode.Space);

        public bool DebugLevelUpPressed => Input.GetKeyDown(KeyCode.F7);

        public bool DebugOverlayTogglePressed => Input.GetKeyDown(KeyCode.F3);

        public bool DebugCopyReportPressed => Input.GetKeyDown(KeyCode.F4);

        public bool DebugMenuTogglePressed => Input.GetKeyDown(KeyCode.F8);

        public int DebugShortcutPressed
        {
            get
            {
                for (int i = 1; i <= 8; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                    {
                        return i;
                    }
                }

                return 0;
            }
        }
    }
}
#endif
