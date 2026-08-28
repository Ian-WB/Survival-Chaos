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
    /// Enemies are pooled too, which they were not at first. The argument
    /// against was that they spawn once or twice a second, so resetting health,
    /// invoke timers and movement state was risk for no gain. The first half of
    /// that was measured against the wave asset and only holds early: nineteen
    /// streams each ramp their own interval and the curve compounds, giving 1.3
    /// spawns a second at four minutes, 4.1 at eight, and 7.5 by the ten minute
    /// mark - which is also when the most bullets, effects and lights are
    /// competing for the frame.
    ///
    /// Those three figures fell at two, four and five minutes when this was
    /// written, because the run ended at five. Doubling it to 602s re-stretched
    /// the curve rather than extending it, so the numbers are unchanged and only
    /// their timestamps moved.
    ///
    /// The second half was accurate, and the three pieces of state it named are
    /// exactly the three that had to be handled: health rebuilds in OnEnable,
    /// Enemy_1 cancels its firing invoke before re-arming it, and EnemyMovement
    /// restores the travel direction its prefab was authored with - that one is
    /// rewritten every frame by the chase, so a reused enemy would otherwise set
    /// off whichever way it was last turned.
    /// </summary>
    public static class ObjectPool
    {
        private static readonly Dictionary<GameObject, Stack<GameObject>> idle =
            new Dictionary<GameObject, Stack<GameObject>>();

        private static Transform root;

        private static PoolMotionReset motionReset;

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

                // Only reused instances need this. A fresh clone has no previous
                // position to report motion from, so it cannot smear.
                pooled.SuppressMotionForOneFrame();
                MotionReset.Register(pooled);
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
        /// Builds the pool up to <paramref name="idleCount"/> spare instances,
        /// so the opening volley does not pay to create them mid-fight.
        ///
        /// Tops up rather than adds, so calling it repeatedly is harmless - the
        /// boss warms itself every time one spawns.
        /// </summary>
        public static void Warm(GameObject prefab, int idleCount)
        {
            if (prefab == null || idleCount <= 0)
            {
                return;
            }

            Stack<GameObject> bucket = Bucket(prefab);

            while (bucket.Count < idleCount)
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
            // Belongs to the root that just went, so it has to go with it or the
            // next spawn registers against a component on a destroyed object.
            motionReset = null;
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
        /// The one component that puts motion vectors back, hosted on the pool's
        /// own root so it appears and disappears with the pool.
        /// </summary>
        private static PoolMotionReset MotionReset
        {
            get
            {
                if (motionReset == null)
                {
                    Transform host = Root;

                    // Explicit null comparison rather than ??, because Unity
                    // overloads == on Object to report a destroyed object as null
                    // and the null-coalescing operator does not go through that
                    // overload. GetComponent hands back a real null when nothing is
                    // there, so ?? happens to work today - but it is the one place
                    // in the project reaching past a lifetime check that three other
                    // files document avoiding, and it would start returning a corpse
                    // the moment this stopped being a freshly built root.
                    PoolMotionReset existing = host.GetComponent<PoolMotionReset>();
                    motionReset = existing != null
                        ? existing
                        : host.gameObject.AddComponent<PoolMotionReset>();
                }

                return motionReset;
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
