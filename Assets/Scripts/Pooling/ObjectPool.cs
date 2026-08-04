using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalChaos
{
    /// <summary>
    /// Reuses projectiles and hit effects instead of allocating them.
    ///
    /// At peak the player has ~36 projectiles alive and the boss ~174, each
    /// previously created with Instantiate and released with Destroy. That churn
    /// does not cost frame rate - the game runs well clear of budget - but it
    /// produces garbage, and the collections show up as periodic spikes against
    /// an otherwise flat frame time. A hitch is more noticeable than a lower
    /// steady frame rate, which is the whole reason this exists.
    ///
    /// Enemies are deliberately not pooled: they spawn once or twice a second,
    /// and reusing one would mean resetting health, invoke timers and movement
    /// state - all risk for no measurable gain.
    /// </summary>
    public static class ObjectPool
    {
        private static readonly Dictionary<GameObject, Stack<GameObject>> idle =
            new Dictionary<GameObject, Stack<GameObject>>();

        private static Transform root;

        /// <summary>
        /// Takes an instance from the pool, or creates one if the pool is empty.
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogWarning("ObjectPool.Spawn called with a null prefab.");
                return null;
            }

            GameObject instance = TakeIdle(prefab);
            bool reused = instance != null;

            if (!reused)
            {
                instance = Object.Instantiate(prefab, Root);
                instance.AddComponent<PooledInstance>().Source = prefab;
            }

            // Set the transform before activating, so OnEnable on the instance
            // sees its final position rather than wherever it last died.
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            if (reused && instance.TryGetComponent(out PooledInstance pooled))
            {
                pooled.Replay();
            }

            return instance;
        }

        /// <summary>
        /// Returns an instance to its pool. Objects the pool did not create are
        /// destroyed instead, so this is safe to call on anything.
        /// </summary>
        public static void Despawn(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (!instance.TryGetComponent(out PooledInstance pooled) || pooled.Source == null)
            {
                Object.Destroy(instance);
                return;
            }

            if (!instance.activeSelf)
            {
                return; // already idle; ignore a double despawn
            }

            instance.SetActive(false);
            instance.transform.SetParent(Root, worldPositionStays: false);
            Bucket(pooled.Source).Push(instance);
        }

        /// <summary>
        /// Creates instances up front so the first volley of a fight does not
        /// pay for them mid-frame.
        /// </summary>
        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            Stack<GameObject> bucket = Bucket(prefab);

            for (int i = 0; i < count; i++)
            {
                GameObject instance = Object.Instantiate(prefab, Root);
                instance.AddComponent<PooledInstance>().Source = prefab;
                instance.SetActive(false);
                bucket.Push(instance);
            }
        }

        /// <summary>Drops every bucket. Instances themselves are not destroyed.</summary>
        public static void Clear()
        {
            idle.Clear();
            root = null;
        }

        private static GameObject TakeIdle(GameObject prefab)
        {
            if (!idle.TryGetValue(prefab, out Stack<GameObject> bucket))
            {
                return null;
            }

            // Entries can be destroyed out from under the pool - by a scene
            // reload, or by code that called Destroy on a pooled object. Skip
            // any that have gone, rather than handing back a dead reference.
            while (bucket.Count > 0)
            {
                GameObject candidate = bucket.Pop();
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Stack<GameObject> Bucket(GameObject prefab)
        {
            if (!idle.TryGetValue(prefab, out Stack<GameObject> bucket))
            {
                bucket = new Stack<GameObject>();
                idle.Add(prefab, bucket);
            }

            return bucket;
        }

        private static Transform Root
        {
            get
            {
                if (root == null)
                {
                    // Identity transform at the origin, so an instance's local
                    // position equals its world position and spawning never has
                    // to reparent.
                    root = new GameObject("Object Pool").transform;
                }

                return root;
            }
        }

        /// <summary>
        /// Static state outlives a scene, and outlives play mode entirely when
        /// domain reload is disabled, so both are cleared explicitly.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Clear();
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            Clear();
        }
    }
}
