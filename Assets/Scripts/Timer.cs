using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Timer : MonoBehaviour
{
    [SerializeField] private float gameTime;
    public Slider timerSlider;
    private bool stopTimer;
    void Start()
    {
        stopTimer=false;
        timerSlider.maxValue = gameTime;
        timerSlider.value=0;
    }

    // Update is called once per frame
    void Update()
    {
        float time = Time.time;
        if (time >= gameTime) {
            stopTimer = true;
        }
        if (stopTimer == false) {
            timerSlider.value = time;
        }
    }
}