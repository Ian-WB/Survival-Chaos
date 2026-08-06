using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The shape of the arena, in one place.
    ///
    /// The orbit radius used to be the bare literal 137.2f, written out twice in
    /// EnemyMovement and ObstacleScript, with nothing tying the player's own
    /// distance from the centre to it.
    /// </summary>
    public static class ArenaGeometry
    {
        /// <summary>
        /// Distance from the arena axis that enemies converge to and hold.
        /// Anything meant to share a lane with them belongs at this radius.
        /// </summary>
        public const float OrbitRadius = 13.72f;

        /// <summary>
        /// Moves a point onto the orbit circle, keeping its bearing around the
        /// axis and its height. Used to place the player and camera onto the
        /// same lane the enemies fly in.
        /// </summary>
        public static Vector3 ProjectOntoOrbit(Vector3 position, Vector3 center, float radius)
        {
            Vector3 offset = position - center;
            offset.y = 0f;

            if (offset.sqrMagnitude < 0.000001f)
            {
                // Sitting exactly on the axis, so there is no bearing to keep.
                // -Z is where the player and camera already start.
                offset = Vector3.back;
            }

            Vector3 result = center + offset.normalized * Mathf.Max(0f, radius);
            result.y = position.y;
            return result;
        }
    }
}
