using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SurvivalChaos
{
    /// <summary>
    /// Scrolls the credits inside their window.
    ///
    /// The credits are longer than the panel since they began naming the
    /// music, the sounds, the fonts and the upscalers as well as the team. Up
    /// and down on the keys, the stick or the d-pad move them, and so does the
    /// mouse wheel. The only thing on the screen to select is Back, so the
    /// vertical axis is free for this.
    ///
    /// The content is a child of this object, which carries the mask; the
    /// content's pivot is its top edge, so its position is how far it has been
    /// scrolled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreditsRoll : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The text that moves. Anchored and pivoted at its top edge.")]
        private RectTransform content;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Canvas units a second at full stick.")]
        private float speed = 420f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Canvas units for one notch of the mouse wheel.")]
        private float wheelStep = 60f;

        // Room under the last line, so it is not cut by the window's edge.
        private const float Padding = 8f;

        private float offset;

        private void OnEnable()
        {
            Measure();

            // Every visit starts at the top: the team comes first.
            offset = 0f;
            Place();
        }

        private void Update()
        {
            if (content == null)
            {
                return;
            }

            // Unscaled: the title screen runs at full speed, but nothing here
            // should depend on that staying true.
            float move = -GameInput.Vertical * speed * Time.unscaledDeltaTime;

#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel != 0f)
                {
                    move -= Mathf.Sign(wheel) * wheelStep;
                }
            }
#endif

            if (move == 0f)
            {
                return;
            }

            offset += move;
            Place();
        }

        /// <summary>
        /// Makes the content as tall as its text. Done here rather than when
        /// the screen is built, because a text on an inactive object cannot be
        /// measured, and the screen is inactive until it is opened.
        /// </summary>
        private void Measure()
        {
            if (content == null || !content.TryGetComponent(out TMP_Text text))
            {
                return;
            }

            text.ForceMeshUpdate();
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Ceil(text.preferredHeight) + Padding);
        }

        private void Place()
        {
            if (content == null)
            {
                return;
            }

            RectTransform window = (RectTransform)transform;
            offset = Clamp(offset, content.rect.height, window.rect.height);

            Vector2 position = content.anchoredPosition;
            position.y = offset;
            content.anchoredPosition = position;
        }

        /// <summary>
        /// How far the content may be scrolled: from its top at the window's
        /// top, to its bottom at the window's bottom. Content shorter than the
        /// window does not move at all.
        /// </summary>
        public static float Clamp(float offset, float contentHeight, float windowHeight)
        {
            float furthest = Mathf.Max(0f, contentHeight - windowHeight);
            return Mathf.Clamp(offset, 0f, furthest);
        }
    }
}
