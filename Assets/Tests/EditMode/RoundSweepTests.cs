using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The query behind a round's sweep between physics steps - see
    /// ShootScript.FixedUpdate. A round that stops at its first target has to
    /// stop at the nearest one, and a crowded stretch must not push the target
    /// out of the answer: every other round in the air is in it too.
    /// </summary>
    public class RoundSweepTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject thing in created)
            {
                if (thing != null)
                {
                    Object.DestroyImmediate(thing);
                }
            }

            created.Clear();
        }

        /// <summary>A trigger box on the x axis, far from anything else a scene might hold.</summary>
        private Collider Box(float x, float size = 0.2f)
        {
            var thing = new GameObject("Sweep Target " + x);
            thing.transform.position = new Vector3(x, 5000f, 0f);
            var box = thing.AddComponent<BoxCollider>();
            box.size = Vector3.one * size;
            box.isTrigger = true;
            created.Add(thing);
            return box;
        }

        private static int Sweep(float from, float distance, out RaycastHit[] hits)
        {
            Physics.SyncTransforms();
            return ShootScript.SweepBox(new Vector3(from, 5000f, 0f), Vector3.one * 0.05f, Vector3.right,
                Quaternion.identity, distance, out hits);
        }

        [Test]
        public void Hits_ComeBackNearestFirst()
        {
            // Made far one first, so an answer in creation order would be wrong.
            Collider far = Box(8f);
            Collider near = Box(4f);
            Collider middle = Box(6f);

            int count = Sweep(0f, 10f, out RaycastHit[] hits);

            Assert.AreEqual(3, count);
            Assert.AreSame(near, hits[0].collider);
            Assert.AreSame(middle, hits[1].collider);
            Assert.AreSame(far, hits[2].collider);
        }

        [Test]
        public void ACrowdedStretch_StillReportsWhatLiesBeyondIt()
        {
            // Twenty boxes in the way of the buffer's first sixteen places, and
            // the one that matters behind them.
            for (int i = 0; i < 20; i++)
            {
                Box(1f + i * 0.1f, 0.05f);
            }

            Collider target = Box(9f);

            int count = Sweep(0f, 10f, out RaycastHit[] hits);

            Assert.AreEqual(21, count, "hits were dropped when the buffer filled");
            Assert.AreSame(target, hits[count - 1].collider);
        }
    }
}
