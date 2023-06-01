using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    public Transform center;
    public float speed;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        
        Vector3 pos = center.position;
        pos.y = transform.position.y;
        transform.RotateAround(pos, Vector3.up, -h * Time.deltaTime * speed);
        transform.LookAt(pos);


        transform.position += Vector3.up * v * Time.deltaTime * speed;
    }
}
