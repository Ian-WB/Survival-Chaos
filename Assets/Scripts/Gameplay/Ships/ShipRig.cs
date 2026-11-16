using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// How a ship's springs are tuned. Lives on whatever drives the rig, so the
    /// dials sit beside the angles they carry.
    /// </summary>
    [System.Serializable]
    public sealed class ShipRigSettings
    {
        [Header("Turning")]
        [Min(0f)]
        [Tooltip("How fast the ship comes round to a new facing, in swings a second. 2 turns a " +
                 "half circle in about half a second. 0 snaps.")]
        public float turnFrequency = 2f;

        [Range(0.1f, 2f)]
        [Tooltip("1 arrives without passing the new facing. Lower swings past and back.")]
        public float turnDamping = 0.85f;

        [Tooltip("Degrees of bank for each degree a second the ship is turning at. This is what " +
                 "makes a reversal a banked turn and not a model spinning on a pin.")]
        public float bankPerTurnRate = 0.05f;

        [Min(0f)]
        [Tooltip("The most a turn may bank the ship, in degrees.")]
        public float maxTurnBank = 35f;

        [Header("Attitude (pitch and roll)")]
        [Min(0f)]
        [Tooltip("How fast the nose and wings follow, in swings a second.")]
        public float attitudeFrequency = 2.4f;

        [Range(0.1f, 2f)]
        [Tooltip("Lower lets the ship lean a little too far and settle. 1 is dead-beat.")]
        public float attitudeDamping = 0.6f;

        [Min(0f)]
        [Tooltip("The steepest the nose may point, up or down, in degrees.")]
        public float maxPitch = 40f;

        [Header("Knocks (recoil and hits)")]
        [Min(0f)]
        [Tooltip("How fast a knocked ship springs back to its place, in swings a second.")]
        public float knockFrequency = 5f;

        [Range(0.1f, 2f)]
        public float knockDamping = 0.5f;

        [Min(0f)]
        [Tooltip("The furthest a knock may carry the model from its place, in the units of the " +
                 "object it sits under. The hit box does not move with it.")]
        public float maxKnock = 0.2f;
    }

    /// <summary>
    /// Holds a ship model's pose: one place that writes its rotation and its
    /// position, from everything that wants a say in them.
    ///
    /// The player's model was posed by SpaceShipPitch and the enemies' by
    /// EnemySpaceShip, each writing Euler angles straight onto the transform.
    /// That works for one voice. It has no room for a second: a recoil, a hit,
    /// a roll through a dash would each have had to be blended into those two
    /// scripts by hand, and the enemy one had no motion in it to blend with.
    /// Here a driver says where the ship should be facing and how it should
    /// be leaning, anything may knock it, and the springs make one pose of it.
    ///
    /// Three kinds of input, because they behave differently:
    /// <list type="bullet">
    /// <item>what the ship is aiming for (<see cref="Aim"/>), which the
    /// springs chase;</item>
    /// <item>knocks (<see cref="Kick"/>, <see cref="Twist"/>), which are
    /// added to a spring's speed and die away on their own;</item>
    /// <item>a barrel roll (<see cref="BarrelRoll"/>), which runs on a clock
    /// and ends level.</item>
    /// </list>
    ///
    /// Only the model moves. The ship's hit box is on the object above, so
    /// nothing here changes what can be hit or where.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipRig : MonoBehaviour
    {
        private ShipBasis basis = ShipBasis.NoseAlongX;
        private ShipRigSettings settings = new ShipRigSettings();

        private Vector3 restPosition;
        private Quaternion restRotation = Quaternion.identity;
        private bool restMeasured;

        /// <summary>
        /// Whether the pose is the model's whole rotation or sits on top of
        /// the one it was authored with. A driven ship is told its facing
        /// outright; a model nobody drives (the Drone) keeps the rotation its
        /// prefab gave it and is only ever knocked about it.
        /// </summary>
        private bool driven;

        private Spring yaw;
        private Spring pitch;
        private Spring roll;
        private Spring surge;
        private Spring heave;
        private Spring sway;

        private float aimYaw;
        private float aimPitch;
        private float aimRoll;
        private Vector3 aimOffset;

        private float rollStarted = float.NegativeInfinity;
        private float rollSeconds;
        private int rollTurns;

        private float bobPhase;
        private float bobHeight;
        private float bobRoll;
        private float bobPitch;
        private float bobSpeed;

        /// <summary>How much of the bob shows, 0 to 1. The player's fades as they take the stick.</summary>
        public float Bob { get; set; } = 1f;

        /// <summary>The facing the ship is at now, in degrees, unwrapped.</summary>
        public float Yaw => yaw.Value;

        /// <summary>How fast the ship is turning, in degrees a second.</summary>
        public float TurnRate => yaw.Velocity;

        /// <summary>The rig on an object, adding one the first time it is asked for.</summary>
        public static ShipRig On(GameObject host)
        {
            if (host == null)
            {
                return null;
            }

            return host.TryGetComponent(out ShipRig existing) ? existing : host.AddComponent<ShipRig>();
        }

        /// <summary>Says which way this model's nose points and how its springs are tuned.</summary>
        public void Configure(ShipBasis modelBasis, ShipRigSettings tuning)
        {
            MeasureRest();
            basis = modelBasis;
            driven = true;

            if (tuning != null)
            {
                settings = tuning;
            }
        }

        private void Awake()
        {
            MeasureRest();

            // Not the same phase on every ship, or a wave bobs as one object.
            // Counted, not drawn: a random number here would come out of the
            // run's seeded stream and move every spawn after it.
            built++;
            bobPhase = Mathf.Repeat(built * 0.6180339f, 1f) * 20f;
        }

        /// <summary>How many rigs have woken, which is all the bob's phase is made from.</summary>
        private static int built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            built = 0;
        }

        private void MeasureRest()
        {
            if (!restMeasured)
            {
                restPosition = transform.localPosition;
                restRotation = transform.localRotation;
                restMeasured = true;
            }
        }

        /// <summary>
        /// Every life starts at rest. A pooled enemy killed mid-knock would
        /// otherwise come back still swinging from the hit that killed it.
        /// </summary>
        private void OnEnable()
        {
            Settle();
        }

        /// <summary>
        /// What the ship is aiming for: its facing, how its nose and wings
        /// sit, and where its model rides against its place. Degrees, and for
        /// the offset the units and axes of the object the model sits under.
        /// </summary>
        public void Aim(float facing, float nose, float bank, Vector3 offset)
        {
            aimYaw = facing;
            aimPitch = nose;
            aimRoll = bank;
            aimOffset = offset;
        }

        /// <summary>
        /// How the ship hovers when left alone: a rise and fall in the units
        /// of the object the model sits under, a rock from wing to wing and a
        /// nod of the nose in degrees, at <paramref name="speed"/> radians a
        /// second. <see cref="Bob"/> says how much of it shows.
        /// </summary>
        public void Hover(float height, float rockDegrees, float nodDegrees, float speed)
        {
            bobHeight = height;
            bobRoll = rockDegrees;
            bobPitch = nodDegrees;
            bobSpeed = speed;
        }

        /// <summary>Puts the ship on what it is aiming for, at rest, with no roll running.</summary>
        public void Settle()
        {
            yaw.Reset(aimYaw);
            pitch.Reset(aimPitch);
            roll.Reset(aimRoll);
            surge.Reset(aimOffset.x);
            heave.Reset(aimOffset.y);
            sway.Reset(aimOffset.z);
            rollStarted = float.NegativeInfinity;

            if (restMeasured)
            {
                Apply(0f);
            }
        }

        /// <summary>
        /// Knocks the model, in units a second along the axes of the object
        /// it sits under. It springs back on its own.
        /// </summary>
        public void Kick(Vector3 velocity)
        {
            // Bounded, because the player's widest gun lands eight rounds on
            // one hull in a frame and each of them knocks.
            float limit = settings.maxKnock * settings.knockFrequency * 2f * Mathf.PI;
            Vector3 speed = new Vector3(surge.Velocity, heave.Velocity, sway.Velocity) + velocity;

            if (limit > 0f)
            {
                speed = Vector3.ClampMagnitude(speed, limit);
            }

            surge.Velocity = speed.x;
            heave.Velocity = speed.y;
            sway.Velocity = speed.z;
        }

        /// <summary>Knocks the model along a world direction. See <see cref="Kick"/>.</summary>
        public void KickWorld(Vector3 worldDirection, float speed)
        {
            if (worldDirection.sqrMagnitude < 0.000001f)
            {
                return;
            }

            Transform parent = transform.parent;
            Vector3 local = parent != null
                ? parent.InverseTransformDirection(worldDirection.normalized)
                : worldDirection.normalized;

            Kick(local * speed);
        }

        /// <summary>Knocks the nose and the wings, in degrees a second.</summary>
        public void Twist(float noseUp, float bankRight)
        {
            pitch.Kick(noseUp);
            roll.Kick(bankRight);
        }

        /// <summary>
        /// Rolls the ship about its nose, <paramref name="turns"/> whole turns
        /// over <paramref name="seconds"/> of game time, negative to roll the
        /// other way. It ends level because it ends on a whole turn.
        /// </summary>
        public void BarrelRoll(int turns, float seconds)
        {
            if (turns == 0 || seconds <= 0f)
            {
                return;
            }

            rollTurns = turns;
            rollSeconds = seconds;
            rollStarted = Time.time;
        }

        /// <summary>Which way the nose points now, in the axes of the object the model sits under.</summary>
        public Vector3 Nose => ShipFlight.NoseAt(basis, yaw.Value);

        /// <summary>
        /// After everything that moves the ship or aims the rig this frame.
        /// </summary>
        private void LateUpdate()
        {
            // A stopped clock holds the pose: nothing has asked for a new one.
            if (Time.deltaTime > 0f)
            {
                Apply(Time.deltaTime);
            }
        }

        private void Apply(float delta)
        {
            MeasureRest();

            float limit = settings.maxPitch;

            yaw.Step(aimYaw, settings.turnFrequency, settings.turnDamping, delta);
            pitch.Step(Mathf.Clamp(aimPitch, -limit, limit),
                settings.attitudeFrequency, settings.attitudeDamping, delta);
            roll.Step(aimRoll, settings.attitudeFrequency, settings.attitudeDamping, delta);

            surge.Step(aimOffset.x, settings.knockFrequency, settings.knockDamping, delta);
            heave.Step(aimOffset.y, settings.knockFrequency, settings.knockDamping, delta);
            sway.Step(aimOffset.z, settings.knockFrequency, settings.knockDamping, delta);

            float wave = (Time.time + bobPhase) * bobSpeed;
            float bob = Mathf.Clamp01(Bob);
            float rock = Mathf.Sin(wave) * bobRoll * bob;
            float nod = Mathf.Sin(wave * 0.7f + 1.3f) * bobPitch * bob;
            float rise = Mathf.Sin(wave * 1.3f + 0.4f) * bobHeight * bob;

            float bank = Mathf.Clamp(
                yaw.Velocity * settings.bankPerTurnRate, -settings.maxTurnBank, settings.maxTurnBank);

            float stunt = 0f;
            float rolled = (Time.time - rollStarted) / Mathf.Max(rollSeconds, 0.0001f);

            if (rolled >= 0f && rolled < 1f)
            {
                stunt = ShipFlight.BarrelRoll(rolled, rollTurns);
            }

            Quaternion pose = ShipFlight.Rotation(
                basis,
                yaw.Value,
                pitch.Value + nod,
                roll.Value + rock + bank + stunt);

            transform.localRotation = driven ? pose : pose * restRotation;

            Vector3 offset = new Vector3(surge.Value, heave.Value + rise, sway.Value);

            // The knocks are bounded on the way in; this bounds where they can
            // leave the model, so a long frame cannot show it off its ship.
            if (settings.maxKnock > 0f)
            {
                offset = aimOffset + Vector3.ClampMagnitude(offset - aimOffset, settings.maxKnock * 1.5f);
            }

            transform.localPosition = restPosition + offset;
        }
    }
}
