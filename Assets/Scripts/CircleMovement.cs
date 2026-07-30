using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

public class CircleMovement : MonoBehaviour
{

    public float rotationSpeed = 5f;

    private float centerX;
    private float centerZ;
    private Vector3 center;
    
    void Start()
    {
        centerX = GameObject.FindWithTag("Scenario").transform.position.x;
        centerZ = GameObject.FindWithTag("Scenario").transform.position.z;
        center = new Vector3(centerX, 0f, centerZ);
    }

    private void Update()
    {
        // Get the horizontal input axis (e.g., A and D keys or left and right arrow keys)
        float horizontalInput = GameInput.Horizontal;
        float verticalInput = GameInput.Vertical;


        var move = new Vector3(
            0f,
            verticalInput * rotationSpeed * Time.deltaTime,
            0f   
        );
        transform.Translate(move);

        // Rotate the player around the circular environment
        transform.RotateAround(center, Vector3.up, -rotationSpeed * horizontalInput * Time.deltaTime);

    }
}