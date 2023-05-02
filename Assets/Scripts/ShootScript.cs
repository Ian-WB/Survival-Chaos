using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShootScript : MonoBehaviour
{

    public float rotationSpeed = 5f;

    public Transform player;
    private float centerZ;
    private float centerX;
    private Vector3 center;
    public Transform referencePoint;
    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.Find("ReferencePoint").transform;
        centerX = GameObject.FindWithTag("Scenario").transform.position.x;
        centerZ = GameObject.FindWithTag("Scenario").transform.position.z;
        center = new Vector3(centerX, 0f, centerZ);
    }

    // Update is called once per frame
    void Update()
    {
        transform.RotateAround(center, Vector3.up, rotationSpeed * Time.deltaTime);
    }
        
}
