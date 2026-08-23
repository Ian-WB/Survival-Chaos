#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace SurvivalChaos
{
    /// <summary>
    /// IGameInput backed by com.unity.inputsystem, reading keyboard and gamepad
    /// together.
    ///
    /// The Input System reports keys digitally, whereas the legacy Input Manager
    /// smoothed "Horizontal" and "Vertical" toward their target. Reading raw keys
    /// here would make the ship noticeably twitchier than it was, so this
    /// reproduces the legacy ramp using the Input Manager's default sensitivity,
    /// gravity, and snap behaviour.
    ///
    /// A stick does not want that ramp. It already reports how far it is pushed,
    /// so feeding it through a rate limit would add lag to the one input that
    /// never needed any - the ship would lean into a turn a fifth of a second
    /// after the stick did. Digital sources ramp, analogue sources do not, and
    /// the d-pad counts as digital: it is routed in beside the keys rather than
    /// beside the stick, because it is the same kind of signal wearing different
    /// hardware.
    ///
    /// No deadzone is applied here. Gamepad.leftStick already carries the layout's
    /// stickDeadzone processor, which on this project reads 0.125 to 0.925 from
    /// InputSettings; a second one stacked on top would rescale an already
    /// rescaled range and quietly cost the player the top of their stick travel.
    ///
    /// The debug bindings stay on the keyboard. F3, F4 and F7 exist to be
    /// deliberate, and there is no face button a player will not eventually press
    /// by accident.
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

        /// <summary>
        /// Released rather than pressed, on both devices, because that is the
        /// edge the legacy binding used and the flip reads as happening when the
        /// player lets go.
        /// </summary>
        public bool ToggleDirectionReleased
        {
            get
            {
                Keyboard keyboard = Keyboard.current;

                if (keyboard != null && keyboard.leftShiftKey.wasReleasedThisFrame)
                {
                    return true;
                }

                Gamepad pad = Gamepad.current;
                return pad != null && pad.leftShoulder.wasReleasedThisFrame;
            }
        }

        public bool PausePressed
        {
            get
            {
                Keyboard keyboard = Keyboard.current;

                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    return true;
                }

                Gamepad pad = Gamepad.current;
                return pad != null && pad.startButton.wasPressedThisFrame;
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
            Gamepad pad = Gamepad.current;

            Vector2 stick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
            Vector2 dpad = pad != null ? pad.dpad.ReadValue() : Vector2.zero;

            horizontal = Blend(
                horizontal,
                stick.x,
                ReadAxis(keyboard, Key.A, Key.LeftArrow, Key.D, Key.RightArrow) + dpad.x);

            vertical = Blend(
                vertical,
                stick.y,
                ReadAxis(keyboard, Key.S, Key.DownArrow, Key.W, Key.UpArrow) + dpad.y);
        }

        /// <summary>
        /// Resolves one axis from an analogue source and a digital one.
        ///
        /// The stick wins outright while it is deflected, and hands back the
        /// moment it centres. What it hands back to is the ramp, still holding
        /// the value the stick left it at - so letting go of the stick decays
        /// from where it was rather than snapping to zero, and a key pressed
        /// afterwards ramps on from there rather than from a standstill. Both
        /// devices stay live at once with no mode to switch and nothing to
        /// detect.
        /// </summary>
        private static float Blend(float current, float analogue, float digital)
        {
            if (analogue != 0f)
            {
                return analogue;
            }

            return Advance(current, Mathf.Clamp(digital, -1f, 1f));
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
