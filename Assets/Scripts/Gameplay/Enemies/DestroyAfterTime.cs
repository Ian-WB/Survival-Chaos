using UnityEngine;
using SurvivalChaos;

/// <summary>
/// Retires an object a fixed time after it appears.
///
/// Objects the pool created go back to it; anything else is destroyed, which is
/// what this always used to do. Enemies carry this component too and are not
/// pooled, so both paths are live.
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

    private void Retire()
    {
        ObjectPool.Despawn(gameObject);
    }
}
