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

        [Header("Delay")]
        [SerializeField]
        [Range(0f, 10f)]
        private float initialDelay = 1f;

        [SerializeField]
        [Range(0f, 10f)]
        private float spawnDelay = 1;

        private EnemyMovement movement;

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
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(Shoot));
        }

        private void Shoot()
        {
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
            ObjectPool.Spawn(bullet, shootPivot_1.position, Quaternion.Euler(0f, 0f, 90f));
        }
    }
}
