using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

public class SpaceShipPitch : MonoBehaviour
{

    public float rotationSpeed;

    private bool rotate;
    // Start is called before the first frame update
    void Start()
    {
        rotate = false;
    }

    // Update is called once per frame
    void Update()
    {


        float v = GameInput.Vertical;
        float h = GameInput.Horizontal;

        if(GameInput.ToggleDirectionReleased)
        {
            rotate = !rotate;
        }

        
        
        if(rotate) 
        {
            transform.localRotation = Quaternion.Euler(0, 180, -Mathf.Sign(h) * v * 25);
        }

        else
        {
            transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sign(h) * v * 25);
        }

        
        

        
    }
}
