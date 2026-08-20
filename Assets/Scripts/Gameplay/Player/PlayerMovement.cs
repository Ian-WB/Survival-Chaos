using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

public class PlayerMovement : MonoBehaviour
{

    [SerializeField]
    [Tooltip("The arena axis this object orbits. The camera runs this same component, " +
             "which is how it stays in step with the player rather than following it.")]
    private Transform center;

    [SerializeField]
    [Tooltip("Degrees per second around the axis - not units per second. RotateAround takes " +
             "an angle, which is why a tenth-scale arena needed no change here.")]
    private float speed;

    [SerializeField]
    [Tooltip("Vertical speed, in world units per second. Genuinely linear, so unlike the " +
             "orbit speed above this one did have to scale with the arena.")]
    private float Up_Down_Speed;

    void Update()
    {
        if (center == null)
        {
            return;
        }

        float h = GameInput.Horizontal;
        float v = GameInput.Vertical;
        
        Vector3 pos = center.position;
        pos.y = transform.position.y;
        transform.RotateAround(pos, Vector3.up, -h * Time.deltaTime * speed);
        transform.LookAt(pos);


        transform.position += Vector3.up * v * Time.deltaTime * Up_Down_Speed;
    }
}
