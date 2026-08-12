using UnityEngine;
using SurvivalChaos;

/// <summary>
/// The boss's laser trigger volume: the laser attack fires only while the player
/// is inside it.
///
/// This used to be one line - <c>boss.LaserActive = other.CompareTag("Player")</c>
/// in OnTriggerEnter - which got both halves wrong. Anything that was not the
/// player entering the volume, a projectile or another enemy, switched the laser
/// off; and with no exit handler the player leaving never switched it off at all.
/// BossEmitter.LaserActive documents itself as being set "as the player enters
/// and leaves", and only one of those was happening.
/// </summary>
public class ColliderScript_3 : MonoBehaviour
{
    public GameObject EnemyShip;

    private void OnTriggerEnter(Collider other)
    {
        SetLaser(other, active: true);
    }

    private void OnTriggerExit(Collider other)
    {
        SetLaser(other, active: false);
    }

    /// <summary>
    /// The laser also has to stop if the volume itself goes away mid-fight -
    /// otherwise it would be left latched on by the last enter it saw.
    /// </summary>
    private void OnDisable()
    {
        if (EnemyShip != null && EnemyShip.TryGetComponent(out BossEmitter boss))
        {
            boss.LaserActive = false;
        }
    }

    private void SetLaser(Collider other, bool active)
    {
        // Only the player decides this. Anything else crossing the volume is not
        // an answer to "is the player in the beam".
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (EnemyShip == null)
        {
            Debug.LogWarning(
                "ColliderScript_3 has no Enemy Ship assigned, so the boss laser will never fire.",
                this);
            return;
        }

        if (EnemyShip.TryGetComponent(out BossEmitter boss))
        {
            boss.LaserActive = active;
        }
    }
}
