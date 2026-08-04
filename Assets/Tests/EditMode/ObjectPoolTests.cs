using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Pooling trades a clear rule - every projectile is new - for a subtle one:
    /// a projectile may have flown before. The failures that causes are quiet
    /// ones, so the promises are pinned down here.
    /// </summary>
    public class ObjectPoolTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        private GameObject NewPrefab(string name = "Bullet")
        {
            GameObject prefab = new GameObject(name);
            created.Add(prefab);
            return prefab;
        }

        [SetUp]
        public void SetUp()
        {
            ObjectPool.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ObjectPool.Clear();

            // Instances live under the pool's root, so removing it removes them.
            GameObject root = GameObject.Find("Object Pool");
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            foreach (GameObject prefab in created)
            {
                if (prefab != null)
                {
                    Object.DestroyImmediate(prefab);
                }
            }

            created.Clear();
        }

        [Test]
        public void Spawn_WithEmptyPool_CreatesAnInstance()
        {
            GameObject prefab = NewPrefab();

            GameObject instance = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            Assert.IsNotNull(instance);
            Assert.AreNotSame(prefab, instance);
            Assert.IsTrue(instance.activeSelf);
        }

        [Test]
        public void Spawn_PlacesTheInstance()
        {
            GameObject prefab = NewPrefab();
            Vector3 position = new Vector3(1f, 2f, 3f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, 90f);

            GameObject instance = ObjectPool.Spawn(prefab, position, rotation);

            Assert.AreEqual(position.x, instance.transform.position.x, 0.0001f);
            Assert.AreEqual(position.y, instance.transform.position.y, 0.0001f);
            Assert.AreEqual(position.z, instance.transform.position.z, 0.0001f);
            Assert.AreEqual(0f, Quaternion.Angle(rotation, instance.transform.rotation), 0.01f);
        }

        [Test]
        public void Despawn_DeactivatesRatherThanDestroys()
        {
            GameObject prefab = NewPrefab();
            GameObject instance = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            ObjectPool.Despawn(instance);

            Assert.IsTrue(instance != null, "a pooled instance must survive despawning");
            Assert.IsFalse(instance.activeSelf);
        }

        [Test]
        public void Spawn_AfterDespawn_ReusesTheSameInstance()
        {
            // The whole point: no allocation, so no garbage, so no collection spike.
            GameObject prefab = NewPrefab();
            GameObject first = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            ObjectPool.Despawn(first);
            GameObject second = ObjectPool.Spawn(prefab, Vector3.one, Quaternion.identity);

            Assert.AreSame(first, second);
            Assert.IsTrue(second.activeSelf);
        }

        [Test]
        public void Spawn_WhileAllInstancesAreLive_CreatesAnother()
        {
            GameObject prefab = NewPrefab();

            GameObject first = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            GameObject second = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            Assert.AreNotSame(first, second, "a live instance must never be handed out twice");
        }

        [Test]
        public void Spawn_KeepsPrefabsApart()
        {
            GameObject bullet = NewPrefab("Bullet");
            GameObject explosion = NewPrefab("Explosion");

            GameObject spawnedBullet = ObjectPool.Spawn(bullet, Vector3.zero, Quaternion.identity);
            ObjectPool.Despawn(spawnedBullet);

            GameObject spawnedExplosion = ObjectPool.Spawn(explosion, Vector3.zero, Quaternion.identity);

            Assert.AreNotSame(spawnedBullet, spawnedExplosion,
                "an idle bullet must not be handed out as an explosion");
        }

        [Test]
        public void Spawn_SkipsInstancesDestroyedBehindThePool()
        {
            // A scene reload, or code that still calls Destroy on a projectile,
            // leaves dead references in the bucket. Handing one back would throw
            // on first use.
            GameObject prefab = NewPrefab();
            GameObject instance = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            ObjectPool.Despawn(instance);

            Object.DestroyImmediate(instance);

            GameObject replacement = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            Assert.IsTrue(replacement != null);
            Assert.IsTrue(replacement.activeSelf);
        }

        [Test]
        public void Despawn_Twice_DoesNotQueueTheInstanceTwice()
        {
            // Two despawns in one frame are plausible - a bullet can be absorbed
            // on the same frame its lifetime expires. If both queued, the pool
            // would hand the same object to two callers at once.
            GameObject prefab = NewPrefab();
            GameObject instance = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            ObjectPool.Despawn(instance);
            ObjectPool.Despawn(instance);

            GameObject first = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            GameObject second = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            Assert.AreSame(instance, first);
            Assert.AreNotSame(first, second);
        }

        [Test]
        public void Warm_MakesLaterSpawnsReuse()
        {
            GameObject prefab = NewPrefab();

            ObjectPool.Warm(prefab, 2);
            GameObject first = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            GameObject second = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            Assert.IsTrue(first.activeSelf);
            Assert.IsTrue(second.activeSelf);
            Assert.AreNotSame(first, second);
        }

        [Test]
        public void Warm_LeavesInstancesInactive()
        {
            GameObject prefab = NewPrefab();

            ObjectPool.Warm(prefab, 1);

            GameObject root = GameObject.Find("Object Pool");
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.transform.childCount);
            Assert.IsFalse(root.transform.GetChild(0).gameObject.activeSelf,
                "a warmed instance must not be live in the scene");
        }

        [Test]
        public void Warm_CalledRepeatedly_TopsUpRatherThanGrows()
        {
            // BossEmitter warms on every spawn, so this must not compound.
            GameObject prefab = NewPrefab();

            ObjectPool.Warm(prefab, 3);
            ObjectPool.Warm(prefab, 3);
            ObjectPool.Warm(prefab, 3);

            GameObject root = GameObject.Find("Object Pool");
            Assert.AreEqual(3, root.transform.childCount);
        }

        [Test]
        public void Warm_AfterInstancesAreTaken_RefillsToTheTarget()
        {
            GameObject prefab = NewPrefab();
            ObjectPool.Warm(prefab, 2);
            ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            ObjectPool.Warm(prefab, 2);

            GameObject root = GameObject.Find("Object Pool");
            Assert.AreEqual(3, root.transform.childCount,
                "one instance is live, so topping back up to two spares means three in total");
        }

        [Test]
        public void Warm_WithNullPrefab_DoesNothing()
        {
            Assert.DoesNotThrow(() => ObjectPool.Warm(null, 4));
        }

        [Test]
        public void Clear_DropsIdleInstances()
        {
            GameObject prefab = NewPrefab();
            GameObject first = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
            ObjectPool.Despawn(first);

            ObjectPool.Clear();
            GameObject second = ObjectPool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            Assert.AreNotSame(first, second, "instances from a previous run must not carry over");
        }

        [Test]
        public void Spawn_WithNullPrefab_WarnsAndReturnsNull()
        {
            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Warning, new System.Text.RegularExpressions.Regex("null prefab"));

            Assert.IsNull(ObjectPool.Spawn(null, Vector3.zero, Quaternion.identity));
        }

        [Test]
        public void Despawn_WithNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ObjectPool.Despawn(null));
        }
    }
}
