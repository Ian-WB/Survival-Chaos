using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

public class EnemyMovement : MonoBehaviour
{
    public float speed = 5.0f;
    private Transform player;

    public float rotationSpeed;

    private Vector3 center;
    private float centerZ;
    private float centerX;

    public bool leftOrRight = true;

    public float spawnSpeed;

    /// <summary>
    /// How close the player has to be, vertically and around the ring, before
    /// this enemy starts matching their height.
    ///
    /// Expressed as a fraction of the orbit radius rather than as a literal. It
    /// was the bare number 15, tuned when the radius was 137.2 - about an 11%
    /// band. The arena was later shrunk tenfold and the literal stayed, which
    /// left the threshold larger than the whole arena: the check could never be
    /// false, so every enemy chased vertically all the time.
    /// </summary>
    private const float ChaseRadiusFraction = 0.11f;

    private static float ChaseRadius => ArenaGeometry.OrbitRadius * ChaseRadiusFraction;

    void Start()
    {
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        GameObject scenario = GameObject.FindWithTag("Scenario");
        if (scenario != null)
        {
            centerX = scenario.transform.position.x;
            centerZ = scenario.transform.position.z;
            center = new Vector3(centerX, 0f, centerZ);
        }
    }


void Update()
    {
        if (player == null)
        {
            return;
        }
        
        Vector3 pos = center;
        pos.y = transform.position.y;

        // Distance from the axis, ignoring height - the enemy flies in to the
        // orbit radius first, then rides it.
        Vector3 flat = transform.position;
        flat.y = 0f;

        transform.LookAt(pos);

        // Both approaches below go through ShipMotion.Approach, which eases the
        // same amount per second whatever the frame rate and cannot overshoot.
        // They were written as `position += (target - position) * deltaTime *
        // speed`, which is the same curve only while deltaTime is small: once
        // deltaTime * speed passes 2 the step is larger than the gap and the
        // enemy diverges instead of arriving. That is reachable on a slow
        // machine, and presents as enemies flying off rather than as a frame
        // rate complaint.
        if(Vector3.Distance(center, flat) >= ArenaGeometry.OrbitRadius)
        {
            Vector3 next = transform.position;
            next.x = ShipMotion.Approach(next.x, center.x, spawnSpeed, Time.deltaTime);
            next.z = ShipMotion.Approach(next.z, center.z, spawnSpeed, Time.deltaTime);
            transform.position = next;
        }
        else
        {
            float direction = leftOrRight ? 1f : -1f;
            transform.RotateAround(pos, Vector3.up, direction * rotationSpeed * Time.deltaTime);

            if(Vector3.Distance(transform.position, player.position) <= ChaseRadius)
            {
                // Height only: the ring position is handled by the orbit above.
                Vector3 next = transform.position;
                next.y = ShipMotion.Approach(next.y, player.position.y, speed, Time.deltaTime);
                transform.position = next;
            }
        }
    }
}