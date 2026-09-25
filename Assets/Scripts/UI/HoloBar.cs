using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// Feeds a bar's value into the holographic bar shader.
    ///
    /// Reads whatever source the element already has - a Slider for health, the
    /// boss and the timer, or an Image's fillAmount for experience - so the
    /// existing gameplay scripts keep working untouched. They still set the same
    /// Slider values they always did; this only changes how that is drawn.
    ///
    /// The option screens' volume and sharpness bars are sliders the player
    /// moves, and those also show when they have keyboard or pad focus. The
    /// HUD's bars are never selected - their sliders are not interactable and
    /// take no navigation - so for them that part never runs.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Holo Bar")]
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public sealed class HoloBar : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField]
        [Tooltip("Where the value comes from. Leave empty to read this element's own Image fill.")]
        private Slider source;

        [SerializeField]
        [Tooltip("Below this fraction the bar pulses. Set to 0 for bars where low is not urgent, " +
                 "like experience.")]
        [Range(0f, 1f)]
        private float lowThreshold = 0.25f;

        [SerializeField]
        [Tooltip("Seconds the stretch lost to a hit stays on screen before it drains away.")]
        private float ghostHold = 0.45f;

        [Header("Selected (option bars only)")]
        [SerializeField]
        [Tooltip("Line work brightness multiplier while the bar has focus.")]
        private float selectedGlowBoost = 1.6f;

        [SerializeField]
        [Tooltip("Frame thickness multiplier while the bar has focus. A shape change as well as a " +
                 "brightness one, because a full bar's frame is already near white.")]
        private float selectedBorderBoost = 2f;

        [SerializeField]
        [Tooltip("How far the fill moves toward white while the bar has focus.")]
        [Range(0f, 1f)]
        private float selectedFillLift = 0.35f;

        [SerializeField]
        [Tooltip("How sharply the focus highlight settles. Higher is snappier.")]
        private float selectedResponse = 16f;

        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int GhostId = Shader.PropertyToID("_Ghost");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int BorderId = Shader.PropertyToID("_Border");
        private static readonly int FillColourId = Shader.PropertyToID("_FillColor");

        private readonly BarMotion motion = new BarMotion();

        private Graphic graphic;
        private Image image;
        private Material instance;

        private float baseGlow;
        private float baseBorder;
        private Color baseFillColour;

        private float selected;
        private float selectedTarget;
        private float appliedSelected = -1f;

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
            image = graphic as Image;

            if (graphic.material == null)
            {
                Debug.LogWarning("HoloBar has no material, so it cannot drive the bar shader.", this);
                enabled = false;
                return;
            }

            // One material per bar: the shader carries this bar's value, and a
            // shared material would make every bar show the same number.
            instance = new Material(graphic.material);
            graphic.material = instance;

            baseGlow = instance.GetFloat(GlowId);
            baseBorder = instance.GetFloat(BorderId);
            baseFillColour = instance.GetColor(FillColourId);

            motion.HoldSeconds = ghostHold;
            motion.SnapTo(ReadTarget());
            Apply();
        }

        private void OnEnable()
        {
            // A screen can reopen with focus somewhere else entirely, so a bar
            // must never come back still lit from last time. Focus, if it lands
            // here, arrives as a fresh select.
            selected = 0f;
            selectedTarget = 0f;
        }

        private void OnDestroy()
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            selectedTarget = 1f;

            // The same cue a button gives when focus arrives, so moving down a
            // column of mixed buttons and bars sounds like one list.
            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.UiHover);
            }
        }

        public void OnDeselect(BaseEventData eventData) => selectedTarget = 0f;

        /// <summary>
        /// Unscaled time throughout: the death, victory and pause screens all set
        /// timeScale to zero, and a bar frozen mid-drain behind a menu looks
        /// broken rather than paused.
        /// </summary>
        private void LateUpdate()
        {
            motion.Advance(ReadTarget(), Time.unscaledDeltaTime);

            if (!Mathf.Approximately(selected, selectedTarget))
            {
                selected = Mathf.Lerp(selected, selectedTarget,
                    1f - Mathf.Exp(-selectedResponse * Time.unscaledDeltaTime));
            }

            Apply();
        }

        private float ReadTarget()
        {
            if (source != null)
            {
                return source.normalizedValue;
            }

            return image != null ? image.fillAmount : 1f;
        }

        private void Apply()
        {
            instance.SetFloat(FillId, motion.Fill);
            instance.SetFloat(GhostId, motion.Ghost);

            float pulse = lowThreshold > 0f && motion.Fill < lowThreshold
                ? 1f - motion.Fill / lowThreshold
                : 0f;

            instance.SetFloat(PulseId, pulse);

            // Written only when it moves: the HUD's bars never leave zero, and
            // have no reason to rewrite three properties every frame.
            if (Mathf.Approximately(selected, appliedSelected))
            {
                return;
            }

            appliedSelected = selected;
            instance.SetFloat(GlowId, baseGlow * Mathf.Lerp(1f, selectedGlowBoost, selected));
            instance.SetFloat(BorderId, baseBorder * Mathf.Lerp(1f, selectedBorderBoost, selected));

            Color fill = Color.Lerp(baseFillColour, Color.white, selectedFillLift * selected);
            fill.a = baseFillColour.a;
            instance.SetColor(FillColourId, fill);
        }
    }
}
