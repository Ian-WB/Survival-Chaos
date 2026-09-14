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

            float lane = ArenaGeometry.LaneRadius;

            if(Vector3.Distance(center, flat) > lane + 0.001f)
            {
                // Through ShipMotion.Approach for the same reason as EnemyMovement:
                // the original `position += (center - position) * deltaTime * speed`
                // holds its curve only while deltaTime is small, and diverges once
                // deltaTime * spawnSpeed passes 2.
                Vector3 next = transform.position;
                next.x = ShipMotion.Approach(next.x, center.x, spawnSpeed, Time.deltaTime);
                next.z = ShipMotion.Approach(next.z, center.z, spawnSpeed, Time.deltaTime);

                // Stops on the lane, not past it: a long frame would otherwise
                // leave it wherever the step ended, as EnemyMovement explains.
                Vector3 nextFlat = next;
                nextFlat.y = 0f;

                if (Vector3.Distance(center, nextFlat) <= lane)
                {
                    next = ArenaGeometry.ProjectOntoOrbit(next, center, lane);
                }

                transform.position = next;
            }
        }
    }
}
