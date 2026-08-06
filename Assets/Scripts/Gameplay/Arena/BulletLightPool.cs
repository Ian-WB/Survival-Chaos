using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Lights the player's bullets using a small fixed pool rather than a light
    /// per projectile.
    ///
    /// A light on every bullet is not affordable here: with maximum shot skills
    /// the player has around 36 alive at once, and the boss adds ~174 more. Each
    /// shadow-casting point light renders the scene six times into a cubemap, so
    /// even a handful of those is expensive - and HDRP silently drops lights once
    /// its shadow atlas is full, which shows up as flickering rather than as a
    /// clean failure.
    ///
    /// Instead a fixed number of lights follow the bullets nearest the camera,
    /// and only the closest few cast shadows. Nobody counts the lights; they read
    /// the fact that the bullets are lit.
    /// </summary>
    public sealed class BulletLightPool : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField]
        [Tooltip("Light to clone for the pool. Configure its colour, intensity, range and shadow " +
                 "settings on this template - the pool copies whatever you set here.")]
        private Light lightTemplate;

        [SerializeField]
        [Tooltip("Tag on the player's projectiles.")]
        private string bulletTag = "Shoot";

        [Header("Budget")]
        [SerializeField]
        [Tooltip("How many lights exist. They follow the bullets closest to the camera.")]
        private int lightCount = 8;

        [SerializeField]
        [Tooltip("How many of those cast shadows, closest first. Each one costs six scene renders " +
                 "per frame, so keep this very low.")]
        private int shadowCastingCount = 1;

        [Header("Placement")]
        [SerializeField]
        [Tooltip("Lifts the light off the bullet so it does not sit inside its own mesh.")]
        private Vector3 offset = Vector3.zero;

        private Light[] pool;
        private Transform[] poolTransforms;
        private Camera view;

        private readonly List<Transform> bullets = new List<Transform>();

        private void Awake()
        {
            if (lightTemplate == null)
            {
                Debug.LogWarning("BulletLightPool has no light template assigned; it will do nothing.", this);
                enabled = false;
                return;
            }

            BuildPool();
        }

        private void BuildPool()
        {
            int count = Mathf.Max(0, lightCount);
            pool = new Light[count];
            poolTransforms = new Transform[count];

            // The template itself stays in the scene as the reference copy; it is
            // switched off so it does not light anything on its own.
            lightTemplate.gameObject.SetActive(false);

            for (int i = 0; i < count; i++)
            {
                Light copy = Instantiate(lightTemplate, transform);
                copy.name = $"Bullet Light {i + 1}";
                copy.gameObject.SetActive(false);

                // Shadows only on the first few. Everything else about the light
                // comes from the template.
                copy.shadows = i < shadowCastingCount ? lightTemplate.shadows : LightShadows.None;

                pool[i] = copy;
                poolTransforms[i] = copy.transform;
            }
        }

        /// <summary>
        /// Runs after movement so the lights land on this frame's bullet
        /// positions rather than last frame's.
        /// </summary>
        private void LateUpdate()
        {
            if (pool == null || pool.Length == 0)
            {
                return;
            }

            if (view == null)
            {
                view = Camera.main;
                if (view == null)
                {
                    return;
                }
            }

            CollectNearestBullets(pool.Length);

            for (int i = 0; i < pool.Length; i++)
            {
                bool used = i < bullets.Count;

                if (used)
                {
                    poolTransforms[i].position = bullets[i].position + offset;
                }

                // SetActive is cheap and keeps unused lights out of HDRP's
                // culling entirely, which matters more than the call itself.
                if (pool[i].gameObject.activeSelf != used)
                {
                    pool[i].gameObject.SetActive(used);
                }
            }
        }

        /// <summary>
        /// Fills <see cref="bullets"/> with at most <paramref name="limit"/>
        /// bullets, nearest to the camera first.
        /// </summary>
        private void CollectNearestBullets(int limit)
        {
            bullets.Clear();

            GameObject[] found = GameObject.FindGameObjectsWithTag(bulletTag);
            if (found.Length == 0)
            {
                return;
            }

            Vector3 eye = view.transform.position;

            // Partial selection: for each bullet, insert it into the running
            // shortlist if it beats the worst entry. Cheaper than sorting the
            // whole set when only a handful are wanted.
            for (int i = 0; i < found.Length; i++)
            {
                Transform candidate = found[i].transform;
                float distance = (candidate.position - eye).sqrMagnitude;

                int slot = bullets.Count;
                while (slot > 0 &&
                       (bullets[slot - 1].position - eye).sqrMagnitude > distance)
                {
                    slot--;
                }

                if (slot < limit)
                {
                    bullets.Insert(slot, candidate);

                    if (bullets.Count > limit)
                    {
                        bullets.RemoveAt(bullets.Count - 1);
                    }
                }
            }
        }
    }
}
