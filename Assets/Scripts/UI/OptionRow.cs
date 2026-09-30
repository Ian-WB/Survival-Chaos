using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// A settings row as one thing to stand on. Up and down move between rows,
    /// left and right change the value, and Enter or A steps it forward.
    ///
    /// Until 30 September 2026 a row was a label and two arrow buttons, and
    /// the arrows were the only way in. A pad had to walk sideways onto an
    /// arrow and press A for every step, and walk back off it to reach the next
    /// row. Ian, on a pad: "i have to press individual arrows". A settings list
    /// is expected to work the way this one does now.
    ///
    /// The arrows stay, for the mouse, but they are out of navigation so the
    /// pad passes over them (see <see cref="MenuScreen"/> for handing focus back
    /// when the mouse has put it on one).
    ///
    /// A row stays reachable when its setting is greyed out, so the note under
    /// it can still be read from the pad. Stepping it does nothing.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Option Row")]
    public sealed class OptionRow : Selectable, IPointerMoveHandler, ISubmitHandler
    {
        [SerializeField]
        [Tooltip("The setting the row changes.")]
        private SteppedSetting option;

        [SerializeField]
        [Tooltip("The framed plate behind the row, faded in while the row has focus.")]
        private Graphic plate;

        [SerializeField]
        [Tooltip("How sharply the plate settles. Higher is snappier.")]
        private float response = 16f;

        private float lit;
        private float litTarget;

        public SteppedSetting Option => option;

        protected override void OnEnable()
        {
            base.OnEnable();

            // A screen can reopen with focus anywhere, so nothing comes back lit.
            // Focus, if it lands here, arrives as a fresh select.
            lit = 0f;
            litTarget = 0f;
            ApplyPlate();
        }

        /// <summary>
        /// Left and right change the value instead of leaving the row; up and
        /// down navigate as usual.
        /// </summary>
        public override void OnMove(AxisEventData eventData)
        {
            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    Step(-1);
                    eventData.Use();
                    break;

                case MoveDirection.Right:
                    Step(1);
                    eventData.Use();
                    break;

                default:
                    base.OnMove(eventData);
                    break;
            }
        }

        /// <summary>Enter and A step forward, as the right arrow would.</summary>
        public void OnSubmit(BaseEventData eventData) => Step(1);

        /// <summary>
        /// Hovering selects, as it does on every button, so the pointer and the
        /// pad share one highlight. Only a moving pointer, for the reason given on
        /// <see cref="HoloButtonHighlight.OnPointerEnter"/>.
        /// </summary>
        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if (eventData.IsPointerMoving())
            {
                eventData.selectedObject = gameObject;
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (eventData.selectedObject != gameObject && eventData.IsPointerMoving())
            {
                eventData.selectedObject = gameObject;
            }
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            litTarget = 1f;

            // The cue buttons and bars give when focus arrives, so a column of
            // rows sounds like the rest of the menu.
            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.UiHover);
            }
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            litTarget = 0f;
        }

        /// <summary>
        /// Changes the setting and clicks when it changed. A greyed-out row, or
        /// Shadows already at its end, stays silent, because a click would claim
        /// something happened.
        /// </summary>
        public bool Step(int direction)
        {
            if (option == null || !option.Step(direction))
            {
                return false;
            }

            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.UiClick);
            }

            return true;
        }

        /// <summary>Unscaled: the option screens only ever open with time stopped.</summary>
        private void Update()
        {
            if (Mathf.Approximately(lit, litTarget))
            {
                return;
            }

            lit = Mathf.Lerp(lit, litTarget, 1f - Mathf.Exp(-response * Time.unscaledDeltaTime));
            if (Mathf.Abs(lit - litTarget) < 0.004f)
            {
                lit = litTarget;
            }

            ApplyPlate();
        }

        /// <summary>
        /// All four channels, not only alpha. The holo panel shader scales its
        /// colour by the vertex colour and its alpha by the vertex alpha
        /// separately, and the edge glow adds light, so fading alpha alone would
        /// leave a glowing frame on an empty row.
        /// </summary>
        private void ApplyPlate()
        {
            if (plate != null)
            {
                plate.color = new Color(lit, lit, lit, lit);
            }
        }
    }
}
