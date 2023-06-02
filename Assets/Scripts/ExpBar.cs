using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExpBar : MonoBehaviour
{
    public int thisMaxExp;
    public float thisCurrentExp;
    public Image xpBar;
    // Start is called before the first frame update
    void Start()
    {
        xpBar.fillAmount = 0;
        
    }

    void Update(){
        xpBar.fillAmount = thisCurrentExp / thisMaxExp;
    }

    public void setMaxExp(int maxExp){
        thisMaxExp = maxExp;
    }
    public void setCurrentExp(int currentExp){
        thisCurrentExp = currentExp;
    }
}
