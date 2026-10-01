using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// Holds an end card's buttons for a moment: they fade in and take no
    /// press until they are there.
    ///
    /// On a pad, A is the dash and also presses the selected button, and both
    /// cards open with Try Again selected. So an A pressed as the card came up,
    /// which is the reflex when a hit is coming, started a new run before the
    /// card had been read, and a NEW BEST went with it. Added by the two menus
    /// as they show their card, like <see cref="RunSummary"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardGuard : MonoBehaviour
    {
        /// <summary>Real seconds the buttons wait. Real, because the card stops the game's clock.</summary>
        public const float HoldSeconds = 0.6f;

        private CanvasGroup[] groups;
        private float shownAt;
        private bool holding;

        /// <summary>Holds the buttons on <paramref name="card"/>, adding this the first time.</summary>
        public static void Hold(GameObject card)
        {
            if (card == null)
            {
                return;
            }

            if (!card.TryGetComponent(out CardGuard guard))
            {
                guard = card.AddComponent<CardGuard>();
            }

            guard.Begin();
        }

        private void Begin()
        {
            Button[] buttons = GetComponentsInChildren<Button>(includeInactive: true);
            groups = new CanvasGroup[buttons.Length];

            for (int i = 0; i < buttons.Length; i++)
            {
                if (!buttons[i].TryGetComponent(out CanvasGroup group))
                {
                    group = buttons[i].gameObject.AddComponent<CanvasGroup>();
                }

                group.alpha = 0f;
                group.interactable = false;
                groups[i] = group;
            }

            shownAt = Time.unscaledTime;
            holding = true;
            enabled = true;
        }

        private void Update()
        {
            if (!holding)
            {
                enabled = false;
                return;
            }

            float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / HoldSeconds);

            foreach (CanvasGroup group in groups)
            {
                if (group != null)
                {
                    group.alpha = t;
                }
            }

            if (t < 1f)
            {
                return;
            }

            holding = false;

            foreach (CanvasGroup group in groups)
            {
                if (group != null)
                {
                    group.interactable = true;
                }
            }

            // Focus could not land on a button that took no presses, so it is
            // put on the first one now, which is Try Again.
            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            if (events != null && (selected == null || !selected.transform.IsChildOf(transform)))
            {
                Button first = GetComponentInChildren<Button>();
                if (first != null)
                {
                    events.SetSelectedGameObject(first.gameObject);
                }
            }

            enabled = false;
        }
    }
}
