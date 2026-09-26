using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Where a boss emplacement's centre ends up once the boss is on the lane.
    ///
    /// The player's rounds orbit at the lane radius and never leave it, so a pod
    /// whose centre is off the lane loses that much of its window to a sideways
    /// miss the camera cannot show. Until 26 September 2026 the pods sat 0.23 to
    /// 0.29 inside the 19.72 lane, from a correction worked out for the 13.72
    /// one.
    /// </summary>
    public class BossPodLaneTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void Call(object target, string method)
        {
            target.GetType().GetMethod(method, Private).Invoke(target, null);
        }

        /// <summary>
        /// Both headings, since the rig the pods hang off turns round with the
        /// ship, and the offsets of all three banks.
        /// </summary>
        [Test]
        public void APodOnASettledBoss_SitsOnTheLane_InBothHeadings()
        {
            float lane = ArenaGeometry.LaneRadius;

            foreach (float yaw in new[] { 90f, 270f })
            {
                foreach (float outboard in new[] { 4.9375f, 5.75f })
                {
                    GameObject boss = new GameObject("Boss");

                    try
                    {
                        // On the lane and facing the middle, as EnemyMovement
                        // leaves it.
                        boss.transform.position = new Vector3(0f, 5f, -lane);
                        boss.transform.LookAt(new Vector3(0f, 5f, 0f));
                        boss.AddComponent<BossEmitter>();

                        Transform rig = new GameObject("Rig").transform;
                        rig.SetParent(boss.transform, false);
                        rig.localRotation = Quaternion.Euler(0f, yaw, 0f);

                        GameObject pod = new GameObject("Pod");
                        pod.transform.SetParent(rig, false);
                        pod.transform.localPosition = new Vector3(0f, 1f, outboard);
                        BossWeakPoint weakPoint = pod.AddComponent<BossWeakPoint>();

                        Call(weakPoint, "Awake");
                        Call(weakPoint, "LateUpdate");

                        Vector3 flat = pod.transform.position;
                        flat.y = 0f;

                        Assert.AreEqual(lane, flat.magnitude, 0.001f,
                            "rig at yaw " + yaw + ", " + outboard + " outboard");
                    }
                    finally
                    {
                        Object.DestroyImmediate(boss);
                    }
                }
            }
        }
    }
}
