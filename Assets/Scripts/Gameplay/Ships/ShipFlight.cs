using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Which way a ship model's nose and roof point in its own space.
    ///
    /// The models were not authored to one convention: the player's nose is
    /// its +X and the enemies' is their +Z. Everything a ship does is said in
    /// the ship's own words - nose up, bank right, facing - and this is what
    /// turns those into a rotation for a particular model, so no script has to
    /// know which Euler axis happens to carry the nose on the mesh in front
    /// of it. SpaceShipPitch used to, and got it backwards for half of what it
    /// did (see <see cref="ShipFlight.Rotation"/>).
    /// </summary>
    public readonly struct ShipBasis
    {
        public readonly Vector3 Nose;
        public readonly Vector3 Up;

        public ShipBasis(Vector3 nose, Vector3 up)
        {
            Nose = nose.normalized;
            Up = up.normalized;
        }

        /// <summary>The pilot's right hand.</summary>
        public Vector3 Right => Vector3.Cross(Up, Nose);

        /// <summary>The player's ship: nose along +X.</summary>
        public static ShipBasis NoseAlongX => new ShipBasis(Vector3.right, Vector3.up);

        /// <summary>The enemy ships: nose along +Z.</summary>
        public static ShipBasis NoseAlongZ => new ShipBasis(Vector3.forward, Vector3.up);
    }

    /// <summary>
    /// The arithmetic of how a ship holds itself in flight, with no Unity
    /// object attached, so a sign can be tested without playing the game.
    /// </summary>
    public static class ShipFlight
    {
        /// <summary>
        /// The model's rotation for an attitude, all in degrees.
        ///
        /// <paramref name="yaw"/> turns the ship about its roof, positive to
        /// the pilot's right. <paramref name="pitch"/> lifts the nose when
        /// positive. <paramref name="roll"/> banks to the pilot's right when
        /// positive, and is applied last, about the nose as it then points -
        /// which is what lets a barrel roll go all the way round without the
        /// nose wandering.
        ///
        /// Until 16 November 2026 the player's ship wrote Euler angles
        /// straight onto its model, and the sign of each was whatever fell out
        /// of the mesh: a climb dropped the nose when the ship faced one way
        /// and lifted it when flipped. Nothing here depends on the mesh.
        /// </summary>
        public static Quaternion Rotation(ShipBasis basis, float yaw, float pitch, float roll)
        {
            Quaternion turn = Quaternion.AngleAxis(yaw, basis.Up);
            Quaternion lift = Quaternion.AngleAxis(pitch, Vector3.Cross(basis.Nose, basis.Up));
            Quaternion bank = Quaternion.AngleAxis(-roll, basis.Nose);

            return turn * lift * bank;
        }

        /// <summary>Where the nose points at a facing, with no pitch or roll.</summary>
        public static Vector3 NoseAt(ShipBasis basis, float yaw)
        {
            return Quaternion.AngleAxis(yaw, basis.Up) * basis.Nose;
        }

        /// <summary>
        /// The part of a throttle that is new: what is held now, less what the
        /// ship has got used to.
        ///
        /// A ship that leaned for as long as the throttle was held spent most
        /// of a run leaning, and the lean then cancelled the climb's pitch on
        /// one diagonal and doubled it on the other. Leaning on the change
        /// makes it a reaction: the nose dips as the ship sets off, comes back
        /// as it settles into the speed, and lifts when the throttle is let
        /// go. <paramref name="hold"/> is the share that stays for as long as
        /// the throttle does, 0 for none.
        /// </summary>
        public static float Surge(float throttle, float settled, float hold)
        {
            return (throttle - settled) + settled * Mathf.Clamp01(hold);
        }

        /// <summary>
        /// The facing to turn to, in degrees that are never wrapped, so a
        /// spring can follow it the long way round when that is the way
        /// wanted.
        ///
        /// A ship reversing on the ring turns through half a circle, and
        /// there are two halves to choose from: nose swinging in toward the
        /// island, where the camera sees its tail, or out toward the camera.
        /// <paramref name="outward"/> is the facing that points at the camera;
        /// the turn goes through it.
        /// </summary>
        /// <param name="current">The facing being held now, unwrapped.</param>
        /// <param name="wanted">The facing to end on, any equivalent angle.</param>
        public static float TurnThrough(float current, float wanted, float outward)
        {
            float shortWay = Mathf.DeltaAngle(current, wanted);

            // Nothing to choose for a small correction, and nothing to turn
            // through when the ship is already facing out.
            if (Mathf.Abs(shortWay) < 120f)
            {
                return current + shortWay;
            }

            float toOutward = Mathf.DeltaAngle(current, outward);

            if (Mathf.Abs(toOutward) < 1f)
            {
                return current + shortWay;
            }

            float direction = Mathf.Sign(toOutward);
            float sweep = Mathf.Repeat(direction * Mathf.DeltaAngle(current, wanted), 360f);

            return current + direction * sweep;
        }

        /// <summary>
        /// How far round a barrel roll is, in degrees, <paramref name="progress"/>
        /// of the way through it: away smoothly, fastest in the middle, and
        /// arriving level on a whole number of turns.
        /// </summary>
        public static float BarrelRoll(float progress, int turns)
        {
            float t = Mathf.Clamp01(progress);
            float eased = t * t * t * (t * (t * 6f - 15f) + 10f);

            return eased * 360f * turns;
        }

        /// <summary>
        /// A measured speed as a share of the speed that counts as full, -1 to
        /// 1. For the enemies, whose pitch follows how fast they are actually
        /// climbing and not a stick.
        /// </summary>
        public static float Share(float speed, float fullSpeed)
        {
            return fullSpeed > 0f ? Mathf.Clamp(speed / fullSpeed, -1f, 1f) : 0f;
        }
    }
}
