using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Retires an object a fixed time after it appears.
    ///
    /// Objects the pool created go back to it; anything else is destroyed, which is
    /// what this always used to do. Enemies carry this component too, and they come
    /// through the pool now like the rounds - WaveDirector spawns them with
    /// ObjectPool - so the destroy path is left for anything placed in a scene by
    /// hand or spawned around the pool.
    /// </summary>
    public class DestroyAfterTime : MonoBehaviour
    {
        [SerializeField]
        private float delay = 10f;

        /// <summary>
        /// Arms on every spawn, including reuse from the pool. This was Awake(),
        /// which only runs on an object's first life - a reused projectile would
        /// have flown on forever.
        /// </summary>
        private void OnEnable()
        {
            Invoke(nameof(Retire), delay);
        }

        /// <summary>
        /// Disarms the timer. Without this a projectile that died early - by hitting
        /// something - would carry its pending timer into its next life and vanish
        /// partway through it.
        /// </summary>
        private void OnDisable()
        {
            CancelInvoke(nameof(Retire));
        }

        /// <summary>
        /// Moves this life's end to <paramref name="seconds"/> from now, for
        /// something that learns how long it should last only after it has
        /// appeared - a boss torpedo, which is fired from the same prefab as a
        /// round that lives far less long. The next spawn starts again from the
        /// authored delay.
        /// </summary>
        public void RetireIn(float seconds)
        {
            CancelInvoke(nameof(Retire));
            Invoke(nameof(Retire), Mathf.Max(0f, seconds));
        }

        private void Retire()
        {
            ObjectPool.Despawn(gameObject);
        }
    }
}
