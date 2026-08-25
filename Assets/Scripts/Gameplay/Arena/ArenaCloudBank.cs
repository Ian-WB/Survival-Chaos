using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos
{
    /// <summary>
    /// Drifts a Local Volumetric Fog volume's density mask across the arena, so
    /// banks of cloud pass through the island every so often.
    ///
    /// The sky's volumetric clouds cannot do this. HDRP will not render them
    /// within about twenty units of the camera, their altitude range has a hard
    /// 100m floor - five times the height of the island - and their noise runs
    /// at kilometre scale, so across 37 units of arena the field is uniform and
    /// the sky simply turns overcast as a whole. Local Volumetric Fog is the
    /// local equivalent, and this drives it.
    ///
    /// The mask is scrolled rather than the volume moved. HDRP samples the mask
    /// in the volume's own space, so scrolling a tiling texture gives endless
    /// cloud while the box stays put - move the box instead and its edges sweep
    /// through frame once a lap.
    ///
    /// HDRP measures scrolling in mask-UV per second, which is not a figure
    /// anyone can picture and which silently changes meaning whenever the volume
    /// is resized. driftSpeed here is world units per second, converted using
    /// the volume's own size and tiling, so it keeps meaning what it says.
    /// </summary>
    [RequireComponent(typeof(LocalVolumetricFog))]
    [ExecuteAlways]
    public sealed class ArenaCloudBank : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("How fast the banks travel, in world units per second. At 2.5 a bank crosses " +
                 "the 37 unit island in about fifteen seconds.")]
        private float driftSpeed = 2.5f;

        [SerializeField]
        [Range(0f, 360f)]
        [Tooltip("Bearing the banks travel towards, in degrees: 0 is +Z, 90 is +X. Match this " +
                 "to the sky's cloud wind orientation so both layers agree.")]
        private float driftBearing = 90f;

        private LocalVolumetricFog fog;

        /// <summary>Direction the banks travel, in world space.</summary>
        public Vector3 DriftDirection
        {
            get
            {
                float radians = driftBearing * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            }
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        /// <summary>
        /// Pushes the drift onto the fog volume. Only needs calling when something
        /// changes - HDRP integrates the scrolling speed over time itself, so there
        /// is no reason to touch this every frame.
        /// </summary>
        [ContextMenu("Apply Drift")]
        public void Apply()
        {
            if (fog == null)
            {
                fog = GetComponent<LocalVolumetricFog>();
            }

            if (fog == null)
            {
                return;
            }

            Vector3 velocity = DriftDirection * driftSpeed;
            Vector3 size = fog.parameters.size;
            Vector3 tiling = fog.parameters.textureTiling;

            // Negated because HDRP adds the scroll to the sample coordinate: walking
            // the lookup one way slides the pattern the other.
            fog.parameters.textureScrollingSpeed = new Vector3(
                -PerSecondInUv(velocity.x, tiling.x, size.x),
                0f,
                -PerSecondInUv(velocity.z, tiling.z, size.z));
        }

        private static float PerSecondInUv(float worldPerSecond, float tiling, float size)
        {
            return Mathf.Approximately(size, 0f) ? 0f : worldPerSecond * tiling / size;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position;
            Vector3 direction = DriftDirection;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + direction * 10f);

            // Arrow head, so the bearing is readable at a glance.
            Vector3 tip = origin + direction * 10f;
            Vector3 side = Vector3.Cross(direction, Vector3.up) * 1.5f;
            Gizmos.DrawLine(tip, tip - direction * 3f + side);
            Gizmos.DrawLine(tip, tip - direction * 3f - side);
        }
    }
}
