using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    public class ObstacleScript : MonoBehaviour
    {
        private Transform player;

        private Vector3 center;
        private float centerZ;
        private float centerX;


        [SerializeField]
        private float spawnSpeed;



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

            // Distance from the axis, ignoring height.
            Vector3 flat = transform.position;
            flat.y = 0f;

            transform.LookAt(pos);

            if(Vector3.Distance(center, flat) >= ArenaGeometry.OrbitRadius)
            {
                // Through ShipMotion.Approach for the same reason as EnemyMovement:
                // the original `position += (center - position) * deltaTime * speed`
                // holds its curve only while deltaTime is small, and diverges once
                // deltaTime * spawnSpeed passes 2.
                Vector3 next = transform.position;
                next.x = ShipMotion.Approach(next.x, center.x, spawnSpeed, Time.deltaTime);
                next.z = ShipMotion.Approach(next.z, center.z, spawnSpeed, Time.deltaTime);
                transform.position = next;
            }
        }
    }
}
