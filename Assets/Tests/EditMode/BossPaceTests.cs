using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The boss keeps pace with the ship's Move Speed picks, and nothing else
    /// on the ring does.
    ///
    /// It is one tickbox on the boss prefab's EnemyMovement, and nothing else
    /// notices it going: with no picks the fight is the same either way, and
    /// only a run that takes three or more finds the boss falling behind, which
    /// is how it was found on 28 September 2026. On a wave enemy it would take
    /// away the one thing the picks buy against the waves.
    /// </summary>
    public class BossPaceTests
    {
        [Test]
        public void TheBossKeepsPaceWithTheShip()
        {
            GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss.prefab");
            Assert.That(boss, Is.Not.Null, "no boss prefab");

            EnemyMovement movement = boss.GetComponent<EnemyMovement>();
            Assert.That(movement, Is.Not.Null, "the boss has no EnemyMovement");

            Assert.IsTrue(new SerializedObject(movement).FindProperty("keepPaceWithPlayer").boolValue,
                "the boss should keep pace with the ship's Move Speed picks");
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
                    Assert.IsFalse(new SerializedObject(movement).FindProperty("keepPaceWithPlayer").boolValue,
                        prefab.name + " should keep its own pace");
                }
            }

            Assert.Greater(checkedCount, 0, "found no enemy movement to check");
        }
    }
}
