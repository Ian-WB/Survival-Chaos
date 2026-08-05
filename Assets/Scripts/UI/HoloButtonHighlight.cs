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
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
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

        private void Awake()
        {
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

        private void Highlight()
        {
            target = 1f;
            sweep = 0f;
        }

        /// <summary>
        /// Unscaled: every screen this appears on runs at a stopped timeScale, and
        /// a menu that stops animating exactly when it is being used feels broken.
        /// </summary>
        private void Update()
        {
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
            instance.SetFloat(GlowId, Mathf.Lerp(baseGlow, baseGlow * glowBoost, highlight));
            instance.SetFloat(BracketId, Mathf.Lerp(baseBracket, baseBracket * bracketBoost, highlight));

            Color fill = baseFill;
            fill.a = Mathf.Lerp(baseFill.a, Mathf.Min(1f, baseFill.a * fillBoost), highlight);
            instance.SetColor(FillId, fill);

            // 1 means settled; anything less draws the travelling line.
            instance.SetFloat(SweepId, sweep);

            if (label != null)
            {
                label.color = Color.Lerp(baseLabel, Color.white, highlight * 0.6f);
            }
        }
    }
}
