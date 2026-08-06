#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace SurvivalChaos
{
    /// <summary>
    /// IGameInput backed by com.unity.inputsystem.
    ///
    /// The Input System reports keys digitally, whereas the legacy Input Manager
    /// smoothed "Horizontal" and "Vertical" toward their target. Reading raw keys
    /// here would make the ship noticeably twitchier than it was, so this
    /// reproduces the legacy ramp using the Input Manager's default sensitivity,
    /// gravity, and snap behaviour.
    /// </summary>
    public sealed class InputSystemGameInput : IGameInput
    {
        /// <summary>Units per second the axis climbs toward a held direction.</summary>
        private const float Sensitivity = 3f;

        /// <summary>Units per second the axis falls back to zero when released.</summary>
        private const float Gravity = 3f;

        private float horizontal;
        private float vertical;
        private int steppedFrame = -1;

        public float Horizontal
        {
            get
            {
                Step();
                return horizontal;
            }
        }

        public float Vertical
        {
            get
            {
                Step();
                return vertical;
            }
        }

        public bool ToggleDirectionReleased
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && keyboard.leftShiftKey.wasReleasedThisFrame;
            }
        }

        public bool PausePressed
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            }
        }

        public bool DebugLevelUpPressed
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && keyboard.f7Key.wasPressedThisFrame;
            }
        }

        public bool DebugOverlayTogglePressed
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && keyboard.f3Key.wasPressedThisFrame;
            }
        }

        public bool DebugCopyReportPressed
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && keyboard.f4Key.wasPressedThisFrame;
            }
        }

        /// <summary>
        /// Advances smoothing at most once per frame, so it does not matter how
        /// many scripts read the axes or in what order.
        /// </summary>
        private void Step()
        {
            if (steppedFrame == Time.frameCount)
            {
                return;
            }

            steppedFrame = Time.frameCount;

            Keyboard keyboard = Keyboard.current;
            horizontal = Advance(horizontal, ReadAxis(keyboard, Key.A, Key.LeftArrow, Key.D, Key.RightArrow));
            vertical = Advance(vertical, ReadAxis(keyboard, Key.S, Key.DownArrow, Key.W, Key.UpArrow));
        }

        private static float Advance(float current, float target)
        {
            // Snap: reversing direction zeroes the axis first rather than
            // coasting through, matching the legacy axis configuration.
            if (target != 0f && current != 0f && Mathf.Sign(target) != Mathf.Sign(current))
            {
                current = 0f;
            }

            float rate = target != 0f ? Sensitivity : Gravity;
            return Mathf.MoveTowards(current, target, rate * Time.deltaTime);
        }

        private static float ReadAxis(Keyboard keyboard, Key negative, Key negativeAlt, Key positive, Key positiveAlt)
        {
            if (keyboard == null)
            {
                return 0f;
            }

            float value = 0f;

            if (keyboard[negative].isPressed || keyboard[negativeAlt].isPressed)
            {
                value -= 1f;
            }

            if (keyboard[positive].isPressed || keyboard[positiveAlt].isPressed)
            {
                value += 1f;
            }

            return value;
        }
    }
}
#endif
