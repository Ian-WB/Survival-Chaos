using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Places this object on the enemy orbit circle at startup, keeping its
    /// bearing and height. On the player this puts it in the same lane the
    /// enemies fly in; on the camera, a radiusOffset keeps it trailing behind
    /// by a fixed distance.
    /// </summary>
    public sealed class SnapToOrbit : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Arena axis to orbit. Leave empty to use the object tagged Scenario.")]
        private Transform center;

        [SerializeField]
        [Tooltip("Added to the arena orbit radius. 0 puts this object exactly on the enemy lane; " +
                 "a positive value sits further out, which is what the camera wants.")]
        private float radiusOffset;

        /// <summary>The radius this object will be placed at.</summary>
        public float TargetRadius => Mathf.Max(0f, ArenaGeometry.OrbitRadius + radiusOffset);

        private void Awake()
        {
            Snap();
        }

        [ContextMenu("Snap Now")]
        public void Snap()
        {
            Transform pivot = ResolveCenter();
            if (pivot == null)
            {
                Debug.LogWarning("SnapToOrbit found no centre: assign one, or tag the arena Scenario.", this);
                return;
            }

            transform.position = ArenaGeometry.ProjectOntoOrbit(
                transform.position,
                pivot.position,
                TargetRadius);
        }

        private Transform ResolveCenter()
        {
            if (center != null)
            {
                return center;
            }

            GameObject scenario = GameObject.FindWithTag("Scenario");
            return scenario != null ? scenario.transform : null;
        }

        private void OnDrawGizmosSelected()
        {
            Transform pivot = ResolveCenter();
            if (pivot == null)
            {
                return;
            }

            Vector3 origin = pivot.position;
            origin.y = transform.position.y;

            Gizmos.color = Color.cyan;

            const int segments = 64;
            Vector3 previous = origin + new Vector3(TargetRadius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = origin + new Vector3(
                    Mathf.Cos(angle) * TargetRadius,
                    0f,
                    Mathf.Sin(angle) * TargetRadius);

                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
