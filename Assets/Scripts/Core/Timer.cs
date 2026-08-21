using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
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
        [SerializeField]
        private BossHpBar bossHpBar;
        [SerializeField]
        [Tooltip("Fallback length of the survival phase, in seconds. Only used when there is " +
                 "no WaveDirector in the scene to say when the boss arrives - the wave asset " +
                 "wins whenever there is one.")]
        private float gameTime = 60;

        /// <summary>
        /// How long the survival phase actually runs for. Resolved once in Start
        /// rather than read from the field, which is only the fallback.
        /// </summary>
        private float runLength;

        [SerializeField]
        private Slider timerSlider;
        [SerializeField]
        private GameObject timerBar;
        [SerializeField]
        private float timeValue= 0;

        /// <summary>True once the boss phase has been started. Latches the handover.</summary>
        private bool handedOver;

        void Start()
        {
            runLength = ResolveRunLength();

            if (timerSlider != null)
            {
                timerSlider.maxValue = runLength;
            }

            timeValue = 0;
            handedOver = false;
        }

        /// <summary>
        /// Asks the wave asset when the boss arrives, and falls back to the
        /// serialized field when there is nothing to ask.
        ///
        /// The boss is spawned by a stream in the wave asset and revealed by this
        /// timer, which is two clocks for one event. Both said 600 and so both
        /// agreed, but nothing was making them agree: moving the boss in the wave
        /// asset left the health bar behind, and moving this left the bar out in
        /// front of an empty arena. Neither is visible while authoring, and both
        /// are visible ten minutes into a run.
        ///
        /// A disagreement is reported rather than silently corrected. The field
        /// stays in the Inspector because a scene without a director still needs
        /// one, and a number sitting there that has quietly stopped meaning
        /// anything is the trap this is closing.
        /// </summary>
        private float ResolveRunLength()
        {
            WaveDirector director = FindAnyObjectByType<WaveDirector>();

            float authored = director != null && director.Wave != null
                ? director.Wave.BossArrivesAt
                : -1f;

            // Negative means the wave has no boss in it at all, which is a
            // legitimate wave rather than a broken one - an endless mode, or a
            // test scene. The field is the only answer available then.
            if (authored < 0f)
            {
                return gameTime;
            }

            if (!Mathf.Approximately(authored, gameTime))
            {
                Debug.LogWarning(
                    "Timer's Game Time is " + gameTime + "s but the wave spawns the boss at "
                    + authored + "s. Going with the wave, so the health bar and the boss "
                    + "arrive together. Update the field to match, or clear the disagreement "
                    + "in the wave asset.",
                    this);
            }

            return authored;
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

            if (timeValue < runLength)
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
}
