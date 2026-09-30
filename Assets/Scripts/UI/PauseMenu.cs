using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    public class PauseMenu : MonoBehaviour
    {
        public static bool GameIsPaused = false;
        [SerializeField]
        private GameObject pauseMenuUI;
        [SerializeField]
        private GameObject optionsUI;

        /// <summary>
        /// A fresh scene is a fresh run, so the flag starts down.
        ///
        /// It is static, and static state survives both a scene load and - with
        /// domain reload disabled - play mode itself. Quitting to the menu while
        /// paused used to carry a true value into the next run, where the first Esc
        /// press took the resume branch and appeared to do nothing.
        /// </summary>
        void Awake()
        {
            RunTime.ResetForNewRun();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            GameIsPaused = false;
            active = null;
        }

        private static PauseMenu active;

        /// <summary>
        /// True while a pause menu is in the scene to answer the back keys.
        ///
        /// Esc, Start and the pad's B step a menu back one screen, and at the
        /// root of the pause menu's screens they mean resume, which only this can
        /// do. So where one exists it answers them for every screen, and
        /// MenuScreen leaves them alone rather than stepping back a second time
        /// on the same press. The title scene has none, and its screens answer
        /// the keys themselves.
        /// </summary>
        public static bool InScene => active != null;

        void OnEnable()
        {
            active = this;
        }

        void OnDisable()
        {
            if (active == this){
                active = null;
            }

            // Leaving the run, by any route. The title screen is all pointer.
            ShowCursor(true);
        }

        /// <summary>
        /// Losing the window mid-run opens the pause menu.
        ///
        /// Builds switch "run in background" off (RunInBackgroundOff), so an
        /// alt-tab freezes the game where it stands, and coming back used to
        /// drop the player into the same frame of the fight with no warning.
        /// Now they come back to the pause screen and resume when ready.
        ///
        /// Only where the game really does stop without focus. The editor runs
        /// in the background, and pausing there on every click into another
        /// window would get in the way of working on the game. Not on a death
        /// or victory card, or behind the loading screen, which have nothing to
        /// pause.
        /// </summary>
        void OnApplicationFocus(bool focused)
        {
            if (focused || Application.runInBackground){
                return;
            }

            if (GameIsPaused || RunOutcome.Decided || LoadingScreen.Busy){
                return;
            }

            Pause();
        }

        /// <summary>
        /// The pointer is hidden while the run is being played, since nothing
        /// in a run uses it, and shown whenever a menu or card is up.
        /// LateUpdate, so a pause or an ending this frame has already landed.
        /// </summary>
        void LateUpdate()
        {
            bool menu = GameIsPaused || RunOutcome.RunEnded || LoadingScreen.Busy;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION || SURVIVAL_CHAOS_DEBUG_MENU
            menu |= DebugMenu.Showing;
#endif
            ShowCursor(menu);
        }

        private static void ShowCursor(bool visible)
        {
            if (Cursor.visible != visible){
                Cursor.visible = visible;
            }
        }

        void Update()
        {
            // B backs out as Esc and Start do, but only from a menu. In play it
            // is a free button, and one that paused the game would be a surprise.
            bool back = GameIsPaused && GameInput.BackPressed;

            if (!GameInput.PausePressed && !back){
                return;
            }

            // The run is over and a death or victory screen is up. Pausing on top of
            // it would let the player resume out of an ending they have already
            // reached, and carry on playing at zero health. The beat before the
            // death card counts too: the run is already lost there.
            if (RunOutcome.Decided){
                return;
            }

            if (!GameIsPaused){
                Pause();
                return;
            }

            // Esc retraces the way in, one screen per press, and only resumes once
            // there is nothing left to back out of. Otherwise a player three screens
            // deep in options loses their place to a keypress meant to undo one step.
            MenuScreen open = MenuScreen.Open();
            if (open != null && open.Back()){
                return;
            }

            Resume();
        }

        public void Resume(){
            if (RunOutcome.RunEnded)
            {
                RunTime.Apply();
                return;
            }

            // Every screen, not just the two this component happens to hold
            // references to - the player could be several screens deep in options.
            // Closed before time restarts, so play never resumes under a menu.
            MenuScreen.CloseAll();

            if (pauseMenuUI != null){
                pauseMenuUI.SetActive(false);
            }

            if (optionsUI != null){
                optionsUI.SetActive(false);
            }

            GameIsPaused = false;
            RunTime.Apply();
        }

        void Pause(){
            if (RunOutcome.Decided) { return; }
            if (pauseMenuUI != null) { pauseMenuUI.SetActive(true); }
            GameIsPaused = true;
            RunTime.Apply();
        }
    }
}
