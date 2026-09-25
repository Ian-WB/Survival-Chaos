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
    /// stays inside the band wherever the band goes, and that the hull still
    /// walls off the whole band for the ram. Read off the Boss prefab, its pods,
    /// its hull and its authored height, rather than off numbers copied here.
    /// </summary>
    public class BossBandHeightTests
    {
        /// <summary>
        /// PlayerBounds in the Game scene since the evening of 25 September 2026,
        /// when the band grew to 10.5 to make room for a boss a quarter bigger.
        /// It was 2.72 to 11.62 before, and the boss flew at 6.09 on it.
        /// </summary>
        private const float Floor = 2.7875f;
        private const float Ceiling = 13.2875f;

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
            Assert.AreEqual(8.0375f, SpawnBand.Middle(Floor, Ceiling), 1e-4f);
        }

        /// <summary>
        /// 1.35 under the band's middle, which puts the hull's own middle on it.
        /// Pinned so the height cannot drift without the tests saying so.
        /// </summary>
        [Test]
        public void OnTheBandAsItIs_TheBossFliesAt6Point69()
        {
            GameObject boss = Boss();

            Assert.AreEqual(6.6875f, SpawnBand.Middle(Floor, Ceiling) + HeightFromBandMiddle(boss), 0.005f);
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
        public void HullStretch_IsOne_WhenTheHullAlreadyReaches()
        {
            Assert.AreEqual(1f, BossEmitter.HullStretch(-10f, 10f, -4f, 4f, 1f), 1e-5f);
        }

        [Test]
        public void HullStretch_ReachesPastBothEdgesOfATallBand()
        {
            // A hull 5 each side of 0 against a band 8 each side, with 1 to spare: 9 of 5.
            Assert.AreEqual(1.8f, BossEmitter.HullStretch(-5f, 5f, -8f, 8f, 1f), 1e-5f);
        }

        [Test]
        public void HullStretch_ReachesTheFartherEdge_OfAnOffCentreHull()
        {
            // Middle 5, half 5; the floor's -1 with 1 to spare is 7 below the middle.
            Assert.AreEqual(1.4f, BossEmitter.HullStretch(0f, 10f, -1f, 9f, 1f), 1e-5f);
        }

        /// <summary>
        /// The ram's one counter is the dash because the hull is a wall: it spans
        /// the band, so climbing over it or diving under it is not an option.
        /// Pinned against the prefab's own hull box, its height and its overhang,
        /// for bands moved and made taller - up to 24, which the authored 19.1
        /// hull cannot span without stretching.
        /// </summary>
        [TestCase(0f, 10.5f)]
        [TestCase(-1.7f, 10.5f)]
        [TestCase(3f, 10.5f)]
        [TestCase(0f, 14f)]
        [TestCase(0f, 18f)]
        [TestCase(2f, 24f)]
        public void TheRam_CannotBeClimbedOverOrDivedUnder_WhereverTheBandGoes(float shift, float height)
        {
            GameObject boss = Boss();
            HullOf(boss, out BoxCollider box, out float overhang);

            float floor = Floor + shift;
            float ceiling = floor + height;
            float middle = SpawnBand.Middle(floor, ceiling) + HeightFromBandMiddle(boss) + box.center.y;
            float half = 0.5f * box.size.y;
            float stretch = BossEmitter.HullStretch(middle - half, middle + half, floor, ceiling, overhang);

            Assert.That(middle - (half * stretch), Is.LessThanOrEqualTo(floor - overhang + 1e-3f), "under the floor");
            Assert.That(middle + (half * stretch), Is.GreaterThanOrEqualTo(ceiling + overhang - 1e-3f), "over the ceiling");
        }

        [Test]
        public void OnTheBandAsItIs_TheHullKeepsItsSize()
        {
            GameObject boss = Boss();
            HullOf(boss, out BoxCollider box, out float overhang);

            float middle = SpawnBand.Middle(Floor, Ceiling) + HeightFromBandMiddle(boss) + box.center.y;
            float half = 0.5f * box.size.y;

            Assert.AreEqual(1f, BossEmitter.HullStretch(middle - half, middle + half, Floor, Ceiling, overhang));
            // Centred on the band's middle, which is what makes any shift of the band safe.
            Assert.AreEqual(SpawnBand.Middle(Floor, Ceiling), middle, 0.01f);
        }

        /// <summary>The box the ram hits with, off the prefab, and the model that stretches with it.</summary>
        private static void HullOf(GameObject boss, out BoxCollider box, out float overhang)
        {
            var emitter = new SerializedObject(boss.GetComponent<BossEmitter>());
            box = emitter.FindProperty("hullBox").objectReferenceValue as BoxCollider;
            var model = emitter.FindProperty("hullModel").objectReferenceValue as Transform;
            overhang = emitter.FindProperty("hullOverhang").floatValue;

            Assert.IsNotNull(box, "BossEmitter has no hull box");
            Assert.AreSame(boss, box.gameObject, "the hull box is not the one on the boss itself");
            Assert.IsNotNull(model, "BossEmitter has no hull model");
            Assert.IsNotNull(model.GetComponentInChildren<MeshRenderer>(), "the hull model draws nothing");
            Assert.Greater(overhang, 0f);
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
