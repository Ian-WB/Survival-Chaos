using UnityEngine;
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
    /// </summary>
    [AddComponentMenu("Survival Chaos/Holo Bar")]
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public sealed class HoloBar : MonoBehaviour
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

        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int GhostId = Shader.PropertyToID("_Ghost");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");

        private readonly BarMotion motion = new BarMotion();

        private Graphic graphic;
        private Image image;
        private Material instance;

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

            motion.HoldSeconds = ghostHold;
            motion.SnapTo(ReadTarget());
            Apply();
        }

        private void OnDestroy()
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        /// <summary>
        /// Unscaled time throughout: the death, victory and pause screens all set
        /// timeScale to zero, and a bar frozen mid-drain behind a menu looks
        /// broken rather than paused.
        /// </summary>
        private void LateUpdate()
        {
            motion.Advance(ReadTarget(), Time.unscaledDeltaTime);
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
        }
    }
}
