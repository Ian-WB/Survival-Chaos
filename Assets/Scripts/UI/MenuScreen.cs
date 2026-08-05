using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// One full-screen menu. Showing one hides the others.
    ///
    /// Each screen draws its own scrim, so two visible at once means two dimming
    /// layers and two panels stacked on top of each other - which is what
    /// happened when the options screen appeared over the pause screen.
    ///
    /// The rule is enforced in OnEnable rather than by the buttons, because the
    /// buttons are not the only thing that opens a screen: DeathMenu, PauseMenu
    /// and VictoryMenu all call SetActive directly. Anything that shows a screen,
    /// by any route, now closes the rest.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Menu Screen")]
    [DisallowMultipleComponent]
    public sealed class MenuScreen : MonoBehaviour
    {
        private void OnEnable()
        {
            Transform parent = transform.parent;
            if (parent == null)
            {
                return;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform sibling = parent.GetChild(i);

                if (sibling == transform || !sibling.gameObject.activeSelf)
                {
                    continue;
                }

                // Only other screens. Anything else parked alongside them - the
                // tutorial prompt, for instance - is left alone.
                if (sibling.TryGetComponent(out MenuScreen _))
                {
                    sibling.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Opens this screen, closing whichever one is open. For buttons.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>Closes this screen without opening another.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
