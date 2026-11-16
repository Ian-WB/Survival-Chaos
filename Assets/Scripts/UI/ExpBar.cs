using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SurvivalChaos
{
    public class ExpBar : MonoBehaviour
    {
        [SerializeField]
        private int thisMaxExp;
        [SerializeField]
        private float thisCurrentExp;
        [SerializeField]
        private Image xpBar;
        [SerializeField]
        private TextMeshProUGUI expText;
        [SerializeField]
        private Player player;

        [Header("Level-up")]
        [SerializeField]
        [Tooltip("The colour the level label lights to as a level is reached, fading back to its " +
                 "own. The experience numbers' gold, which the bar is drawn in too.")]
        private Color levelUpColour = new Color(1f, 0.78f, 0.25f, 1f);

        [SerializeField]
        [Range(1f, 2f)]
        [Tooltip("How large the level label swells as a level is reached.")]
        private float levelUpScale = 1.3f;

        [SerializeField]
        [Range(0.1f, 2f)]
        [Tooltip("Seconds the label takes to settle back after a level-up.")]
        private float levelUpSeconds = 0.6f;

        /// <summary>
        /// The level the label currently reads. -1 forces the first write.
        ///
        /// The label used to be rebuilt every frame: a string concat plus a
        /// TextMeshPro geometry rebuild, for a value that changes maybe eight times
        /// in a run. Both are garbage, and garbage is what the frame time graph
        /// shows as spikes.
        /// </summary>
        private int shownLevel = -1;

        private HoloBar holo;
        private Color labelColour;
        private Coroutine punching;

        void Awake()
        {
            holo = xpBar != null ? xpBar.GetComponent<HoloBar>() : null;
            labelColour = expText != null ? expText.color : Color.white;
        }

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

        /// <summary>
        /// Writes the label only when the level has actually changed, and marks
        /// a level reached: the bar flashes and the label swells and settles.
        ///
        /// The level-up itself had only a sound until 16 November 2026. Since
        /// 9 November it puts an offer out on the ring rather than granting
        /// anything, and the banner waits for a pick to be flown into, so
        /// nothing on screen said a level had come.
        /// </summary>
        private void RefreshLevelText(){
            if (expText == null || player == null || player.currentLevel == shownLevel){
                return;
            }

            bool reached = shownLevel > 0 && player.currentLevel > shownLevel;

            shownLevel = player.currentLevel;
            expText.text = "Level " + shownLevel;

            if (reached)
            {
                MarkLevelUp();
            }
        }

        private void MarkLevelUp()
        {
            if (holo != null)
            {
                holo.Flash();
            }

            if (punching != null)
            {
                StopCoroutine(punching);
            }

            punching = StartCoroutine(Punch());
        }

        private IEnumerator Punch()
        {
            Transform label = expText.transform;
            float elapsed = 0f;

            while (elapsed < levelUpSeconds)
            {
                // Unscaled: Slow Mo should not stretch it, and the endings stop
                // time with the label mid-swell otherwise.
                elapsed += Time.unscaledDeltaTime;
                float settle = Mathf.Clamp01(elapsed / levelUpSeconds);
                float left = (1f - settle) * (1f - settle);

                label.localScale = Vector3.one * Mathf.Lerp(1f, levelUpScale, left);
                expText.color = Color.Lerp(labelColour, levelUpColour, left);
                yield return null;
            }

            label.localScale = Vector3.one;
            expText.color = labelColour;
            punching = null;
        }

        private void OnDisable()
        {
            if (punching != null)
            {
                StopCoroutine(punching);
                punching = null;
            }

            if (expText != null)
            {
                expText.transform.localScale = Vector3.one;
                expText.color = labelColour;
            }
        }

        public void setMaxExp(int maxExp){
            thisMaxExp = maxExp;
        }
        public void setCurrentExp(int currentExp){
            thisCurrentExp = currentExp;
        }
    }
}
