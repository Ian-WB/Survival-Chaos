using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tutorial : MonoBehaviour
{
    public GameObject shiftTutorial;

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
