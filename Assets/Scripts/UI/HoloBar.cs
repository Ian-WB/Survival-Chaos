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

        [Header("Flash and alert (HUD bars)")]
        [SerializeField]
        [Range(0.05f, 2f)]
        [Tooltip("Seconds a Flash takes to fade. A flash lights the bar the way focus does, at " +
                 "full strength, and lets go of it across this.")]
        private float flashSeconds = 0.45f;

        [SerializeField]
        [Tooltip("The fill colour the bar turns while Alert is up. The boss bar's crimson " +
                 "(#FF3B4E), so the timer's last seconds already look like what is coming.")]
        private Color alertColour = new Color(1.00f, 0.231f, 0.306f, 1.00f);

        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int SegmentsId = Shader.PropertyToID("_Segments");
        private static readonly int GhostId = Shader.PropertyToID("_Ghost");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");

        /// <summary>
        /// The least the bar pulses, whatever its fill: 1 for full strength.
        /// Set by the health bar on the ship's last hit point, where the pulse
        /// from the low threshold alone is already most of the way there but
        /// the warning should be unmistakable.
        /// </summary>
        public float PulseFloor { get; set; }

        /// <summary>
        /// How far the fill has turned to the alert colour, 0 to 1. Set by the
        /// timer for the ten seconds the distant horn gives before the boss.
        /// </summary>
        public float Alert { get; set; }

        /// <summary>What is left of the last flash, 1 at its start.</summary>
        private float flash;

        /// <summary>
        /// Lights the bar once, the way focus lights an option bar, and fades.
        /// For a moment the player should notice in the corner of their eye:
        /// a level reached, the dash back.
        /// </summary>
        public void Flash()
        {
            flash = 1f;
        }

        /// <summary>
        /// Sets how many segments the bar is ruled into, on this bar's own
        /// material. The hull bar keeps one segment for every two hit points
        /// however long it grows.
        /// </summary>
        public void SetSegments(float segments)
        {
            if (instance != null)
            {
                instance.SetFloat(SegmentsId, Mathf.Max(0f, segments));
            }
        }

        /// <summary>
        /// Changes the bar's own fill colour, the one Alert and focus are
        /// measured from. The hull bar's green, amber and crimson (17 November
        /// 2026 redesign). Keeps the material's alpha.
        /// </summary>
        public void SetFillColour(Color colour)
        {
            if (instance == null)
            {
                return;
            }

            colour.a = baseFillColour.a;
            baseFillColour = colour;

            // Forces Apply to write the colour on its next pass, even with
            // lift and alert unchanged.
            appliedLift = -1f;
        }

        /// <summary>The fill colour Alert and focus are measured from.</summary>
        public Color FillColour => baseFillColour;

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
        private float appliedLift = -1f;
        private float appliedAlert = -1f;

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

            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime / flashSeconds);
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
            pulse = Mathf.Max(pulse, PulseFloor);

            instance.SetFloat(PulseId, pulse);

            // Focus and a flash light the bar the same way, so the stronger of
            // the two is what shows.
            float lift = Mathf.Max(selected, flash);
            float alert = Mathf.Clamp01(Alert);

            // Written only when it moves: the HUD's bars spend nearly all their
            // time at zero, and have no reason to rewrite three properties
            // every frame.
            if (Mathf.Approximately(lift, appliedLift) && Mathf.Approximately(alert, appliedAlert))
            {
                return;
            }

            appliedLift = lift;
            appliedAlert = alert;
            instance.SetFloat(GlowId, baseGlow * Mathf.Lerp(1f, selectedGlowBoost, lift));
            instance.SetFloat(BorderId, baseBorder * Mathf.Lerp(1f, selectedBorderBoost, lift));

            Color fill = Color.Lerp(baseFillColour, alertColour, alert);
            fill = Color.Lerp(fill, Color.white, selectedFillLift * lift);
            fill.a = baseFillColour.a;
            instance.SetColor(FillColourId, fill);
        }
    }
}
