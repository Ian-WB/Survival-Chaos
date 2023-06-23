using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShootScript : MonoBehaviour
{

    public Transform center;
    public float speed;


    // Start is called before the first frame update
    void Start()
    {
        transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        center = GameObject.FindWithTag("Scenario").transform;
    }

    // Update is called once per frame
    void Update()
    {
        //float h = Input.GetAxis("Horizontal");
        //float v = Input.GetAxis("Vertical");
        
        Vector3 pos =  center.position;
        pos.y = transform.position.y;
        transform.RotateAround(pos, Vector3.up, Time.deltaTime * speed);
        transform.LookAt(pos);


        //transform.position += Vector3.up  * Time.deltaTime * speed;
    }

}
