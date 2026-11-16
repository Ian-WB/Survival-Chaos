using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The enemy that shoots: the Enemy and Enemy 1 prefabs. Health, hits, death
    /// and the debug menu's exits are shared with the other enemies in
    /// <see cref="EnemyBase"/>; this adds the guns.
    /// </summary>
    public class Enemy_1 : EnemyBase
    {
        [SerializeField]
        private GameObject childObject;

        [Header("Shoot")]
        [SerializeField]
        private Transform shootPivot;

        [SerializeField]
        private Transform shootPivot_1;

        [SerializeField]
        private GameObject shootPrefab;

        [SerializeField]
        private GameObject shootPrefab1;

        [SerializeField, Range(1, 2)]
        [Tooltip("Rounds in a volley. 2 fires one from each pivot, as both ships did until " +
                 "10 November 2026; 1 fires from the first pivot only, which is the Fighter now.")]
        private int roundsPerVolley = 2;

        [Header("Delay")]
        [SerializeField]
        [Range(0f, 10f)]
        private float initialDelay = 1f;

        [SerializeField]
        [Range(0f, 10f)]
        private float spawnDelay = 1;

        [Header("Tell")]
        [SerializeField]
        [Tooltip("The glow shown on a gun before it fires: BossTellGlow, the Leviathan's. Left " +
                 "empty, the gun fires unannounced, as it did before 16 November 2026.")]
        private Mesh tellMesh;

        [SerializeField]
        [Tooltip("Material for the glow: BossMuzzleTell, the thruster shader in the hostile colour.")]
        private Material tellMaterial;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Seconds the glow builds for before each shot. These rounds cost two hit points; " +
                 "this is the warning that one is coming.")]
        private float tellSeconds = 0.3f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Size of the glow at the moment the gun fires, in this ship's own units.")]
        private float tellSize = 0.3f;

        [SerializeField]
        [Tooltip("Brightness of the glow at the moment the gun fires. The Leviathan's is 3.")]
        private float tellBrightness = 3f;

        [Header("Recoil")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("How hard a shot kicks the model back, in units a second. 0 for none.")]
        private float recoil = 2.5f;

        private EnemyMovement movement;
        private EnemySpaceShip model;

        private Renderer[] tells;
        private MaterialPropertyBlock tellBlock;

        /// <summary>When the next volley leaves, in game time, for the glow to count down to.</summary>
        private float nextShotAt = float.PositiveInfinity;

        protected override int FallbackReward => 15;

        protected override void Awake()
        {
            base.Awake();

            // Once is enough: the reference is to a child of this same prefab, so it
            // survives a trip through the pool.
            if (Ship != null)
            {
                Ship.TryGetComponent(out movement);
            }

            model = GetComponentInChildren<EnemySpaceShip>();
            BuildTells();
        }

        /// <summary>
        /// One glow on each gun that fires. In Awake, so the pool counts them
        /// among this ship's renderers the first time it spawns it.
        /// </summary>
        private void BuildTells()
        {
            if (tellMesh == null || tellMaterial == null || tellSeconds <= 0f)
            {
                return;
            }

            bool second = roundsPerVolley > 1 && shootPivot_1 != null && shootPivot_1 != shootPivot;

            tells = new Renderer[second ? 2 : 1];
            tells[0] = ShipFlare.Build(
                shootPivot, "Gun Tell", tellMesh, tellMaterial, Vector3.zero, Quaternion.identity);

            if (second)
            {
                tells[1] = ShipFlare.Build(
                    shootPivot_1, "Gun Tell", tellMesh, tellMaterial, Vector3.zero, Quaternion.identity);
            }

            tellBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// The glow, building over the last <see cref="tellSeconds"/> before a
        /// shot on the curve the Leviathan's muzzles use, so the moment it
        /// peaks is the moment the gun fires.
        /// </summary>
        private void Update()
        {
            if (tells == null)
            {
                return;
            }

            float until = nextShotAt - Time.time;
            float level = until < tellSeconds && !ArenaIntroDirector.IsPlaying
                ? VolleyTell.Together(1f - until / tellSeconds)
                : 0f;

            SetTells(level);
        }

        private void SetTells(float level)
        {
            if (tells == null)
            {
                return;
            }

            for (int i = 0; i < tells.Length; i++)
            {
                ShipFlare.Set(tells[i], tellBlock, Vector3.one * (tellSize * level), tellBrightness * level);
            }
        }

        /// <summary>
        /// Arms the guns at the start of every life, after the shared reset.
        ///
        /// The firing invoke is cancelled before it is armed because a repeat left
        /// over from the previous life would stack: two invokes, then three, and an
        /// enemy that fires faster the more times it has been recycled.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            CancelInvoke(nameof(Shoot));
            InvokeRepeating(nameof(Shoot), initialDelay, spawnDelay);
            nextShotAt = Time.time + initialDelay;
            SetTells(0f);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(Shoot));
        }

        private void Shoot()
        {
            nextShotAt = Time.time + spawnDelay;

            // Cached and guarded: this fires on a repeating invoke, so an unguarded
            // lookup would throw for the whole lifetime of a mis-wired prefab.
            //
            // The pivots needed the same guard and did not have it. The movement
            // check below only decides which prefab travels which way; the pivots
            // were dereferenced as arguments on either side of it, so a prefab
            // missing one threw every spawnDelay for as long as that enemy lived -
            // and pooling means it comes back. Player.FireLine refuses the same way.
            // The prefabs are left to ObjectPool.Spawn, which already warns and
            // returns on a null one.
            if (shootPivot == null || shootPivot_1 == null)
            {
                return;
            }

            GameObject bullet = movement != null && movement.TravellingLeft
                ? shootPrefab
                : shootPrefab1;

            ObjectPool.Spawn(bullet, shootPivot.position, Quaternion.Euler(0f, 0f, 90f));

            if (roundsPerVolley > 1)
            {
                ObjectPool.Spawn(bullet, shootPivot_1.position, Quaternion.Euler(0f, 0f, 90f));
            }

            // Back along the ring from the way the rounds leave.
            if (recoil > 0f && model != null && model.Rig != null)
            {
                bool left = movement != null && movement.TravellingLeft;
                model.Rig.Kick(new Vector3(left ? recoil : -recoil, 0f, 0f));
            }
        }
    }
}
