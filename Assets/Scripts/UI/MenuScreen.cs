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
        [SerializeField]
        [Tooltip("The screen this one was opened from. Empty means this is the first screen, and backing out of it ends the flow.")]
        private MenuScreen previous;

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

        /// <summary>
        /// The screen currently on show, or null if none is.
        ///
        /// At most one can be open: <see cref="OnEnable"/> closes the others.
        /// </summary>
        public static MenuScreen Open()
        {
            MenuScreen[] showing = FindObjectsByType<MenuScreen>(FindObjectsInactive.Exclude);
            return showing.Length == 0 ? null : showing[0];
        }

        /// <summary>
        /// Steps back one level, opening whichever screen this one was reached
        /// from. False when there is nowhere to go, which is the caller's cue that
        /// backing out again should end the flow instead.
        ///
        /// Shares the <see cref="previous"/> link with the Back button rather than
        /// tracking its own history, so the key and the button cannot disagree
        /// about where "back" is.
        /// </summary>
        public bool Back()
        {
            if (previous == null)
            {
                return false;
            }

            // Show closes this one on the way, being a sibling.
            previous.Show();
            return true;
        }

        /// <summary>
        /// Closes every screen, wherever the player got to.
        ///
        /// For anything that ends the whole flow rather than stepping back one
        /// level - resuming from pause, most of all. Written as a sweep rather
        /// than as a list of screens to hide because that list is what rots: the
        /// pause menu knew about the options hub and nothing else, so adding the
        /// audio, display and graphics screens left Esc resuming the game with one
        /// of them still on screen. A sweep cannot fall behind the screens that
        /// exist.
        /// </summary>
        public static void CloseAll()
        {
            MenuScreen[] screens = FindObjectsByType<MenuScreen>(FindObjectsInactive.Include);

            foreach (MenuScreen screen in screens)
            {
                if (screen.gameObject.activeSelf)
                {
                    screen.gameObject.SetActive(false);
                }
            }
        }
    }
}
