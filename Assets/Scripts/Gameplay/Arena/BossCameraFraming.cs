using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Gives the boss more screen space without changing the shared orbit angle
    /// or the player's aiming height. Runs after movement and preserves both.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class BossCameraFraming : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float extraDistance = 22f;
        [SerializeField, Min(0.1f)] private float response = 3f;

        private Transform center;
        private float normalRadius;
        private float offset;

        private void Awake()
        {
            GameObject scenario = GameObject.FindWithTag("Scenario");
            center = scenario != null ? scenario.transform : null;

            if (center != null)
            {
                Vector3 radial = transform.position - center.position;
                radial.y = 0f;
                normalRadius = radial.magnitude;
            }
        }

        private void LateUpdate()
        {
            if (center == null || PauseMenu.GameIsPaused)
            {
                return;
            }

            BossEmitter boss = BossEmitter.Active;
            bool fighting = boss != null && boss.isActiveAndEnabled && !RunOutcome.RunEnded;
            float target = fighting ? extraDistance : 0f;
            // Restore behind the end screen too, even though gameplay time stops.
            offset = ShipMotion.Approach(offset, target, response, Time.unscaledDeltaTime);
            ApplyRadius(normalRadius + offset);
        }

        private void OnDisable()
        {
            offset = 0f;
            ApplyRadius(normalRadius);
        }

        private void ApplyRadius(float radius)
        {
            if (center != null)
            {
                transform.position = ArenaGeometry.ProjectOntoOrbit(
                    transform.position, center.position, radius);
            }
        }
    }
}
