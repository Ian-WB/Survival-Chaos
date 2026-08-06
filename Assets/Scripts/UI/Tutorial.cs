using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tutorial : MonoBehaviour
{
    public GameObject shiftTutorial;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(showShiftTutorial());
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    IEnumerator showShiftTutorial()
    {
        shiftTutorial.SetActive(true);
        yield return new WaitForSeconds(5);
        shiftTutorial.SetActive(false);
    }
}
