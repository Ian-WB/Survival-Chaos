using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// Hover and selection response for a framed holographic button.
    ///
    /// The title screen's entries slide, because they have no frame to react
    /// with. These do, so they react as the frame: the edge glow lifts, the
    /// corner brackets reach further along the sides, the fill warms, and a light
    /// runs up the panel once as the pointer arrives. Same idea, stated in the
    /// language of the shape it is applied to.
    ///
    /// Unity's own colour tint is not used. Tinting fades the whole image at once,
    /// including the fill, which flattens the frame rather than sharpening it.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Holo Button Highlight")]
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class HoloButtonHighlight : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        IPointerClickHandler
    {
        [SerializeField]
        [Tooltip("The framed image. Its material is copied so this button's glow is its own.")]
        private Graphic panel;

        [SerializeField]
        [Tooltip("Optional. Brightens along with the frame.")]
        private TMP_Text label;

        [SerializeField]
        [Tooltip("Edge glow multiplier when highlighted.")]
        private float glowBoost = 2.1f;

        [SerializeField]
        [Tooltip("How much further the corner brackets reach when highlighted.")]
        private float bracketBoost = 1.9f;

        [SerializeField]
        [Tooltip("How much denser the fill becomes when highlighted.")]
        private float fillBoost = 2f;

        [SerializeField]
        [Tooltip("Seconds for the light to travel up the panel on arrival.")]
        private float sweepDuration = 0.35f;

        [SerializeField]
        [Tooltip("How sharply it settles. Higher is snappier.")]
        private float response = 16f;

        [SerializeField]
        [Tooltip("How much of its brightness the frame keeps while the button is not interactable.")]
        private float disabledDim = 0.3f;

        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int BracketId = Shader.PropertyToID("_BracketArm");
        private static readonly int FillId = Shader.PropertyToID("_FillColor");
        private static readonly int SweepId = Shader.PropertyToID("_Sweep");

        private Material instance;
        private float baseGlow;
        private float baseBracket;
        private Color baseFill;
        private Color baseLabel;

        private float highlight;
        private float target;
        private float sweep = 1f;

        private Button button;
        private bool wasInteractable = true;

        /// <summary>
        /// Whether the button will accept a click.
        ///
        /// Unity's own transition is set to None on these buttons, so
        /// interactable blocks the click and changes nothing on screen. Without
        /// this, a disabled stepper still lit up on hover, still played the hover
        /// sound and still played a click sound for a click that did nothing -
        /// which is worse than no feedback, because it claims something happened.
        /// </summary>
        private bool Interactable => button == null || button.interactable;

        private void Awake()
        {
            button = GetComponent<Button>();
            wasInteractable = Interactable;

            if (panel == null || panel.material == null)
            {
                enabled = false;
                return;
            }

            // Its own copy. The builder gives every button the same material
            // asset, so without this one button lighting up would light them all.
            instance = new Material(panel.material);
            panel.material = instance;

            baseGlow = instance.GetFloat(GlowId);
            baseBracket = instance.GetFloat(BracketId);
            baseFill = instance.GetColor(FillId);

            if (label != null)
            {
                baseLabel = label.color;
            }

            Apply();
        }

        private void OnDestroy()
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        private void OnEnable()
        {
            // A menu can reopen with the pointer anywhere, so nothing may come
            // back still lit from the last time it was shown.
            target = 0f;
            highlight = 0f;
            sweep = 1f;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData) => Highlight();

        public void OnPointerExit(PointerEventData eventData) => target = 0f;

        public void OnSelect(BaseEventData eventData) => Highlight();

        public void OnDeselect(BaseEventData eventData) => target = 0f;

        /// <summary>
        /// The click sound. Here rather than on each button's onClick, because
        /// every framed button in the game already carries this component - the
        /// editor tools put it there - so there is nothing to wire and nothing to
        /// forget when a screen gains a button.
        ///
        /// Menu sounds answer to the Interface channel, which until now had a
        /// working slider and nothing to attenuate.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            // A disabled Selectable still delivers pointer events to the other
            // handlers on the object, so this has to decline for itself.
            if (!Interactable)
            {
                return;
            }

            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.UiClick);
            }
        }

        private void Highlight()
        {
            if (!Interactable)
            {
                return;
            }

            target = 1f;
            sweep = 0f;

            // Only when the pointer arrives, not on every frame it stays: this
            // runs again on reselection, and a menu that ticks while the mouse
            // rests on a button is worse than one that says nothing.
            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.UiHover);
            }
        }

        /// <summary>
        /// Unscaled: every screen this appears on runs at a stopped timeScale, and
        /// a menu that stops animating exactly when it is being used feels broken.
        /// </summary>
        private void Update()
        {
            // Nothing raises an event when interactable changes, and the settled
            // check below would otherwise skip the frame that needs to react to
            // it - leaving a row that just went inert still looking live.
            if (Interactable != wasInteractable)
            {
                wasInteractable = Interactable;

                if (!Interactable)
                {
                    target = 0f;
                    highlight = 0f;
                    sweep = 1f;
                }

                Apply();
                return;
            }

            bool settled = Mathf.Approximately(highlight, target) && sweep >= 1f;
            if (settled)
            {
                return;
            }

            float delta = Time.unscaledDeltaTime;
            highlight = Mathf.Lerp(highlight, target, 1f - Mathf.Exp(-response * delta));

            if (sweep < 1f)
            {
                sweep = sweepDuration > 0f ? Mathf.Min(1f, sweep + delta / sweepDuration) : 1f;
            }

            Apply();
        }

        private void Apply()
        {
            float dim = Interactable ? 1f : Mathf.Clamp01(disabledDim);

            instance.SetFloat(GlowId, Mathf.Lerp(baseGlow, baseGlow * glowBoost, highlight) * dim);

            // The bracket arm is a length rather than a brightness, so it is left
            // alone: a disabled button should read as dimmer, not as a different
            // shape from the ones beside it.
            instance.SetFloat(BracketId, Mathf.Lerp(baseBracket, baseBracket * bracketBoost, highlight));

            Color fill = baseFill;
            fill.a = Mathf.Lerp(baseFill.a, Mathf.Min(1f, baseFill.a * fillBoost), highlight) * dim;
            instance.SetColor(FillId, fill);

            // 1 means settled; anything less draws the travelling line.
            instance.SetFloat(SweepId, sweep);

            if (label != null)
            {
                Color colour = Color.Lerp(baseLabel, Color.white, highlight * 0.6f);
                colour.a = baseLabel.a * dim;
                label.color = colour;
            }
        }
    }
}
