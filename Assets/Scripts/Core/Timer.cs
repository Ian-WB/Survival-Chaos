using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Counts the run out, then hands over to the boss.
///
/// The handover used to live inside <c>if (timerSlider != null)</c> along with
/// everything else, so an unassigned slider meant the run simply never
/// progressed, with nothing said about why. It also ran every frame for the rest
/// of the run once the time was up, re-showing an already-shown bar. Both are
/// now separate: the slider drives the display, the elapsed time drives the game
/// state, and the handover happens once.
/// </summary>
public class Timer : MonoBehaviour
{
    public BossHpBar bossHpBar;
    [SerializeField] private float gameTime = 60;
    public Slider timerSlider;
    public GameObject timerBar;
    public float timeValue = 0;

    /// <summary>True once the boss phase has been started. Latches the handover.</summary>
    private bool handedOver;

    void Start()
    {
        if (timerSlider != null)
        {
            timerSlider.maxValue = gameTime;
        }

        timeValue = 0;
        handedOver = false;
    }

    void Update()
    {
        if (handedOver)
        {
            return;
        }

        timeValue += Time.deltaTime;

        // Display only. A missing slider costs the countdown bar, not the run.
        if (timerSlider != null)
        {
            timerSlider.value = timeValue;
        }

        if (timeValue < gameTime)
        {
            return;
        }

        handedOver = true;
        HandOverToBoss();
    }

    private void HandOverToBoss()
    {
        if (bossHpBar != null)
        {
            bossHpBar.showHpBar();
        }
        else
        {
            Debug.LogWarning(
                "Timer has no Boss Hp Bar assigned, so the boss fight starts with no health bar.",
                this);
        }

        if (timerBar != null)
        {
            timerBar.SetActive(false);
        }
    }
}
