using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathMenu : MonoBehaviour
{

    public GameObject deathMenuUI;
    void Update()
    {

    }

    public void ShowDeathMenu(){
        deathMenuUI.SetActive(true);
        Time.timeScale = 0f;
    }
}
