using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// A whole-number Slider whose left and right speed up while they are held.
    ///
    /// A plain Slider moves one whole number a press, which is what the graphics
    /// sliders want for fine-tuning and no use for crossing 270 frame rates on a
    /// pad. This keeps the single step and widens it once the direction has been
    /// held (<see cref="DisplayOptions.NextSliderPosition"/>). The mouse is
    /// untouched: dragging is the Slider's own.
    ///
    /// It was FrameCapSlider until 17 November 2026, when render scale and
    /// dynamic resolution became sliders too. The file kept its GUID, so the
    /// frame cap bar already in the scenes is this component.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Hold Step Slider")]
    public sealed class HoldStepSlider : Slider
    {
        /// <summary>
        /// Presses closer together than this count as one hold. The input
        /// module repeats a held direction ten times a second; nobody taps that
        /// fast, so tapping stays one step a press.
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

            value = DisplayOptions.NextSliderPosition(Mathf.RoundToInt(value), direction, repeats,
                Mathf.RoundToInt(minValue), Mathf.RoundToInt(maxValue));
        }
    }
}
