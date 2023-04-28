using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamMovement : MonoBehaviour
{
    public CircleMovement player;
    [HideInInspector]
    public float speed;
    private float centerZ;
    private float centerX;
    private Vector3 center;

    void Start()
    {
        speed = player.rotationSpeed;
        centerX = GameObject.FindWithTag("Scenario").transform.position.x;
        centerZ = GameObject.FindWithTag("Scenario").transform.position.z;
        center = new Vector3(centerX, 0f, centerZ);
    }

    void Update()
    {
        // Horizontal movement
        float horizontalInput = Input.GetAxis("Horizontal");

        // Set position
        transform.RotateAround(center, Vector3.up, -player.rotationSpeed * horizontalInput * Time.deltaTime);

        // Look to scenario
        transform.LookAt(center);
    }
}