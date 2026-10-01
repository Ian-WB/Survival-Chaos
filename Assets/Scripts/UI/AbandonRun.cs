using TMPro;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The question the pause screen asks before a run is thrown away:
    /// Restart, Main Menu and Quit all come here first, and only Yes carries
    /// them out.
    ///
    /// Until 30 September 2026 one press of Main Menu or Quit ended a run of
    /// up to ten minutes, and Quit closed the game outright. No is selected
    /// when the question opens, and B, Esc or Start back out to the pause
    /// screen as from any other.
    /// </summary>
    [RequireComponent(typeof(MenuScreen))]
    public sealed class AbandonRun : MonoBehaviour
    {
        private enum Choice
        {
            None,
            Restart,
            MainMenu,
            Quit
        }

        [SerializeField]
        [Tooltip("Carries out Restart, Main Menu and Quit once the player says yes.")]
        private MainMenu mainMenu;

        [SerializeField]
        [Tooltip("The line under the question that says what Yes will do.")]
        private TMP_Text consequence;

        private Choice pending;

        public void AskRestart() => Ask(Choice.Restart, "Start again from the beginning.");

        public void AskMainMenu() => Ask(Choice.MainMenu, "Go back to the title screen.");

        public void AskQuit() => Ask(Choice.Quit, "Close the game.");

        private void Ask(Choice choice, string line)
        {
            pending = choice;

            if (consequence != null)
            {
                consequence.text = line + " This run is lost.";
            }

            GetComponent<MenuScreen>().Show();
        }

        /// <summary>Yes: does what was asked.</summary>
        public void Confirm()
        {
            Choice choice = pending;
            pending = Choice.None;

            if (mainMenu == null)
            {
                Debug.LogWarning("AbandonRun has no MainMenu to carry the choice out.", this);
                return;
            }

            switch (choice)
            {
                case Choice.Restart:
                    mainMenu.Jogar();
                    break;

                case Choice.MainMenu:
                    mainMenu.MenuPrincipal();
                    break;

                case Choice.Quit:
                    mainMenu.Sair();
                    break;
            }
        }

        /// <summary>No: back to the pause screen, the run untouched.</summary>
        public void Cancel()
        {
            pending = Choice.None;
            GetComponent<MenuScreen>().Back();
        }
    }
}
