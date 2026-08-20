using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

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
        GameIsPaused = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnEnterPlayMode()
    {
        GameIsPaused = false;
    }

    void Update()
    {
        if (!GameInput.PausePressed){
            return;
        }

        // The run is over and a death or victory screen is up. Pausing on top of
        // it would let the player resume out of an ending they have already
        // reached, and carry on playing at zero health.
        if (RunOutcome.RunEnded){
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

        Time.timeScale = 1f;
        GameIsPaused = false;
    }

    void Pause(){
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        GameIsPaused = true;
    }
}
