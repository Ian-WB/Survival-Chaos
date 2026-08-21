using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    public class Tutorial : MonoBehaviour
    {
        [SerializeField]
        private GameObject shiftTutorial;

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
            shiftTutorial.SetActive(true);
            yield return new WaitForSeconds(5);
            shiftTutorial.SetActive(false);
        }
    }
}
