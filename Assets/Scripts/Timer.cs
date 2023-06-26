using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Timer : MonoBehaviour
{
    public BossHpBar bossHpBar;
    [SerializeField] private float gameTime = 60;
    public Slider timerSlider;
    public GameObject timerBar;
    public float timeValue = 0;
    void Start()
    {

        if (timerSlider != null)
        {
            timerSlider.maxValue = gameTime;
        }

        timeValue = 0;
}

    //Update is called once per frame
    void Update()
    {
        if (timerSlider != null)
        {
            if(gameTime > timeValue){
                timeValue += Time.deltaTime;
                timerSlider.value = timeValue;
            } else {
                bossHpBar.showHpBar();
                timerBar.SetActive(false);
            }
        }
        
    }
    // void Update()
    // {
    //     if(gameTime > timeValue){
    //         timeValue += Time.deltaTime;
    //         timerSlider.value = timeValue;
    //     } else {
    //         bossHpBar.showHpBar();
    //         timerBar.SetActive(false);
    //     }
    // }
}