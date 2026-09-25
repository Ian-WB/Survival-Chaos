using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The boss's height is measured from the player's band, so moving the band
    /// moves the boss. On 21 September 2026 the band moved and the boss did not,
    /// and the crown emplacement ended up above the ceiling, where most runs
    /// could not reach it - so what is pinned here is that every emplacement
    /// stays inside the band wherever the band goes. Read off the Boss prefab,
    /// its pods and its authored height, rather than off numbers copied here.
    /// </summary>
    public class BossBandHeightTests
    {
        /// <summary>PlayerBounds in the Game scene, as it was when the boss flew at 6.09.</summary>
        private const float Floor = 2.72f;
        private const float Ceiling = 11.62f;

        private static GameObject Boss()
        {
            GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss.prefab");
            Assert.IsNotNull(boss, "Boss prefab not found");
            return boss;
        }

        private static float HeightFromBandMiddle(GameObject boss)
        {
            var emitter = new SerializedObject(boss.GetComponent<BossEmitter>());
            SerializedProperty height = emitter.FindProperty("heightFromBandMiddle");
            Assert.IsNotNull(height, "BossEmitter has no heightFromBandMiddle");
            return height.floatValue;
        }

        /// <summary>Where a pod sits when the boss flies against the given band.</summary>
        private static float PodHeight(GameObject boss, BossWeakPoint pod, float floor, float ceiling)
        {
            float bossHeight = SpawnBand.Middle(floor, ceiling) + HeightFromBandMiddle(boss);
            return bossHeight + (pod.transform.position.y - boss.transform.position.y);
        }

        [Test]
        public void TheMiddle_IsHalfwayUpTheBand()
        {
            Assert.AreEqual(7.17f, SpawnBand.Middle(Floor, Ceiling), 1e-4f);
        }

        [Test]
        public void OnTheBandAsItIs_TheBossFliesWhereItAlwaysHas()
        {
            GameObject boss = Boss();

            Assert.AreEqual(6.09f, SpawnBand.Middle(Floor, Ceiling) + HeightFromBandMiddle(boss), 0.005f);
        }

        [TestCase(0f)]
        [TestCase(-1.7f)]
        [TestCase(3f)]
        [TestCase(-6f)]
        public void EveryEmplacement_StaysInsideTheBand_WhereverTheBandMoves(float shift)
        {
            GameObject boss = Boss();
            BossWeakPoint[] pods = boss.GetComponentsInChildren<BossWeakPoint>(true);
            Assert.AreEqual(3, pods.Length, "expected the keel, prow and crown emplacements");

            float floor = Floor + shift;
            float ceiling = Ceiling + shift;

            foreach (BossWeakPoint pod in pods)
            {
                Assert.That(PodHeight(boss, pod, floor, ceiling), Is.InRange(floor, ceiling), pod.name);
            }
        }

        [Test]
        public void TheLance_RunsThroughTheMiddleOfTheBand()
        {
            // What the lance is for: nothing else in the fight reaches the middle.
            GameObject boss = Boss();
            BossWeakPoint prow = null;

            foreach (BossWeakPoint pod in boss.GetComponentsInChildren<BossWeakPoint>(true))
            {
                if (pod.name.Contains("Prow"))
                {
                    prow = pod;
                }
            }

            Assert.IsNotNull(prow, "no prow emplacement");
            Assert.AreEqual(SpawnBand.Middle(Floor, Ceiling), PodHeight(boss, prow, Floor, Ceiling), 0.5f);
        }
    }
}
