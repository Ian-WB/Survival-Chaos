using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Flies the player's ship model: says where it should face and how it
    /// should lean, and leaves the moving to <see cref="ShipRig"/>.
    ///
    /// The ship's nose already points along its direction of travel, so the two
    /// inputs mean different things:
    ///   W / S  climb and dive  -> the nose lifts or drops, with a little roll
    ///   A / D  throttle        -> the nose dips as the ship sets off and comes
    ///                             back as it settles, and the craft drags back
    ///                             against the acceleration
    ///
    /// Until 16 November 2026 this wrote the model's rotation itself, as Euler
    /// angles eased toward the stick. Two things were wrong with that beyond
    /// the easing. The signs came out of the mesh and not out of the intent: a
    /// climb dropped the nose when the ship faced one way and lifted it when
    /// flipped, and the throttle lifted it where the comment above said it
    /// dipped. And the lean held for as long as the throttle did, at nearly
    /// the climb's own angle, so climbing forward cancelled the two and diving
    /// forward doubled them. Now the angles are said in the ship's own words
    /// (<see cref="ShipFlight.Rotation"/>), the climb's pitch follows the climb
    /// whichever way the ship faces, and the throttle's lean is a reaction to
    /// the change in it (<see cref="ShipFlight.Surge"/>).
    ///
    /// Every angle and distance is signed. If one reads backwards, negate it
    /// in the inspector rather than editing this file.
    /// </summary>
    public class SpaceShipPitch : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Player whose flip state drives this model. Found in the parents when left empty.")]
        private Player player;

        [Header("Climb / dive (W and S)")]
        [SerializeField]
        [Tooltip("Degrees the nose lifts at full climb, and drops at full dive.")]
        private float climbPitchAngle = 22f;

        [SerializeField]
        [Tooltip("Degrees the ship rolls as it climbs, roof away from the camera. Keep this small; " +
                 "it is flavour, not the main cue.")]
        private float climbRollAngle = 10f;

        [Header("Throttle (A and D)")]
        [SerializeField]
        [Tooltip("Degrees the nose dips as the ship sets off toward it, and lifts as it sets off " +
                 "backwards or lets go. A reaction to the throttle changing, not a pose held with it.")]
        private float thrustLeanAngle = 18f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("The share of that lean kept for as long as the throttle is held. 0 flies level " +
                 "at speed; 1 is the old behaviour, a lean held with the key.")]
        private float thrustLeanHold = 0.25f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How quickly the ship gets used to a throttle and the lean comes back off. Higher " +
                 "is a shorter reaction; 2.5 is most of it gone in under a second.")]
        private float thrustSettle = 2.5f;

        [SerializeField]
        [Tooltip("World units the ship drags back against the way it is travelling. Set 0 to disable.")]
        private float thrustDragDistance = 0.06f;

        [Header("Direction flip (Shift)")]
        [SerializeField]
        [Tooltip("The facing, in degrees, that points the nose at the camera. The flip turns " +
                 "through it both ways, so the turn is seen and not hidden behind the hull.")]
        private float flipThrough = 90f;

        [Header("Dash")]
        [SerializeField]
        [Tooltip("Whole turns the ship rolls about its nose through a dash. 0 for none.")]
        private int dashRollTurns = 1;

        [SerializeField]
        [Range(0.1f, 1f)]
        [Tooltip("Seconds that roll takes. Longer than the 0.22 second burst on purpose: it finishes " +
                 "as the ship comes out of it.")]
        private float dashRollSeconds = 0.35f;

        [Header("Idle drift")]
        [SerializeField]
        [Tooltip("Gentle motion while no input is held. Fades out as the player takes control.")]
        private float idleRollAmplitude = 3f;

        [SerializeField]
        private float idlePitchAmplitude = 2f;

        [SerializeField]
        [Tooltip("World units the ship rises and falls as it idles.")]
        private float idleBobHeight = 0.012f;

        [SerializeField]
        private float idleSpeed = 0.7f;

        [Header("Springs")]
        [SerializeField]
        private ShipRigSettings springs = new ShipRigSettings
        {
            turnFrequency = 2.4f,
            turnDamping = 0.85f,
            bankPerTurnRate = 0.03f,
            maxTurnBank = 25f,
            attitudeFrequency = 2.6f,
            attitudeDamping = 0.6f,
            maxPitch = 34f,
            knockFrequency = 6f,
            knockDamping = 0.5f,
            maxKnock = 0.12f,
        };

        private ShipRig rig;
        private float facing;
        private float settledThrottle;
        private bool wasDashing;

        /// <summary>The rig this drives, for whatever wants to knock the ship.</summary>
        public ShipRig Rig => rig;

        private void Awake()
        {
            rig = ShipRig.On(gameObject);
            rig.Configure(ShipBasis.NoseAlongX, springs);
        }

        private void Start()
        {
            if (player == null)
            {
                player = GetComponentInParent<Player>();
            }

            if (player == null)
            {
                Debug.LogWarning("SpaceShipPitch found no Player; the model will not flip.", this);
            }

            facing = FacingTarget();
            rig.Aim(facing, 0f, 0f, Vector3.zero);
            rig.Settle();
        }

        private void Update()
        {
            // The effective axes rather than the raw ones, so the model banks
            // into a dash it never asked for. A burst begun with nothing held
            // takes its heading from the flip, and reading input directly here
            // would fly the ship dead level through the one manoeuvre in the
            // game that most needs to look like a manoeuvre.
            float climb = PlayerMovement.EffectiveVertical;
            float throttle = PlayerMovement.EffectiveHorizontal;

            // Flipping turns the model round, so "toward the nose" reverses with it.
            float mirror = IsFlipped ? -1f : 1f;
            float noseward = throttle * mirror;

            facing = ShipFlight.TurnThrough(facing, FacingTarget(), flipThrough);

            settledThrottle = ShipMotion.Approach(settledThrottle, noseward, thrustSettle, Time.deltaTime);
            float surge = Mathf.Clamp(
                ShipFlight.Surge(noseward, settledThrottle, thrustLeanHold), -1f, 1f);

            // Nose up in a climb whichever way the ship faces; nose down into
            // a throttle toward it.
            float nose = climb * climbPitchAngle - surge * thrustLeanAngle;

            // The roof tips away from the camera in a climb, which is a bank
            // to the pilot's left facing one way and to the right the other.
            float bank = -climb * climbRollAngle * mirror;

            // Along the ring, against the way the ship is going, whichever
            // way its nose points.
            Vector3 drag = new Vector3(-throttle * thrustDragDistance, 0f, 0f);

            rig.Aim(facing, nose, bank, drag);
            rig.Hover(idleBobHeight, idleRollAmplitude, idlePitchAmplitude, idleSpeed);
            rig.Bob = 1f - Mathf.Clamp01(Mathf.Abs(climb) + Mathf.Abs(throttle));

            bool dashing = PlayerMovement.Dashing;

            if (dashing && !wasDashing && dashRollTurns != 0)
            {
                // Over the top in a climb, under in a dive, and the same way
                // round on screen whichever way the ship faces.
                float way = (climb < -0.1f ? -1f : 1f) * mirror;
                rig.BarrelRoll(way < 0f ? -dashRollTurns : dashRollTurns, dashRollSeconds);
            }

            wasDashing = dashing;
        }

        private bool IsFlipped => player != null && player.DirectionFlipped;

        private float FacingTarget()
        {
            return IsFlipped ? 180f : 0f;
        }
    }
}
