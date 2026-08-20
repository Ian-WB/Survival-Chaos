using UnityEngine;
using SurvivalChaos;

public class DeathMenu : MonoBehaviour
{

    [SerializeField]
    private GameObject deathMenuUI;

    public void ShowDeathMenu(){
        if (deathMenuUI != null){
            deathMenuUI.SetActive(true);
        }

        // Before time stops, so nothing can pause into the gap. PauseMenu reads
        // this and refuses to open: without it, Esc put the pause screen over
        // the death screen and resuming from there restarted a lost run.
        RunOutcome.ReportRunEnded();
        Time.timeScale = 0f;
    }
}
