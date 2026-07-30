using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void Jogar(){
        ResumeTime();
        SceneManager.LoadScene("Game");
    }

    public void Sair(){
        Application.Quit();
    }

    public void MenuPrincipal(){
        ResumeTime();
        SceneManager.LoadScene("Menu");
    }

    /// <summary>
    /// Time.timeScale survives a scene load, and the death, pause and victory
    /// screens all set it to 0. Without this, restarting from one of those
    /// menus loaded a frozen game.
    /// </summary>
    private static void ResumeTime(){
        Time.timeScale = 1f;
    }
}
