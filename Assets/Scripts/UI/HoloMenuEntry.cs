using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// One line in a title-screen menu: a label and a leading accent bar that
    /// respond to being pointed at.
    ///
    /// A title screen should not be a dialog box. Framing every option in its own
    /// panel is what made the first attempt read as a pause menu sitting on top
    /// of the artwork rather than part of it, so there is no box here - the motion
    /// does the work the frame was doing.
    ///
    /// Selection is handled as well as hover, so the list still reads correctly
    /// when driven by a keyboard or a pad.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Holo Menu Entry")]
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class HoloMenuEntry : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        IPointerClickHandler
    {
        [SerializeField]
        [Tooltip("The text. Slides right when this entry is highlighted.")]
        private RectTransform label;

        [SerializeField]
        [Tooltip("The bar to the left of the text. Brightens and widens when highlighted.")]
        private RectTransform accent;

        [SerializeField]
        [Tooltip("How far the label slides, in reference pixels.")]
        private float slide = 16f;

        [SerializeField]
        [Tooltip("Accent width at rest and when highlighted.")]
        private float accentRest = 4f;

        [SerializeField]
        private float accentHighlighted = 12f;

        [SerializeField]
        private Color restColour = new Color(0.55f, 0.95f, 1f, 0.45f);

        [SerializeField]
        private Color highlightColour = new Color(0.7f, 1f, 1f, 1f);

        [SerializeField]
        [Tooltip("How sharply it settles. Higher is snappier.")]
        private float response = 16f;

        private Graphic accentGraphic;
        private Graphic labelGraphic;
        private Vector2 labelRest;
        private float highlight;
        private float target;

        private void Awake()
        {
            if (label != null)
            {
                labelRest = label.anchoredPosition;
                label.TryGetComponent(out labelGraphic);
            }

            if (accent != null)
            {
                accent.TryGetComponent(out accentGraphic);
            }

            Apply();
        }

        private void OnEnable()
        {
            // A screen can be reopened with the pointer somewhere else entirely,
            // so an entry must never come back still lit from last time.
            target = 0f;
            highlight = 0f;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData) => Arrive();

        public void OnPointerExit(PointerEventData eventData) => target = 0f;

        public void OnSelect(BaseEventData eventData) => Arrive();

        public void OnDeselect(BaseEventData eventData) => target = 0f;

        /// <summary>
        /// The same sounds the framed buttons make. The title screen's entries
        /// look nothing like those buttons on purpose, but they are still the same
        /// act — a menu where only half the things you click answer back reads as
        /// broken rather than as two styles.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.UiClick);
            }
        }

        private void Arrive()
        {
            target = 1f;

            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.UiHover);
            }
        }

        /// <summary>
        /// Unscaled: the pause screen runs at a stopped timeScale, and a menu
        /// that stops animating the moment it is needed feels broken.
        /// </summary>
        private void Update()
        {
            if (Mathf.Approximately(highlight, target))
            {
                return;
            }

            highlight = Mathf.Lerp(highlight, target, 1f - Mathf.Exp(-response * Time.unscaledDeltaTime));
            Apply();
        }

        private void Apply()
        {
            if (label != null)
            {
                label.anchoredPosition = labelRest + new Vector2(slide * highlight, 0f);
            }

            if (accent != null)
            {
                Vector2 size = accent.sizeDelta;
                size.x = Mathf.Lerp(accentRest, accentHighlighted, highlight);
                accent.sizeDelta = size;
            }

            Color colour = Color.Lerp(restColour, highlightColour, highlight);

            if (accentGraphic != null)
            {
                accentGraphic.color = colour;
            }

            if (labelGraphic != null)
            {
                labelGraphic.color = colour;
            }
        }
    }
}
