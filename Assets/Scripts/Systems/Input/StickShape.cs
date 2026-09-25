using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Reshapes a stick's reading from the circle it travels in to the square
    /// the keys cover, with no Unity object attached so it can be tested.
    ///
    /// The ship's two axes are separate speeds - round the ring and up or down -
    /// and each runs at full speed when its input reads 1. The keys and the d-pad
    /// give 1 on both axes when two are held. A stick cannot: its gate and the
    /// stickDeadzone processor both hold its reading to a circle, so pushed all
    /// the way into a corner it reads about 0.71 on each axis, and the ship went
    /// round the ring and climbed at 71% of the speed the same move on the keys
    /// gets. Reported from a pad on 25 September 2026: a diagonal felt far slower
    /// than a straight push.
    ///
    /// Stretched along its own direction, so the larger axis reads how far the
    /// stick is pushed. A push straight along an axis is untouched, a full push
    /// into a corner reads 1 on both, a half push reads half on both, and the
    /// direction never changes - only how far out along it the reading sits.
    /// </summary>
    public static class StickShape
    {
        public static Vector2 ToSquare(Vector2 stick)
        {
            float larger = Mathf.Max(Mathf.Abs(stick.x), Mathf.Abs(stick.y));

            if (larger <= 0f)
            {
                return Vector2.zero;
            }

            return stick * (stick.magnitude / larger);
        }
    }
}
