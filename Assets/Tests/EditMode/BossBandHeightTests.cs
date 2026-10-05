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
        /// PlayerBounds in the Game scene since 5 October 2026: 9.23 tall, the
        /// most a camera 9 out at 65 degrees shows clear of the HUD, about the
        /// middle it already had. It was 2.7875 to 13.2875 from 25 September,
        /// 10.5 tall, and the boss flew at 6.69 on it.
        /// </summary>
        private const float Floor = 3.4225f;
        private const float Ceiling = 12.6525f;

        /// <summary>
        /// Keeps the band above the Game scene's. Written out rather than read
        /// in every test so the heights below stay readable, and checked here so
        /// the copy cannot quietly go stale when the band next moves.
        /// </summary>
        [Test]
        public void TheBandHere_IsTheGameScenes()
        {
            SavedScene.Load("Assets/Scenes/Game.unity").Band("Player", out float floor, out float ceiling);

            Assert.AreEqual(floor, Floor, 1e-3f, "floor");
            Assert.AreEqual(ceiling, Ceiling, 1e-3f, "ceiling");
        }

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
        /// 1.568 under the band's middle, which puts the keel and crown pods the
        /// same distance either side of it. Pinned so the height cannot drift
        /// without the tests saying so.
        /// </summary>
        [Test]
        public void OnTheBandAsItIs_TheBossFliesAt6Point47()
        {
            GameObject boss = Boss();

            Assert.AreEqual(6.4695f, SpawnBand.Middle(Floor, Ceiling) + HeightFromBandMiddle(boss), 0.005f);
        }

        /// <summary>
        /// The band is only 0.07 taller than the keel and crown pods are apart,
        /// so the boss has to sit with them centred on it or one goes outside.
        /// </summary>
        [Test]
        public void TheKeelAndTheCrown_AreTheSameDistanceFromTheBandsEdges()
        {
            GameObject boss = Boss();
            float lowest = float.MaxValue, highest = float.MinValue;

            foreach (BossWeakPoint pod in boss.GetComponentsInChildren<BossWeakPoint>(true))
            {
                float height = PodHeight(boss, pod, Floor, Ceiling);
                lowest = Mathf.Min(lowest, height);
                highest = Mathf.Max(highest, height);
            }

            Assert.AreEqual(lowest - Floor, Ceiling - highest, 0.01f);
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
        [TestCase(0f, 9.23f)]
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
            // 0.22 under the band's middle since 5 October 2026, when the pods
            // took the centre instead. It has 4.7 to spare past either edge.
            Assert.AreEqual(SpawnBand.Middle(Floor, Ceiling) - 0.218f, middle, 0.01f);
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
