using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

public class sceneRotation : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }
    private float moveSpeed = 35.0f;
    // Update is called once per frame
    void Update()
    {
        var h = GameInput.Horizontal;
        var rotate = new Vector3(
            0f,
            h * moveSpeed * Time.deltaTime,
            0f
        );
        transform.Rotate(rotate);
    }
}
