using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;
    public GameObject pauseMenuUI;
    public GameObject optionsUI;
    void Update()
    {
        if (!GameInput.PausePressed){
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
