using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void Jogar(){
        SceneManager.LoadScene("Game");
    }

    public void Sair(){
        Application.Quit();
    }
}
