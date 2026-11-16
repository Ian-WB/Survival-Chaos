using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// The frame cap's slider: a whole-number Slider whose left and right
    /// speed up while they are held.
    ///
    /// A plain Slider moves one whole number a press, which is what the cap
    /// wants for the last few frames and no use for crossing 270 of them on a
    /// pad. This keeps the single step and widens it once the direction has
    /// been held (<see cref="DisplayOptions.CapSliderStep"/>). The mouse is
    /// untouched: dragging is the Slider's own.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Frame Cap Slider")]
    public sealed class FrameCapSlider : Slider
    {
        /// <summary>
        /// Presses closer together than this count as one hold. The input
        /// module repeats a held direction ten times a second; nobody taps that
        /// fast, so tapping stays one frame a press.
        /// </summary>
        private const float HoldGap = 0.2f;

        private int lastDirection;
        private float lastMoveTime = float.NegativeInfinity;
        private int repeats;

        public override void OnMove(AxisEventData eventData)
        {
            int direction = eventData.moveDir == MoveDirection.Right ? 1
                : eventData.moveDir == MoveDirection.Left ? -1 : 0;

            // Up and down are the rows', and so is a sideways press when the
            // navigation has somewhere to send it.
            bool ours = direction != 0 && IsActive() && IsInteractable() &&
                (direction > 0 ? FindSelectableOnRight() : FindSelectableOnLeft()) == null;

            if (!ours)
            {
                base.OnMove(eventData);
                return;
            }

            float now = Time.unscaledTime;
            repeats = direction == lastDirection && now - lastMoveTime <= HoldGap ? repeats + 1 : 0;
            lastDirection = direction;
            lastMoveTime = now;

            value = DisplayOptions.NextCapSliderPosition(Mathf.RoundToInt(value), direction, repeats);
        }
    }
}
