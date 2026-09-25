using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

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
    ///
    /// A screen also decides where keyboard and pad focus lands. Unity's UI sends
    /// the arrows, the stick and Enter/A to whatever is selected, and until 25
    /// September 2026 nothing selected anything - so without a mouse they went
    /// nowhere. The pad could fly the ship and pause it, and then could not pick
    /// Restart after dying.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Menu Screen")]
    [DisallowMultipleComponent]
    public sealed class MenuScreen : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The screen this one was opened from. Empty means this is the first screen, and backing out of it ends the flow.")]
        private MenuScreen previous;

        [SerializeField]
        [Tooltip("Where keyboard and pad focus lands when this screen opens. Empty means the first live control on the screen, in hierarchy order.")]
        private Selectable firstSelected;

        /// <summary>Stick or arrow travel that counts as asking for focus back.</summary>
        private const float NavigateThreshold = 0.5f;

        /// <summary>
        /// The control this screen last had focus on, so that backing out to it
        /// lands where the player left rather than back at the top.
        /// </summary>
        private GameObject lastSelected;

        private bool focusPending;
        private bool returning;

        private void Awake()
        {
            LetSlidersTakeLeftAndRight();
        }

        private void OnEnable()
        {
            bool fromAnotherScreen = false;

            Transform parent = transform.parent;
            if (parent != null)
            {
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
                        fromAnotherScreen = true;
                    }
                }
            }

            // Reached from another screen - deeper or back out - it resumes where
            // it was left. Opened from play, it starts at the top: every pause
            // begins on Resume, whatever the last one ended on.
            returning = fromAnotherScreen;
            focusPending = true;
        }

        private void OnDisable()
        {
            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;

            if (selected != null && selected.transform.IsChildOf(transform))
            {
                lastSelected = selected;
            }
        }

        /// <summary>
        /// Focus is placed here rather than in OnEnable. A screen's OnEnable runs
        /// before its children's, and selecting a control whose own OnEnable has
        /// not run yet lights it up only for that OnEnable to reset it - or, on
        /// its first showing, calls into a highlight whose Awake has not run.
        /// LateUpdate is after all of that, and after the EventSystem's own
        /// Update, so a screen opened by a click still gets focus that frame.
        /// </summary>
        private void LateUpdate()
        {
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                return;
            }

            if (focusPending)
            {
                focusPending = false;
                Focus(events, returning ? lastSelected : null);
                return;
            }

            GameObject selected = events.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy)
            {
                if (selected.transform.IsChildOf(transform))
                {
                    lastSelected = selected;
                }

                return;
            }

            // Clicking empty space clears the selection. That is left alone for
            // the mouse; a direction on the keys or the stick brings focus back
            // where it was. A direction only - bringing it back on Submit would
            // press whatever came back, which is not what anyone meant.
            if (NavigatePressed(events))
            {
                Focus(events, lastSelected);
            }
        }

        private void Focus(EventSystem events, GameObject preferred)
        {
            GameObject target = Usable(preferred) ? preferred : DefaultControl();
            if (target == null)
            {
                return;
            }

            // Selecting what the EventSystem already holds sends no event, and a
            // screen reopened on the control it closed on - Resume, every pause
            // after the first - would come back with it unlit, because its
            // highlight resets on enable. Clearing first makes it an event again.
            if (events.currentSelectedGameObject == target)
            {
                events.SetSelectedGameObject(null);
            }

            events.SetSelectedGameObject(target);
            lastSelected = target;
        }

        private GameObject DefaultControl()
        {
            if (firstSelected != null && Usable(firstSelected.gameObject))
            {
                return firstSelected.gameObject;
            }

            foreach (Selectable selectable in GetComponentsInChildren<Selectable>())
            {
                if (selectable.IsInteractable() && selectable.navigation.mode != Navigation.Mode.None)
                {
                    return selectable.gameObject;
                }
            }

            return null;
        }

        private bool Usable(GameObject control)
        {
            return control != null
                && control.activeInHierarchy
                && control.transform.IsChildOf(transform)
                && control.TryGetComponent(out Selectable selectable)
                && selectable.IsInteractable();
        }

        private static bool NavigatePressed(EventSystem events)
        {
#if ENABLE_INPUT_SYSTEM
            if (events.currentInputModule is InputSystemUIInputModule module
                && module.move != null && module.move.action != null)
            {
                return module.move.action.ReadValue<Vector2>().sqrMagnitude
                    >= NavigateThreshold * NavigateThreshold;
            }
#endif
            return false;
        }

        /// <summary>
        /// Hands left and right to the sliders on this screen.
        ///
        /// A horizontal Slider only takes left and right for its value when
        /// nothing is selectable that way, and automatic navigation nearly always
        /// finds something. The sharpness bar sits in the Display screen's right
        /// column with a column of arrows to its left, so on a pad left walked
        /// off it instead of lowering it. Vertical navigation keeps up and down
        /// moving between rows and gives the sideways presses to the value.
        /// </summary>
        private void LetSlidersTakeLeftAndRight()
        {
            foreach (Slider slider in GetComponentsInChildren<Slider>(true))
            {
                bool horizontal = slider.direction == Slider.Direction.LeftToRight
                    || slider.direction == Slider.Direction.RightToLeft;

                if (!horizontal || slider.navigation.mode != Navigation.Mode.Automatic)
                {
                    continue;
                }

                Navigation navigation = slider.navigation;
                navigation.mode = Navigation.Mode.Vertical;
                slider.navigation = navigation;
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
