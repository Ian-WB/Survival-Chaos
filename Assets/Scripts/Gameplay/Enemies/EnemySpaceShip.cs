using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Flies an enemy ship's model: turns it to the way it is going, lifts
    /// its nose as it climbs, banks it through a reversal, and burns an
    /// engine behind it.
    ///
    /// Until 16 November 2026 this was six lines: the model was set to face
    /// left or right, on the frame the ship changed its mind. An enemy
    /// reversing flipped end for end between two frames, and one matching
    /// the player's height slid up the screen dead level. Nothing about it
    /// read as flown.
    ///
    /// It reads what the ship is doing off the ship: where the object
    /// above it moved since last frame, in that object's own axes, which
    /// face the island. So it needs nothing from <see cref="EnemyMovement"/>
    /// but the way round the ring, and it follows whatever else moves the
    /// ship - the spacing that eases two apart, the fly-in from the spawn
    /// point - without being told about either. The pose itself is
    /// <see cref="ShipRig"/>'s, which is also what a hit knocks.
    /// </summary>
    public class EnemySpaceShip : MonoBehaviour
    {

        [SerializeField]
        private GameObject EnemyShip;

        [Header("Flight")]
        [SerializeField]
        [Tooltip("Off leaves the model as it was before 16 November 2026: turned to face left or " +
                 "right on the instant, and nothing else. Off on the Leviathan, whose hull is a " +
                 "wall and should not bank.")]
        private bool animate = true;

        [SerializeField]
        [Tooltip("Degrees the nose lifts at a full climb, and drops at a full dive.")]
        private float climbPitchAngle = 16f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("The climb, in world units a second, that counts as full.")]
        private float fullClimbSpeed = 2.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How fast the ship has to be coming in toward the lane, in world units a second, " +
                 "before its nose points that way and not along the ring. Above the drift that " +
                 "holds a ship on its lane, below the fly-in from the spawn point.")]
        private float flyInSpeed = 1.5f;

        [SerializeField]
        [Tooltip("The facing, in degrees, that points the nose at the camera: straight out from " +
                 "the island. A reversal turns through it, so the turn is seen.")]
        private float turnThrough = 180f;

        [Header("Hover")]
        [SerializeField]
        [Tooltip("Height of the hover bob, in the units of the ship above this model.")]
        private float bobHeight = 0.03f;

        [SerializeField]
        private float bobRoll = 4f;

        [SerializeField]
        private float bobPitch = 2f;

        [SerializeField]
        [Tooltip("How fast the bob runs, in radians a second.")]
        private float bobSpeed = 1.6f;

        [Header("Springs")]
        [SerializeField]
        private ShipRigSettings springs = new ShipRigSettings
        {
            turnFrequency = 1.6f,
            turnDamping = 0.8f,
            bankPerTurnRate = 0.06f,
            maxTurnBank = 35f,
            attitudeFrequency = 2f,
            attitudeDamping = 0.6f,
            maxPitch = 30f,
            knockFrequency = 4.5f,
            knockDamping = 0.5f,
            maxKnock = 0.18f,
        };

        [Header("Engine flare")]
        [SerializeField]
        [Tooltip("The tapered plume the player's ship burns: ShipThrusterPlume. Left empty, the " +
                 "ship flies with no flare.")]
        private Mesh plumeMesh;

        [SerializeField]
        [Tooltip("The thruster material in the hostile colour: BossMuzzleTell.")]
        private Material flareMaterial;

        [SerializeField]
        [Tooltip("Where each engine sits, in this model's own space. Left empty, one engine at " +
                 "the middle of the hull's back face, measured off its meshes.")]
        private Vector3[] engineOffsets = new Vector3[0];

        [SerializeField]
        [Range(0f, 2f)]
        [Tooltip("Plume length at full burn, as a share of the hull's length.")]
        private float flareLength = 0.4f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Plume width at the nozzle, as a share of the hull's narrower side.")]
        private float flareWidth = 0.28f;

        [SerializeField]
        [Tooltip("Emission multiplier at full burn. Kept low for the reason the Leviathan's pods " +
                 "taught: pushed hard, the core blows out white and stops reading as the colour.")]
        private float flareBrightness = 3f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("What the engine burns at while the ship is standing still or mid-turn.")]
        private float flareIdle = 0.35f;

        [Header("Speed trail")]
        [SerializeField]
        [Tooltip("World length of the streak behind the ship for each unit a second it is doing " +
                 "above 1. 0 for no trail.")]
        private float trailLengthPerSpeed = 0.14f;

        [SerializeField]
        [Tooltip("The longest the streak gets, as a share of the hull's length.")]
        private float trailMaxLength = 1.2f;

        [SerializeField]
        [Tooltip("Emission multiplier at full length. Dimmer than the flare it runs out of.")]
        private float trailBrightness = 1.3f;

        private const float FacingLeft = -90f;
        private const float FacingRight = 90f;
        private const float FacingInward = 0f;

        /// <summary>How quickly the measured speeds follow, so one odd frame does not twitch the nose.</summary>
        private const float SpeedResponse = 10f;

        private const float FlareRise = 20f;
        private const float FlareFall = 6f;

        private EnemyMovement movement;
        private Transform body;
        private ShipRig rig;

        private float facing;
        private Vector3 lastPosition;
        private bool hasLast;
        private Vector3 speed;

        private Renderer[] flares;
        private MaterialPropertyBlock block;
        private Vector3 flareSize;
        private float burn;

        /// <summary>The rig this drives, for whatever wants to knock the ship.</summary>
        public ShipRig Rig => rig;

        private void Awake()
        {
            // Cached rather than fetched every frame, and guarded so a prefab
            // without EnemyMovement cannot throw once per frame forever.
            if (EnemyShip != null)
            {
                EnemyShip.TryGetComponent(out movement);
            }

            if (movement == null || !animate)
            {
                return;
            }

            body = movement.transform;
            rig = ShipRig.On(gameObject);
            rig.Configure(ShipBasis.NoseAlongZ, springs);
            rig.Hover(bobHeight, bobRoll, bobPitch, bobSpeed);

            // In Awake, not Start: the pool lists an instance's renderers the
            // first time it spawns it, and a flare built after that would
            // keep reporting motion across the teleport of every later spawn.
            BuildFlares();
        }

        /// <summary>
        /// Every life starts facing the way the ship sets off, at rest, with
        /// no speed carried over from wherever the last one ended.
        /// </summary>
        private void OnEnable()
        {
            hasLast = false;
            speed = Vector3.zero;
            burn = 0f;

            if (rig == null)
            {
                return;
            }

            facing = movement.TravellingLeft ? FacingLeft : FacingRight;
            rig.Aim(facing, 0f, 0f, Vector3.zero);
            rig.Settle();
        }

        private void Update()
        {
            if (movement == null)
            {
                return;
            }

            if (rig == null)
            {
                transform.localRotation = Quaternion.Euler(
                    0f, movement.TravellingLeft ? FacingLeft : FacingRight, 0f);
                return;
            }

            float delta = Time.deltaTime;

            if (delta <= 0f)
            {
                return;
            }

            // In the ship's own axes: X along the ring, Y up, Z in toward
            // the island it is facing.
            Vector3 now = body.position;

            if (hasLast)
            {
                Vector3 measured = body.InverseTransformDirection((now - lastPosition) / delta);
                speed.x = ShipMotion.Approach(speed.x, measured.x, SpeedResponse, delta);
                speed.y = ShipMotion.Approach(speed.y, measured.y, SpeedResponse, delta);
                speed.z = ShipMotion.Approach(speed.z, measured.z, SpeedResponse, delta);
            }

            lastPosition = now;
            hasLast = true;

            // Coming in from the spawn point the ship has no speed along the
            // ring at all, and flown nose-first it arrives; turned side-on,
            // as it was, it slid in.
            bool flyingIn = speed.z > flyInSpeed && speed.z > Mathf.Abs(speed.x);
            float wanted = flyingIn
                ? FacingInward
                : (movement.TravellingLeft ? FacingLeft : FacingRight);

            facing = ShipFlight.TurnThrough(facing, wanted, turnThrough);

            float nose = ShipFlight.Share(speed.y, fullClimbSpeed) * climbPitchAngle;
            rig.Aim(facing, nose, 0f, Vector3.zero);

            Burn(delta);
        }

        /// <summary>
        /// The engine: full while the ship is under way, down to its idle
        /// while it is turning round or standing still.
        /// </summary>
        private void Burn(float delta)
        {
            if (flares == null)
            {
                return;
            }

            bool underWay = speed.sqrMagnitude > 0.09f;
            float turning = Mathf.Clamp01(Mathf.Abs(rig.TurnRate) / 240f);
            float target = Mathf.Lerp(underWay ? 1f : flareIdle, flareIdle, turning);

            burn = ShipMotion.Approach(burn, target, target > burn ? FlareRise : FlareFall, delta);

            Vector3 size = new Vector3(flareSize.x * burn, flareSize.y, flareSize.z);

            for (int i = 0; i < flares.Length; i++)
            {
                ShipFlare.Set(flares[i], block, size, flareBrightness * burn);
            }
        }

        private void BuildFlares()
        {
            if (plumeMesh == null || flareMaterial == null)
            {
                return;
            }

            if (!HullBounds(out Bounds hull))
            {
                return;
            }

            Vector3[] offsets = engineOffsets;

            if (offsets == null || offsets.Length == 0)
            {
                // The nose is +Z, so the back face is the least Z.
                offsets = new[] { new Vector3(hull.center.x, hull.center.y, hull.min.z) };
            }

            float width = Mathf.Min(hull.size.x, hull.size.y) * flareWidth;

            // The plume runs along its own +X, which the turn below lays
            // along the model's -Z: so its X is a length in the model's Z,
            // and its Z a width in the model's X.
            flareSize = new Vector3(hull.size.z * flareLength, width, width);

            block = new MaterialPropertyBlock();
            flares = new Renderer[offsets.Length];

            for (int i = 0; i < offsets.Length; i++)
            {
                flares[i] = ShipFlare.Build(
                    transform, "Engine Flare " + i, plumeMesh, flareMaterial,
                    offsets[i], Quaternion.Euler(0f, 90f, 0f));
            }

            if (trailLengthPerSpeed > 0f)
            {
                // World sizes, at the size the prefab was authored: the
                // model's scale is still that here.
                Vector3 scale = transform.lossyScale;

                ShipTrail.Build(
                    transform, body, plumeMesh, flareMaterial, offsets[0],
                    trailLengthPerSpeed,
                    hull.size.z * Mathf.Abs(scale.z) * trailMaxLength,
                    width * Mathf.Abs(scale.y) * 0.5f,
                    trailBrightness, 1f);
            }
        }

        /// <summary>The box round every mesh of this model, in the model's own space.</summary>
        private bool HullBounds(out Bounds hull)
        {
            hull = default;
            bool any = false;
            Matrix4x4 toModel = transform.worldToLocalMatrix;

            foreach (MeshFilter part in GetComponentsInChildren<MeshFilter>(includeInactive: true))
            {
                if (part.sharedMesh == null)
                {
                    continue;
                }

                Bounds mesh = part.sharedMesh.bounds;
                Matrix4x4 partToModel = toModel * part.transform.localToWorldMatrix;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = mesh.center + Vector3.Scale(mesh.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));

                    Vector3 inModel = partToModel.MultiplyPoint3x4(point);

                    if (any)
                    {
                        hull.Encapsulate(inModel);
                    }
                    else
                    {
                        hull = new Bounds(inModel, Vector3.zero);
                        any = true;
                    }
                }
            }

            return any;
        }
    }
}
