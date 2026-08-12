using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Ends the run when the boss dies: shows the victory panel and stops time,
    /// mirroring how DeathMenu ends a losing run.
    /// </summary>
    public sealed class VictoryMenu : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Panel shown when the boss is defeated. Usually starts inactive.")]
        private GameObject victoryMenuUI;

        private void OnEnable()
        {
            RunOutcome.BossDefeated += Show;
        }

        private void OnDisable()
        {
            RunOutcome.BossDefeated -= Show;
        }

        public void Show()
        {
            if (victoryMenuUI != null)
            {
                victoryMenuUI.SetActive(true);
            }
            else
            {
                Debug.LogWarning(
                    "VictoryMenu has no panel assigned, so the run ends with nothing on screen.", this);
            }

            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.Victory);
            }

            // Stop the run regardless of whether the panel is wired, so "the
            // boss died" always actually ends the game. Flagged before time
            // stops so the pause menu cannot open into the gap - the same route
            // that let a player resume out of the death screen.
            RunOutcome.ReportRunEnded();
            Time.timeScale = 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // DeathMenu can live on the panel it shows, because Player calls it
            // through a direct reference. This one listens for an event, so it
            // has to be on an object that is actually active - otherwise
            // OnEnable never runs and the subscription never happens.
            if (victoryMenuUI == gameObject)
            {
                Debug.LogError(
                    "VictoryMenu is pointing at its own GameObject. Put this component on an " +
                    "active object (the Canvas works) and point it at the inactive victory panel.",
                    this);
            }
            else if (!gameObject.activeInHierarchy && victoryMenuUI != null)
            {
                Debug.LogWarning(
                    "VictoryMenu is on an inactive object, so it will never subscribe and the " +
                    "victory screen will never show. Move it to an active object.",
                    this);
            }
        }
#endif
    }
}
