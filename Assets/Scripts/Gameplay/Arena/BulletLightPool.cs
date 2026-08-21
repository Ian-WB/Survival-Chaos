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
    /// and only a few of those cast shadows. Nobody counts the lights; they read
    /// the fact that the bullets are lit.
    ///
    /// The shadow casters are picked one per volley rather than by distance
    /// alone. A volley is not a loose group that drifts apart: Player.FireLine
    /// stacks its bullets up the pivot and every one of them orbits the same
    /// centre at the same angular speed at its own fixed height, so a six-shot
    /// volley is a rigid column about two units tall for the whole of its
    /// flight. Six lights of range ten strung up two units cover very nearly the
    /// same ground, and shadowing all six was six cubemap renders of one place.
    ///
    /// Picking by distance chose exactly those six, because the bullets nearest
    /// the camera are overwhelmingly the newest volley. So the shadow cost used
    /// to scale with Shot Upgrade - the pick that makes the gun feel good was
    /// the pick that made the frame expensive. One per volley inverts that: the
    /// more bullets a shot puts out, the fewer distinct volleys sit among the
    /// lit ones, and the fewer shadows are drawn.
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
        [Range(0, 4)]
        [Tooltip("At most how many volleys cast a shadow, nearest first - not how many bullets. " +
                 "One shot's worth of bullets shares a single shadow however many of them there " +
                 "are. Each caster is a point light, so it costs six scene renders per frame: " +
                 "measured on this scene, one is about 165k triangles and 85 draw calls.")]
        private int shadowCastingCount = 1;

        [Header("Placement")]
        [SerializeField]
        [Tooltip("Lifts the light off the bullet so it does not sit inside its own mesh.")]
        private Vector3 offset = Vector3.zero;

        private Light[] pool;
        private Transform[] poolTransforms;
        private Camera view;

        // Which volleys already have a shadow this frame. An array and a count
        // rather than a HashSet: it holds at most shadowCastingCount entries, and
        // at that size a linear scan beats hashing and allocates nothing.
        private int[] volleysCovered;
        private int volleysCoveredCount;

        private readonly List<ShootScript> bullets = new List<ShootScript>();

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
            volleysCovered = new int[Mathf.Max(1, count)];

            // The template itself stays in the scene as the reference copy; it is
            // switched off so it does not light anything on its own.
            lightTemplate.gameObject.SetActive(false);

            for (int i = 0; i < count; i++)
            {
                Light copy = Instantiate(lightTemplate, transform);
                copy.name = $"Bullet Light {i + 1}";
                copy.gameObject.SetActive(false);

                // Off to start with. Which lights cast is decided per frame from
                // the volleys actually in flight, so there is no correct answer
                // to bake in here - only a starting point that LateUpdate
                // corrects before anything renders.
                copy.shadows = LightShadows.None;

                pool[i] = copy;
                poolTransforms[i] = copy.transform;
            }
        }

        /// <summary>
        /// How many volleys may cast this frame.
        ///
        /// On the bottom two tiers the answer is always none. Neither of them
        /// says so through HDRP's shadow request budget: all four tiers ship
        /// maxShadowRequests 128, and 0 is the one value that must never go
        /// there. HDShadowManager.InitShadowManager does check for zero, and
        /// returns before allocating its atlas; a later path dereferences that
        /// atlas with no guard of its own, so the tier throws every frame and
        /// draws nothing at all. Clear() one method below guards with the same
        /// check, which is how you can tell HDRP knows the state exists and one
        /// path simply forgets it.
        ///
        /// QualitySettings.shadows carries the intent instead. HDRP does not read
        /// it for its own rendering, which is precisely what makes it usable
        /// here: this does not have to know the pipeline exists, and it cannot
        /// break the pipeline by saying so.
        ///
        /// Asked of QualitySettings rather than compared against a tier index,
        /// for the reason GraphicsOption records beside the player-facing note -
        /// two tiers disable shadows rather than one, and a tier inserted below
        /// would move the answer again.
        ///
        /// Read every frame rather than watched for changes, which is what the
        /// old RefreshShadows did. There is nothing left to watch: the casting
        /// set turns over as volleys are fired and expire, so it is rebuilt each
        /// frame anyway and the quality veto rides along in the same pass.
        /// </summary>
        private int ShadowBudget()
        {
            if (QualitySettings.shadows == ShadowQuality.Disable)
            {
                return 0;
            }

            return Mathf.Clamp(shadowCastingCount, 0, pool.Length);
        }

        /// <summary>
        /// Records <paramref name="volley"/> as having a shadow, and reports
        /// whether it was new.
        /// </summary>
        private bool ClaimVolley(int volley)
        {
            for (int i = 0; i < volleysCoveredCount; i++)
            {
                if (volleysCovered[i] == volley)
                {
                    return false;
                }
            }

            volleysCovered[volleysCoveredCount++] = volley;
            return true;
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

            int budget = ShadowBudget();
            volleysCoveredCount = 0;

            for (int i = 0; i < pool.Length; i++)
            {
                bool used = i < bullets.Count;

                if (used)
                {
                    poolTransforms[i].position = bullets[i].Body.position + offset;
                }

                // Nearest first, one per volley, until the budget runs out. The
                // second and later bullets of a volley still get a light - those
                // are cheap - and no shadow, because the first one's already
                // covers the same couple of units.
                bool casts = used
                    && volleysCoveredCount < budget
                    && ClaimVolley(bullets[i].Volley);

                LightShadows wanted = casts ? lightTemplate.shadows : LightShadows.None;
                if (pool[i].shadows != wanted)
                {
                    pool[i].shadows = wanted;
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
        ///
        /// Reads the projectiles' own registry rather than calling
        /// GameObject.FindGameObjectsWithTag, which allocated a fresh array of
        /// every tagged object on every frame. Nothing here allocates now: the
        /// shortlist is a field, and the registry is maintained by the
        /// projectiles as they come and go.
        /// </summary>
        private void CollectNearestBullets(int limit)
        {
            bullets.Clear();

            IReadOnlyList<ShootScript> found = ShootScript.Live;
            if (found.Count == 0)
            {
                return;
            }

            Vector3 eye = view.transform.position;

            // Partial selection: for each bullet, insert it into the running
            // shortlist if it beats the worst entry. Cheaper than sorting the
            // whole set when only a handful are wanted.
            for (int i = 0; i < found.Count; i++)
            {
                ShootScript candidate = found[i];

                // The registry carries the player's projectiles and the enemies'
                // alike; only the player's are lit. A destroyed entry can survive
                // a route that skipped OnDisable, so that is checked too.
                if (candidate == null || !candidate.CompareTag(bulletTag))
                {
                    continue;
                }

                float distance = (candidate.Body.position - eye).sqrMagnitude;

                int slot = bullets.Count;
                while (slot > 0 &&
                       (bullets[slot - 1].Body.position - eye).sqrMagnitude > distance)
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
