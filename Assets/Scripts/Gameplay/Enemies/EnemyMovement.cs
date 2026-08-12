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

    [SerializeField]
    [Tooltip("How close the player has to be before this enemy starts matching their height, " +
             "as a multiple of the arena orbit radius. Both sit on the ring, so this is a " +
             "straight-line distance across it: 1.1 is roughly a 66 degree arc either side of " +
             "the player, 0.11 is about 6 degrees.")]
    private float chaseRadiusFraction = 1.1f;

    /// <summary>
    /// The distance inside which this enemy chases the player's height.
    ///
    /// A multiple of the orbit radius rather than a literal, so it survives
    /// another rescale of the arena. It was the bare number 15.
    ///
    /// That literal was flagged as stale on the grounds that 15 exceeds the
    /// orbit radius of 13.72 and so could never be false. That reasoning was
    /// wrong: both the enemy and the player sit on the ring, so the distance
    /// between them runs up to the *diameter*, 27.44, not the radius. 15 is a
    /// real threshold there - it covers about 66 degrees of arc either side of
    /// the player, a wide band but far from the whole ring.
    ///
    /// Setting it to a tenth, which is what the arena's tenfold shrink would
    /// imply if the original intent was an arc, collapsed the band to about 6
    /// degrees and the chase all but stopped happening. The default restores
    /// what the game actually played like; it is serialized because this is a
    /// feel value, not a constant, and different enemies may want different
    /// answers.
    /// </summary>
    private float ChaseRadius => ArenaGeometry.OrbitRadius * chaseRadiusFraction;

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