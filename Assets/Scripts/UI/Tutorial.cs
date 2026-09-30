using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    public class Tutorial : MonoBehaviour
    {
        [SerializeField]
        private GameObject shiftTutorial;

        /// <summary>Whether the prompt's five seconds are still running.</summary>
        private bool showing;

        void Start()
        {
            if (shiftTutorial == null)
            {
                Debug.LogWarning("Tutorial has no prompt assigned, so nothing will be shown.", this);
                return;
            }

            StartCoroutine(showShiftTutorial());
        }

        IEnumerator showShiftTutorial()
        {
            showing = true;
            Refresh();
            yield return new WaitForSeconds(5);
            showing = false;
            Refresh();
        }

        /// <summary>
        /// Out of the way while a menu is up. The five seconds are game time, so
        /// pausing inside them froze the prompt on screen, and it sits in front
        /// of the menus: on 30 September 2026 it covered the options tabs' Back
        /// button. It comes back for whatever is left of its time on resuming.
        /// </summary>
        void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (shiftTutorial == null)
            {
                return;
            }

            bool visible = showing && !PauseMenu.GameIsPaused && !RunOutcome.RunEnded;
            if (shiftTutorial.activeSelf != visible)
            {
                shiftTutorial.SetActive(visible);
            }
        }
    }
}
