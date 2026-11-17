using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The box the HUD lives in: the screen clamped to 16:9 and centred, then
    /// pulled in by a safe margin on every side (17 November 2026 redesign).
    ///
    /// On a 16:9 screen that is just the margin. On an ultrawide the corner
    /// elements stay over the 16:9 middle rather than drifting out to the far
    /// edges, and the hull bar, centred, stays under the ship either way. Edge
    /// effects that must reach the real screen edges (the low-hull vignette,
    /// the hit flare, off-screen offer arrows) live outside this frame.
    /// </summary>
    [AddComponentMenu("Survival Chaos/HUD Safe Frame")]
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class HudSafeFrame : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The widest the HUD's box may be, as width over height. 16:9 keeps ultrawide " +
                 "corners over the middle of the screen.")]
        private float maxAspect = 16f / 9f;

        [SerializeField]
        [Range(0f, 0.2f)]
        [Tooltip("Margin on every side, as a fraction of the clamped box's width (left and " +
                 "right) and height (top and bottom).")]
        private float margin = 0.055f;

        private RectTransform rect;
        private Vector2 lastParent = new Vector2(-1f, -1f);
        private float lastScale = -1f;

        /// <summary>
        /// The HUD's scale, 1 for the size it was laid out at. The HUD options'
        /// scale writes this. The frame grows by the inverse, so a scaled HUD
        /// still sits on the same margins.
        /// </summary>
        public float Scale { get; set; } = 1f;

        /// <summary>
        /// The frame's size in canvas units for a canvas of this size: the
        /// canvas clamped to the aspect, less the margin on each side.
        /// </summary>
        public static Vector2 SizeFor(Vector2 canvas, float maxAspect, float margin)
        {
            float width = canvas.x;
            if (maxAspect > 0f && canvas.y > 0f)
            {
                width = Mathf.Min(canvas.x, canvas.y * maxAspect);
            }

            float keep = 1f - 2f * Mathf.Clamp01(margin);
            return new Vector2(width * keep, canvas.y * keep);
        }

        [SerializeField]
        [Tooltip("Takes the HUD options' opacity. Optional.")]
        private CanvasGroup group;

        [SerializeField]
        [Tooltip("Follow the HUD options' scale and opacity. Off for tests and tools.")]
        private bool followSettings = true;

        private void OnEnable()
        {
            rect = (RectTransform)transform;
            lastParent = new Vector2(-1f, -1f);

            if (followSettings)
            {
                HudSettings.Changed += ReadSettings;
                ReadSettings();
            }

            Apply();
        }

        private void OnDisable()
        {
            HudSettings.Changed -= ReadSettings;
        }

        /// <summary>The HUD options' scale and opacity, read when they change.</summary>
        private void ReadSettings()
        {
            Scale = HudSettings.Scale;
            if (group != null)
            {
                group.alpha = HudSettings.Opacity;
            }
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            RectTransform parent = transform.parent as RectTransform;
            if (parent == null || rect == null)
            {
                return;
            }

            Vector2 size = parent.rect.size;
            float scale = Mathf.Max(0.1f, Scale);
            if (size == lastParent && Mathf.Approximately(scale, lastScale))
            {
                return;
            }

            lastParent = size;
            lastScale = scale;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.sizeDelta = SizeFor(size, maxAspect, margin) / scale;
        }
    }
}
