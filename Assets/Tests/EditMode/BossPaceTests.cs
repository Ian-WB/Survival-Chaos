using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The boss travels at a multiple of the ship's speed, and nothing else on
    /// the ring does.
    ///
    /// It is one number on the boss prefab's EnemyMovement, and nothing else
    /// notices it going: at zero the boss falls back to its authored 20 degrees
    /// a second, the fight with no picks is nearly the same, and only a run
    /// that takes three or more finds the boss falling behind, which is how it
    /// was found on 28 September 2026. On a wave enemy it would take away the
    /// one thing the picks buy against the waves.
    /// </summary>
    public class BossPaceTests
    {
        [Test]
        public void TheBossIsFasterThanTheShip()
        {
            GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss.prefab");
            Assert.That(boss, Is.Not.Null, "no boss prefab");

            EnemyMovement movement = boss.GetComponent<EnemyMovement>();
            Assert.That(movement, Is.Not.Null, "the boss has no EnemyMovement");

            // At 1 or below running away wins, and only the ram ever catches.
            Assert.Greater(new SerializedObject(movement).FindProperty("playerSpeedRatio").floatValue, 1f,
                "the boss should travel faster than the ship");
        }

        [Test]
        public void NoWaveEnemyKeepsPaceWithTheShip()
        {
            int checkedCount = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies" }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));

                foreach (EnemyMovement movement in prefab.GetComponentsInChildren<EnemyMovement>(true))
                {
                    checkedCount++;
                    Assert.AreEqual(0f, new SerializedObject(movement).FindProperty("playerSpeedRatio").floatValue,
                        prefab.name + " should keep its own pace");
                }
            }

            Assert.Greater(checkedCount, 0, "found no enemy movement to check");
        }

        [Test]
        public void ARatio_FollowsThePlayerWhateverTheirSpeed()
        {
            Assert.AreEqual(20f, EnemyMovement.CruiseDegrees(12f, 1.25f, 16f), 0.0001f);
            Assert.AreEqual(30f, EnemyMovement.CruiseDegrees(12f, 1.25f, 24f), 0.0001f);
        }

        [Test]
        public void NoRatio_KeepsTheAuthoredSpeed()
        {
            Assert.AreEqual(12f, EnemyMovement.CruiseDegrees(12f, 0f, 24f), 0.0001f);
        }
    }
}
