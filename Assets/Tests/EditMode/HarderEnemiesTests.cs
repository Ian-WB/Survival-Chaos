using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using RendererRayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// What Ian asked for on 10 November 2026: a Heavy twice the size with
    /// twice the health, and enemy rounds he can see that cost 2 a hit.
    /// EnemyRoundBuilder writes the rounds; these hold what it wrote.
    /// </summary>
    public class HarderEnemiesTests
    {
        private const string TrailMaterial = "Assets/Art/Materials/VFX/EnemyRoundTrail.mat";

        [TestCase("Assets/Prefabs/Enemies/enemy_shoot 1.prefab", 1.0f)]
        [TestCase("Assets/Prefabs/Enemies/enemy_shoot 2.prefab", 1.0f)]
        [TestCase("Assets/Prefabs/Enemies/temp_shoot.prefab", 0.1f)]
        [TestCase("Assets/Prefabs/Enemies/temp_shoot1.prefab", 0.1f)]
        public void AnEnemyRound_IsDoubled_Costs2_AndFliesFor07Seconds(string path, float scale)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(root, path);

            Assert.AreEqual(scale, root.transform.localScale.x, 1e-4f, path + " root scale");
            Assert.AreEqual(2, root.GetComponent<ShootScript>().Damage, path + " damage");

            var life = new SerializedObject(root.GetComponent<DestroyAfterTime>());
            Assert.AreEqual(0.7f, life.FindProperty("delay").floatValue, 1e-4f, path + " life");
        }

        [TestCase("Assets/Prefabs/Enemies/enemy_shoot 1.prefab")]
        [TestCase("Assets/Prefabs/Enemies/enemy_shoot 2.prefab")]
        [TestCase("Assets/Prefabs/Enemies/temp_shoot.prefab")]
        [TestCase("Assets/Prefabs/Enemies/temp_shoot1.prefab")]
        public void AnEnemyRound_TrailsAVioletStreakBehindIt(string path)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(root, path);

            MeshRenderer streak = root.GetComponent<MeshRenderer>();
            Assert.IsNotNull(streak, path + " has no streak on its root");
            Assert.AreEqual(TrailMaterial, AssetDatabase.GetAssetPath(streak.sharedMaterial));
            Assert.AreEqual(MotionVectorGenerationMode.Object, streak.motionVectorGenerationMode,
                "a streak without its own motion vectors is dragged off its round by FSR");
            Assert.AreEqual(RendererRayTracingMode.Off, streak.rayTracingMode);

            Mesh mesh = root.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh, path + " streak mesh");

            // About a unit long in the world, and centred, so the round's light
            // stays on the round (ShootScript averages the bounds' centres).
            float world = mesh.bounds.extents.x * root.transform.localScale.x;
            Assert.AreEqual(1f, world, 1e-3f, path + " streak length");
            // On the round itself: the Fighter's dart is not modelled on its pivot,
            // and a streak left on the pivot flew a third of a unit from its dart.
            Vector3 box = root.GetComponent<BoxCollider>().center;
            Assert.Less((mesh.bounds.center - box).magnitude * root.transform.localScale.x, 0.01f,
                path + " streak is not on its round");

            foreach (MeshRenderer part in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (part == streak)
                {
                    continue;
                }

                Vector3 drawn = root.transform.InverseTransformPoint(part.bounds.center);
                Assert.Less((drawn - box).magnitude, 0.08f / root.transform.localScale.x,
                    path + " model is not where its hit box and streak are");
            }
        }

        [Test]
        public void TheTwoRoundsOfAPair_TrailOppositeWays()
        {
            // Each pair flies opposite ways along the same local X, so their
            // streaks must point opposite ways too.
            AssertOpposite("Assets/Prefabs/Enemies/enemy_shoot 1.prefab", "Assets/Prefabs/Enemies/enemy_shoot 2.prefab");
            AssertOpposite("Assets/Prefabs/Enemies/temp_shoot.prefab", "Assets/Prefabs/Enemies/temp_shoot1.prefab");
        }

        private static void AssertOpposite(string a, string b)
        {
            float speedA = Speed(a), speedB = Speed(b);
            Assert.Less(speedA * speedB, 0f, a + " and " + b + " should fly opposite ways");
            Assert.Less(TailSign(a) * TailSign(b), 0f, a + " and " + b + " trail the same way");
        }

        private static float Speed(string path)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return new SerializedObject(root.GetComponent<ShootScript>()).FindProperty("speed").floatValue;
        }

        private static float TailSign(string path)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<MeshFilter>().sharedMesh;
            float sum = 0f;
            foreach (Vector3 v in mesh.vertices)
            {
                sum += v.x;
            }

            return Mathf.Sign(sum);
        }

        [TestCase("Assets/Prefabs/Boss/boss_disc 1.prefab")]
        [TestCase("Assets/Prefabs/Boss/boss_disc 2.prefab")]
        [TestCase("Assets/Prefabs/Boss/boss_shoot 3.prefab")]
        [TestCase("Assets/Prefabs/Boss/boss_shoot 4.prefab")]
        [TestCase("Assets/Prefabs/Boss/boss_shoot 5.prefab")]
        [TestCase("Assets/Prefabs/Boss/boss_shoot 6.prefab")]
        public void TheLeviathansRounds_StillCost1(string path)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(root, path);
            Assert.AreEqual(1, root.GetComponent<ShootScript>().Damage, path);
        }

        [TestCase("Assets/Prefabs/Enemies/Enemy.prefab", 1)]
        [TestCase("Assets/Prefabs/Enemies/Enemy 1.prefab", 2)]
        public void TheFighterFiresOneRoundAVolley_TheHeavyTwo(string path, int rounds)
        {
            GameObject ship = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(ship, path);

            var guns = new SerializedObject(ship.GetComponent<Enemy_1>());
            Assert.AreEqual(rounds, guns.FindProperty("roundsPerVolley").intValue, path);
        }

        [Test]
        public void TheHeavy_IsTwiceTheSize_WithTenHealth()
        {
            GameObject heavy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy 1.prefab");
            Assert.IsNotNull(heavy);
            Assert.AreEqual(new Vector3(2f, 2f, 2f), heavy.transform.localScale);

            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Heavy.asset");
            Assert.IsNotNull(definition);
            Assert.AreEqual(10, definition.MaxHealth);
        }
    }
}
