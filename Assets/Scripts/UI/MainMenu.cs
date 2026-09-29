using UnityEngine;

namespace SurvivalChaos
{
    public class MainMenu : MonoBehaviour
    {
        /// <summary>
        /// Starts a run, from the title screen or as a restart from the death
        /// and victory screens. Those screens leave timeScale at 0; the loading
        /// screen holds it there through the load and hands back to RunTime
        /// once the new run is on screen, so nothing here has to reset it.
        /// </summary>
        public void Jogar(){
            LoadingScreen.Load("Game", "Entering the arena");
        }

        public void Sair(){
            Application.Quit();
        }

        public void MenuPrincipal(){
            LoadingScreen.Load("Menu", "Back to the menu");
        }
    }
}
