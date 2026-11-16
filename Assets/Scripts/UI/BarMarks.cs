using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// Thin upright marks across a HUD bar at given fractions of its length.
    ///
    /// Made for the boss bar, which is one pool of health for three acts: the
    /// emplacements spend the first share of it, the hull the rest, and the
    /// last act starts near the bottom. A mark where each act ends shows the
    /// fight's shape without a word on screen. Words were tried in the boss
    /// fight and taken out on 8 September 2026, which is why these are marks
    /// and the label stays the boss's name.
    ///
    /// The marks are plain Images, children of the bar, so they ride along with
    /// anything that moves or hides it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BarMarks : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Width of each mark, in canvas units.")]
        private float width = 3f;

        [SerializeField]
        [Tooltip("How far each mark stands past the bar's top and bottom edges, in canvas units.")]
        private float overhang = 6f;

        [SerializeField]
        [Tooltip("Where the bar's fill starts and ends inside its frame, in canvas units: the " +
                 "shader's fill inset. Keeps a mark at 0.5 on the middle of the fill rather than " +
                 "of the frame.")]
        private float inset = 4f;

        [SerializeField]
        [Tooltip("Colour of the marks. The HUD's line colour.")]
        private Color colour = new Color(0.55f, 0.95f, 1.00f, 1.00f);

        private readonly List<RectTransform> marks = new List<RectTransform>();

        /// <summary>
        /// Puts one mark at each fraction, 0 to 1 along the bar, and clears any
        /// from before. Fractions at or beyond either end are left out: a mark
        /// on the frame says nothing.
        /// </summary>
        public void Show(IReadOnlyList<float> fractions)
        {
            RectTransform bar = transform as RectTransform;
            if (bar == null)
            {
                return;
            }

            int used = 0;

            if (fractions != null)
            {
                for (int i = 0; i < fractions.Count; i++)
                {
                    float fraction = fractions[i];
                    if (fraction <= 0f || fraction >= 1f)
                    {
                        continue;
                    }

                    RectTransform mark = Mark(used++);
                    mark.gameObject.SetActive(true);
                    mark.anchorMin = new Vector2(0f, 0f);
                    mark.anchorMax = new Vector2(0f, 1f);
                    mark.pivot = new Vector2(0.5f, 0.5f);
                    mark.sizeDelta = new Vector2(width, overhang * 2f);
                    mark.anchoredPosition = new Vector2(Along(bar.rect.width, fraction), 0f);
                }
            }

            for (int i = used; i < marks.Count; i++)
            {
                marks[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Where a fraction falls along a bar of the given width, inside its inset.</summary>
        public static float AlongBar(float barWidth, float inset, float fraction)
        {
            float track = Mathf.Max(0f, barWidth - inset * 2f);
            return inset + track * Mathf.Clamp01(fraction);
        }

        private float Along(float barWidth, float fraction)
        {
            return AlongBar(barWidth, inset, fraction);
        }

        private RectTransform Mark(int index)
        {
            while (marks.Count <= index)
            {
                var holder = new GameObject("Mark " + marks.Count, typeof(RectTransform));
                holder.transform.SetParent(transform, worldPositionStays: false);

                Image image = holder.AddComponent<Image>();
                image.color = colour;
                image.raycastTarget = false;

                marks.Add((RectTransform)holder.transform);
            }

            return marks[index];
        }
    }
}
