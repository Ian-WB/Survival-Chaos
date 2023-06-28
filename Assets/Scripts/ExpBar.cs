using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExpBar : MonoBehaviour
{
    public int thisMaxExp;
    public float thisCurrentExp;
    public Image xpBar;
    public TextMeshProUGUI expText;
    public Player player;
    // Start is called before the first frame update
    void Start()
    {
        xpBar.fillAmount = 0;
        
    }

    void Update(){
        xpBar.fillAmount = thisCurrentExp / thisMaxExp;
        expText.text = "Level " + player.currentLevel;
    }

    public void setMaxExp(int maxExp){
        thisMaxExp = maxExp;
    }
    public void setCurrentExp(int currentExp){
        thisCurrentExp = currentExp;
    }
}
