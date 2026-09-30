using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The option screens as tabs. The shoulders, or Q and E, move to the tab
    /// beside this one in place, and the strip across the top can be clicked.
    ///
    /// Until 30 September 2026 Options was a hub: a screen of three buttons,
    /// each opening a screen of settings, with Back to return to the hub. Going
    /// from Audio to Graphics on a pad took Back, down twice and A. Ian: "there
    /// should be tabs not whole new windows".
    ///
    /// Each tab is still its own <see cref="MenuScreen"/>, with its own layout
    /// and its own remembered focus. They share a panel size and a strip, so
    /// switching reads as turning a page rather than opening a window, and all
    /// of them step straight back to where Options was opened from.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Options Tabs")]
    [RequireComponent(typeof(MenuScreen))]
    [DisallowMultipleComponent]
    public sealed class OptionsTabs : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Every tab in strip order, this one included.")]
        private MenuScreen[] tabs;

        /// <summary>
        /// The tab the player was last on, so Options reopens there. Shared by
        /// both scenes' tabs, which run in the same order.
        /// </summary>
        private static int remembered;

        /// <summary>
        /// The frame this tab opened on. The press that opened it is still down
        /// that frame, and a tab enabled during Update can get its own Update the
        /// same frame, so without this one press of RB could turn two tabs.
        /// </summary>
        private int openedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            remembered = 0;
        }

        private int Index => tabs == null ? -1 : System.Array.IndexOf(tabs, GetComponent<MenuScreen>());

        private void OnEnable()
        {
            openedFrame = Time.frameCount;

            int index = Index;
            if (index >= 0)
            {
                remembered = index;
            }
        }

        private void Update()
        {
            int step = GameInput.MenuTabPressed;
            if (step != 0 && Time.frameCount != openedFrame)
            {
                Turn(step);
            }
        }

        /// <summary>
        /// Opens the tab <paramref name="step"/> places along, wrapping at the
        /// ends. Silent: the control focus lands on in the new tab gives the
        /// cue, as moving onto it does anywhere else.
        /// </summary>
        public void Turn(int step)
        {
            int index = Index;
            if (index < 0 || tabs.Length < 2)
            {
                return;
            }

            int next = ((index + step) % tabs.Length + tabs.Length) % tabs.Length;
            if (tabs[next] != null)
            {
                tabs[next].Show();
            }
        }

        /// <summary>
        /// Opens whichever tab the player was last on. For the Options buttons,
        /// wired to the first tab's copy of this; any copy does the same.
        /// </summary>
        public void ShowRemembered()
        {
            if (tabs == null || tabs.Length == 0)
            {
                return;
            }

            MenuScreen tab = tabs[Mathf.Clamp(remembered, 0, tabs.Length - 1)];
            if (tab != null)
            {
                tab.Show();
            }
        }
    }
}
