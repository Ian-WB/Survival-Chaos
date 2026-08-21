using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Keeps the player inside the playable band.
    ///
    /// Only height is clamped. The bounds box is two-dimensional and this used to
    /// clamp world X from it as well, which is meaningless for a craft that orbits
    /// the arena axis: X is not a limit there, it is where you are around the ring,
    /// and clamping it would pin the ship at two points on the circle and fight
    /// PlayerMovement.RotateAround. It never fired only because the authored box is
    /// 53.9 wide against an orbit radius of 13.72 - so it was a trap waiting for
    /// someone to narrow the box, rather than a feature.
    /// </summary>
    public class ApplyBounds : MonoBehaviour
    {
        [Header("Bounds")]
        [SerializeField]
        [Tooltip("Box whose vertical extent is the player's travel limit. Its width is not used.")]
        private BoxCollider2D playerBounds;

        private bool warned;

        /// <summary>
        /// The band the player is held inside, in world space. False when no box
        /// is assigned - the same condition <see cref="applyBounds"/> warns about.
        ///
        /// Exposed so a wave can be checked against what the player can actually
        /// reach. The box is the authority on that limit, and anything that kept
        /// its own copy of the two numbers would be one more pair to keep in step
        /// with it.
        /// </summary>
        public bool TryGetBand(out float floor, out float ceiling)
        {
            if (playerBounds == null)
            {
                floor = 0f;
                ceiling = 0f;
                return false;
            }

            Bounds box = playerBounds.bounds;
            floor = box.min.y;
            ceiling = box.max.y;
            return true;
        }

        void Update()
        {
            applyBounds();
        }

        private void applyBounds()
        {
            if (playerBounds == null)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning(
                        "ApplyBounds has no bounds box assigned, so the player is not being " +
                        "held inside the arena.", this);
                }

                return;
            }

            // Collider2D.bounds is already a world-space box with the collider's
            // offset and its transform folded in, so min and max are the limits
            // directly. The previous form added the offset and position on top of
            // extents by hand, which came to the same answer only while the collider
            // was unrotated and unscaled.
            Bounds box = playerBounds.bounds;

            Vector3 position = transform.position;
            position.y = Mathf.Clamp(position.y, box.min.y, box.max.y);
            transform.position = position;
        }
    }
}
