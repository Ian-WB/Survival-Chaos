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

    /// <summary>
    /// The level the label currently reads. -1 forces the first write.
    ///
    /// The label used to be rebuilt every frame: a string concat plus a
    /// TextMeshPro geometry rebuild, for a value that changes maybe eight times
    /// in a run. Both are garbage, and garbage is what the frame time graph
    /// shows as spikes.
    /// </summary>
    private int shownLevel = -1;

    void Start()
    {
        if (xpBar != null)
        {
            xpBar.fillAmount = 0;
        }

        RefreshLevelText();
    }

    void Update(){
        if (xpBar != null)
        {
            // Guarded because thisMaxExp is 0 until Player.Start sets it. Unity
            // runs every Start before any Update so it is set in practice, but a
            // fillAmount of NaN is a miserable thing to trace back to a divide.
            xpBar.fillAmount = thisMaxExp > 0 ? thisCurrentExp / thisMaxExp : 0f;
        }

        RefreshLevelText();
    }

    /// <summary>Writes the label only when the level has actually changed.</summary>
    private void RefreshLevelText(){
        if (expText == null || player == null || player.currentLevel == shownLevel){
            return;
        }

        shownLevel = player.currentLevel;
        expText.text = "Level " + shownLevel;
    }

    public void setMaxExp(int maxExp){
        thisMaxExp = maxExp;
    }
    public void setCurrentExp(int currentExp){
        thisCurrentExp = currentExp;
    }
}
