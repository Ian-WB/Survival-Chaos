using TMPro;
using UnityEngine;

namespace SurvivalChaos
{
    public class Tutorial : MonoBehaviour
    {
        [SerializeField]
        private GameObject shiftTutorial;

        private TMP_Text prompt;
        private PlayerDash dash;
        private bool reversed;
        private float shownAt;

        void Start()
        {
            if (shiftTutorial == null)
            {
                Debug.LogWarning("Tutorial has no prompt assigned, so nothing will be shown.", this);
                return;
            }

            prompt = shiftTutorial.GetComponentInChildren<TMP_Text>(true);
            dash = FindAnyObjectByType<PlayerDash>();
            shownAt = Time.time;
            shiftTutorial.SetActive(true);
        }

        private void Update()
        {
            if (shiftTutorial == null) { return; }
            if (RunOutcome.RunEnded)
            {
                shiftTutorial.SetActive(false);
                enabled = false;
                return;
            }

            shiftTutorial.SetActive(!PauseMenu.GameIsPaused);
            if (PauseMenu.GameIsPaused || Time.timeScale <= 0f) { return; }
            reversed |= GameInput.ToggleDirectionReleased;
            bool dashed = dash != null && dash.HasDashed;
            if (reversed && dashed && Time.time - shownAt >= 3f)
            {
                shiftTutorial.SetActive(false);
                enabled = false;
                return;
            }

            if (prompt != null)
            {
                string move = GameInput.UsingGamepad ? "Left stick / D-pad" : "WASD / Arrows";
                string flip = GameInput.UsingGamepad ? "LB / L1" : "Shift";
                string text = move + " - Move\n"
                    + flip + " - Reverse fire independently" + (reversed ? " [done]" : "") + "\n"
                    + GameInput.DashControlLabel + " - Dash / brief invulnerability" + (dashed ? " [done]" : "");
                if (prompt.text != text) { prompt.text = text; }
            }
        }
    }
}
