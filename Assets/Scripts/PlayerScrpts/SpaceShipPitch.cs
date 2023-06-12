using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceShipPitch : MonoBehaviour
{

    public float rotationSpeed;
    private KeyCode lastPressedKey;

    private bool rotate;
    // Start is called before the first frame update
    void Start()
    {
        rotate = false;
    }

    // Update is called once per frame
    void Update()
    {

        if (Input.anyKeyDown)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                lastPressedKey = KeyCode.A;
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                lastPressedKey = KeyCode.D;
            }
            
        }      

        float v = Input.GetAxis("Vertical");
        float h = Input.GetAxis("Horizontal");

        if(Input.GetKeyUp(KeyCode.LeftShift))
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
