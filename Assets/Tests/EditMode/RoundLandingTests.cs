using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// A player round landing on an enemy counts once. The sweep and the
    /// physics system both report contacts, so a round spent on one enemy can
    /// still be reported entering another in the same step; that second report
    /// must not be a second hit.
    ///
    /// Took over from RoundPierceTests when Piercing Rounds was removed on
    /// 25 September 2026: the pierce went, the once-only rule stayed.
    /// </summary>
    public class RoundLandingTests
    {
        private GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            ObjectPool.Clear();
            prefab = new GameObject("Round");
            prefab.AddComponent<BoxCollider>().isTrigger = true;
            prefab.AddComponent<ShootScript>();
        }

        [TearDown]
        public void TearDown()
        {
            ObjectPool.Clear();

            GameObject root = GameObject.Find("Object Pool");
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            Object.DestroyImmediate(prefab);
        }

        private Collider Fire()
        {
            return ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity).GetComponent<Collider>();
        }

        [Test]
        public void TheFirstEnemy_TakesTheHit_AndSpendsTheRound()
        {
            Collider round = Fire();

            Assert.IsTrue(ShootScript.Land(round));
            Assert.IsFalse(round.gameObject.activeSelf);
        }

        [Test]
        public void ASecondReport_OfASpentRound_IsNotAHit()
        {
            Collider round = Fire();
            ShootScript.Land(round);

            Assert.IsFalse(ShootScript.Land(round));
        }

        [Test]
        public void TwoRounds_EachLandOnce()
        {
            Collider first = Fire();
            Collider second = Fire();

            Assert.IsTrue(ShootScript.Land(first));
            Assert.IsTrue(ShootScript.Land(second));
        }
    }
}
