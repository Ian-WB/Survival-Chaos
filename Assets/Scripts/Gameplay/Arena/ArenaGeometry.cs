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
        public const float OrbitRadius = 137.2f;

        /// <summary>
        /// Eases a point toward the orbit circle, keeping its bearing around the
        /// axis and its height, at a rate independent of frame rate.
        ///
        /// The lane is the whole of what makes a shot dangerous in this arena.
        /// Projectiles orbit with RotateAround about the vertical axis, which
        /// preserves whatever distance from the axis they were born at - exactly,
        /// and for their whole life. So a shot fired from a barrel that is not on
        /// the lane never reaches it, and never can, however long it flies.
        ///
        /// Radius only. Bearing and height are the shot's own business: this is
        /// not steering, and a projectile that closed on the player's height as
        /// well would be a homing missile rather than a bullet in a lane.
        /// </summary>
        /// <param name="response">
        /// Higher converges faster. Follows ShipMotion's convention, so 0 or less
        /// snaps to the lane immediately - which is almost never what a caller
        /// wants here, and is why the ones that can be switched off test for it
        /// themselves rather than passing zero through.
        /// </param>
        public static Vector3 EaseOntoOrbit(
            Vector3 position, Vector3 center, float radius, float response, float deltaTime)
        {
            Vector3 offset = position - center;
            offset.y = 0f;

            float eased = ShipMotion.Approach(
                offset.magnitude, Mathf.Max(0f, radius), response, deltaTime);

            return ProjectOntoOrbit(position, center, eased);
        }

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
