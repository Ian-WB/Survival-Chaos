using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    public class ShootScript : MonoBehaviour
    {

        /// <summary>
        /// Every projectile currently in flight, in no particular order.
        ///
        /// Exists so BulletLightPool can find the bullets nearest the camera without
        /// calling GameObject.FindGameObjectsWithTag, which allocates a fresh array
        /// on every call - roughly 200 entries, every frame, forever. That was the
        /// largest steady garbage source left in the game, and garbage is what shows
        /// up as frame time spikes against an otherwise flat graph.
        ///
        /// Projectiles add themselves as they appear and remove themselves as they
        /// die, so the list needs no sweeping. It can still hold a destroyed entry if
        /// something bypasses OnDisable - a scene unload - so readers check.
        ///
        /// Holds the component rather than the transform, so a reader can see which
        /// volley an entry belongs to without a GetComponent per bullet per frame.
        /// <see cref="Body"/> keeps the transform a lookup away for the callers that
        /// only want the position.
        /// </summary>
        private static readonly List<ShootScript> live = new List<ShootScript>();

        public static IReadOnlyList<ShootScript> Live => live;

        /// <summary>
        /// This projectile's transform, resolved once.
        ///
        /// The registry held transforms directly before it held components, for the
        /// reason above: this is read for every live projectile every frame, and the
        /// point of the list is that reading it costs nothing.
        /// </summary>
        public Transform Body { get; private set; }

        /// <summary>
        /// Where this projectile visually is, for anything placing itself on it.
        /// </summary>
        public Vector3 LightPoint =>
            Body != null ? Body.TransformPoint(drawnCentre) : transform.position;

        private void MeasureDrawnCentre()
        {
            drawnCentreMeasured = true;
            drawnCentre = Vector3.zero;

            Renderer[] parts = GetComponentsInChildren<Renderer>(includeInactive: true);

            Vector3 sum = Vector3.zero;
            int counted = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                Vector3 world = parts[i].localToWorldMatrix.MultiplyPoint3x4(parts[i].localBounds.center);
                sum += Body.InverseTransformPoint(world);
                counted++;
            }

            if (counted > 0)
            {
                drawnCentre = sum / counted;
            }
        }

        /// <summary>
        /// Which volley fired this projectile. Equal for every bullet of one shot,
        /// different for every shot.
        ///
        /// The frame number, because a volley *is* a frame: Player.FireLine spawns
        /// the whole pattern in a single loop, so bullets that share a shot share a
        /// frame and nothing else can. Two volleys in one frame would merge, which
        /// needs a fire interval below a frame to happen and costs a redundant
        /// shadow if it ever does.
        ///
        /// Stamped in OnEnable rather than Awake so a projectile taken back out of
        /// the pool belongs to the volley that fired it, not the one that first
        /// created it.
        /// </summary>
        public int Volley { get; private set; }

        // Resolved on enable - not serialized, so stale prefab references can't shadow it.
        private Transform center;

        // The arena centre never moves, and projectiles are now reused rather than
        // recreated, so without this every bullet would repeat the tag search on
        // every reuse. Cleared between play sessions below; a scene reload destroys
        // the old transform, which reads as null here and re-resolves on its own.
        private static Transform sharedCenter;
        private static bool warnedAboutMissingCenter;

        /// <summary>
        /// The middle of what is actually drawn, in this projectile's own space.
        ///
        /// It is not always the origin, and that is the whole reason this exists.
        /// The player's rounds are modelled on their pivot, so for those it is
        /// zero and nothing here changes anything. The boss's darts are not: the
        /// imported art sits 0.266 units above the transform it hangs off. Anything
        /// that wants to be *at* the projectile rather than at its pivot - a light
        /// above all - lands under the round if it uses the transform, which is
        /// exactly what BulletLightPool used to do.
        ///
        /// Measured through each renderer's own matrix from localBounds rather
        /// than from Renderer.bounds, because that one is an axis-aligned box in
        /// world space: its centre shifts as the object turns, and these turn
        /// every frame.
        /// </summary>
        private Vector3 drawnCentre;

        private bool drawnCentreMeasured;

        /// <summary>
        /// The trigger this round hits with, swept between physics steps by
        /// <see cref="FixedUpdate"/>.
        /// </summary>
        private BoxCollider hitBox;

        /// <summary>Where this round was when the previous physics step tested it.</summary>
        private Vector3 lastStepPosition;

        private bool hasLastStep;

        /// <summary>
        /// Shared by every round: a sweep is read and finished before the next one
        /// starts, and allocating per round per step would be the garbage
        /// ShootScript's registry exists to avoid.
        /// </summary>
        private static readonly RaycastHit[] sweepHits = new RaycastHit[16];

        [SerializeField]
        private float speed;

        /// <summary>
        /// Degrees a second around the arena axis, sign included - which is where
        /// this attack's left and right have always been encoded. Read by
        /// <see cref="BossLanceBeam"/>, which stands in for a stream of these and
        /// inherits its direction and its pace from the round it replaces rather
        /// than carrying a second copy of them.
        /// </summary>
        public float Speed => speed;

        [SerializeField]
        [Tooltip("How fast this projectile settles onto the lane the player flies in. 0 leaves " +
                 "it at whatever distance from the arena's axis it was fired at, which is what " +
                 "the player's own shots want. The boss needs it: its 32 muzzles are spread " +
                 "across the width of a ship 3 units deep, so most of them sit outside the " +
                 "band the player can ever be in, and an orbit preserves that error forever.")]
        private float laneResponse;

        /// <summary>
        /// The throw this round was given, set by whatever fired it and cleared
        /// on every spawn - see <see cref="RoundRoute"/>. Zero speed is a round
        /// that holds its height, which is every round the player fires.
        /// </summary>
        private float throwSpeed;
        private float throwHeight;
        private float throwStart;
        private float throwFloor;
        private float throwCeiling;

        /// <summary>
        /// Throws this round up or down at <paramref name="verticalSpeed"/>
        /// from the height it is at now, bouncing between the two limits.
        ///
        /// Called straight after the spawn, so "now" is the muzzle. Timed on
        /// scaled time, so a paused game holds every round where it is on its
        /// path rather than letting it jump ahead on resume.
        /// </summary>
        /// <summary>
        /// What this round is homing on, when it is a torpedo - see
        /// <see cref="TorpedoSteer"/>. Null for every other round, and cleared on
        /// every spawn.
        /// </summary>
        private Transform homeTarget;
        private float homeUntil;
        private float homeGhost;
        private float homePerception;
        private float homeSteer;
        private float homeMaxClimb;

        /// <summary>
        /// The vertical speed the torpedo flew at last frame, so its nose can
        /// point where it is going. Zero once it gives up the chase, which levels
        /// it off.
        /// </summary>
        private float homeClimb;

        /// <summary>
        /// Makes this round a torpedo that steers after <paramref name="target"/>'s
        /// height for <paramref name="seconds"/>, through the lag TorpedoSteer
        /// describes, and inside the band given.
        ///
        /// It starts out believing the target is at its own height, which is what
        /// makes the first few tenths of a second a straight run out of the
        /// muzzle rather than a snap toward the player.
        /// </summary>
        public void Home(Transform target, float seconds, float perception, float steer, float maxClimb,
                         float floor, float ceiling)
        {
            homeTarget = target;
            homeUntil = Time.time + Mathf.Max(0f, seconds);
            homeGhost = transform.position.y;
            homePerception = perception;
            homeSteer = steer;
            homeMaxClimb = maxClimb;
            homeClimb = 0f;
            throwFloor = floor;
            throwCeiling = ceiling;
            throwSpeed = 0f;
        }

        public void Throw(float verticalSpeed, float floor, float ceiling)
        {
            homeTarget = null;
            throwSpeed = verticalSpeed;
            throwHeight = transform.position.y;
            throwStart = Time.time;
            throwFloor = floor;
            throwCeiling = ceiling;
        }

        /// <summary>
        /// Runs on every spawn, including reuse from the pool. This was Start(),
        /// which only ever runs on an object's first life - a reused bullet would
        /// have kept whatever rotation it died with.
        /// </summary>
        private void OnEnable()
        {
            if (Body == null)
            {
                Body = transform;
                hitBox = GetComponent<BoxCollider>();
            }

            // A reused round must not sweep from where it died to where it has just
            // been fired, through everything in between.
            hasLastStep = false;

            // A reused round must not keep the throw of the volley it died in,
            // nor go on hunting for the one before.
            throwSpeed = 0f;
            homeTarget = null;
            homeClimb = 0f;

            // A deterministic starting point, not the final orientation: every
            // frame of Update ends by facing the arena axis, and FaceOrbitCentre
            // below settles this to the same place before the round is ever
            // drawn. This is what a round falls back to if there is no centre.
            Body.rotation = Quaternion.Euler(0f, 0f, 90f);

            // Once per instance, not per spawn: the mesh does not move relative
            // to its own root, and a pooled round is the same object each life.
            if (!drawnCentreMeasured)
            {
                MeasureDrawnCentre();
            }

            Volley = Time.frameCount;
            live.Add(this);

            if (sharedCenter == null)
            {
                GameObject scenario = GameObject.FindWithTag("Scenario");

                if (scenario == null)
                {
                    if (!warnedAboutMissingCenter)
                    {
                        warnedAboutMissingCenter = true;
                        Debug.LogWarning(
                            "ShootScript found nothing tagged 'Scenario', so projectiles have no " +
                            "centre to orbit and will sit still.", this);
                    }

                    return;
                }

                sharedCenter = scenario.transform;
            }

            center = sharedCenter;

            // Without this a round spends its first frame at the placeholder
            // rotation above and only squares up on its first Update - one frame
            // of every shot in the game drawn sideways, the player's included.
            // It also matters to the light pool, which reads LightPoint through
            // this transform: a wrong rotation puts the light in the wrong place
            // for exactly as long as the mesh is wrong.
            FaceOrbitCentre();
        }

        /// <summary>
        /// Points the round at the arena axis, level with itself - the orientation
        /// <see cref="Update"/> settles on, applied at spawn so the first frame
        /// already matches every frame after it.
        /// </summary>
        private void FaceOrbitCentre()
        {
            if (center == null)
            {
                return;
            }

            Vector3 pos = center.position;
            pos.y = transform.position.y;
            transform.LookAt(pos);
        }

        /// <summary>
        /// Catches what this round flies through between two physics steps.
        ///
        /// Rounds move in Update by setting their transform, and the physics system
        /// only tests the positions it finds at each fixed step, 50 times a second.
        /// Between two of those a player round at the lane covers about 0.98 units
        /// (150 degrees a second at radius 18.72), and a hit only registers if one
        /// of those samples lands while the round overlaps a hitbox - a window of
        /// the box's width plus the round's length. For the halved enemies that is
        /// 0.69 to 0.89, so roughly one round in ten to one in three skipped clean
        /// over an enemy it was dead on. Found on 14 September 2026 when a still
        /// player missed a still enemy that the previous round had hit.
        ///
        /// So each step sweeps this round's box along the stretch it has moved since
        /// the last one. Anything the box overlaps at either end is left alone,
        /// because the physics system reports those itself; only what lies wholly in
        /// the gap is delivered, as the same OnTriggerEnter the engine would have
        /// sent. Every receiver - Enemy, Enemy_1, the boss's emplacements, the
        /// player - keeps its own rules, and nothing can be hit twice.
        ///
        /// A straight sweep along a curved path: at this radius a 0.98-unit step
        /// bows 0.006 from its chord, far inside any hitbox.
        /// </summary>
        private void FixedUpdate()
        {
            if (hitBox == null || !hitBox.enabled)
            {
                return;
            }

            Vector3 now = transform.position;

            if (!hasLastStep)
            {
                lastStepPosition = now;
                hasLastStep = true;
                return;
            }

            Vector3 from = lastStepPosition;
            lastStepPosition = now;

            Vector3 travel = now - from;
            float distance = travel.magnitude;

            if (distance < 0.0001f)
            {
                return;
            }

            Quaternion rotation = transform.rotation;
            Vector3 centreOffset = transform.TransformPoint(hitBox.center) - now;
            Vector3 halfExtents = Vector3.Scale(hitBox.size, transform.lossyScale) * 0.5f;

            int count = Physics.BoxCastNonAlloc(
                from + centreOffset,
                halfExtents,
                travel / distance,
                sweepHits,
                rotation,
                distance,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider other = sweepHits[i].collider;

                if (other == null || other == hitBox || !other.enabled)
                {
                    continue;
                }

                // Rounds pass through rounds, as they do in the physics system.
                if (other.GetComponentInParent<ShootScript>() != null)
                {
                    continue;
                }

                if (Physics.GetIgnoreLayerCollision(gameObject.layer, other.gameObject.layer))
                {
                    continue;
                }

                if (OverlapsAt(from, rotation, other) || OverlapsAt(now, rotation, other))
                {
                    continue;
                }

                DeliverTrigger(other);

                // The hit usually sends this round back to the pool, and a round
                // that has already landed does not carry on to a second target.
                if (!gameObject.activeInHierarchy)
                {
                    return;
                }
            }
        }

        private bool OverlapsAt(Vector3 position, Quaternion rotation, Collider other)
        {
            return Physics.ComputePenetration(
                hitBox, position, rotation,
                other, other.transform.position, other.transform.rotation,
                out _, out _);
        }

        /// <summary>
        /// Sends what the physics system would have sent had it caught the overlap:
        /// OnTriggerEnter to both sides, and to the struck collider's rigidbody when
        /// that sits on a different object.
        /// </summary>
        private void DeliverTrigger(Collider other)
        {
            other.gameObject.SendMessage("OnTriggerEnter", hitBox, SendMessageOptions.DontRequireReceiver);

            Rigidbody body = other.attachedRigidbody;
            if (body != null && body.gameObject != other.gameObject && gameObject.activeInHierarchy)
            {
                body.gameObject.SendMessage("OnTriggerEnter", hitBox, SendMessageOptions.DontRequireReceiver);
            }

            if (gameObject.activeInHierarchy && other.gameObject.activeInHierarchy)
            {
                SendMessage("OnTriggerEnter", other, SendMessageOptions.DontRequireReceiver);
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (center == null)
            {
                return;
            }

            // Before the orbit, so the LookAt at the end faces from where this
            // ends up rather than from where it started the frame. The throw
            // sets height and the lane sets distance from the axis, so neither
            // undoes the other.
            if (throwSpeed != 0f)
            {
                Vector3 thrown = transform.position;
                thrown.y = RoundRoute.Height(
                    throwHeight, throwSpeed, Time.time - throwStart, throwFloor, throwCeiling);
                transform.position = thrown;
            }

            if (homeTarget != null)
            {
                if (Time.time < homeUntil && homeTarget.gameObject.activeInHierarchy)
                {
                    Vector3 chasing = transform.position;
                    float height = chasing.y;

                    homeClimb = TorpedoSteer.Step(ref height, ref homeGhost, homeTarget.position.y,
                        homePerception, homeSteer, homeMaxClimb, throwFloor, throwCeiling, Time.deltaTime);

                    chasing.y = height;
                    transform.position = chasing;
                }
                else
                {
                    // Given up: level off and fly on at the height it reached, so
                    // a torpedo that missed is a round to avoid on its next lap
                    // rather than one still hunting.
                    homeTarget = null;
                    homeClimb = 0f;
                }
            }

            if (laneResponse > 0f)
            {
                transform.position = ArenaGeometry.EaseOntoOrbit(
                    transform.position,
                    center.position,
                    ArenaGeometry.LaneRadius,
                    laneResponse,
                    Time.deltaTime);
            }

            Vector3 pos =  center.position;
            pos.y = transform.position.y;
            transform.RotateAround(pos, Vector3.up, Time.deltaTime * speed);
            transform.LookAt(pos);

            // A torpedo points its nose along its path. LookAt leaves the round
            // level and facing the axis, with the ring running along its local X,
            // so the climb is a turn about its own forward - positive noses up
            // when it travels toward +X, and the other way when it travels back.
            if (homeClimb != 0f)
            {
                float radius = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(pos.x, pos.z));
                float alongRing = speed * Mathf.Deg2Rad * radius;
                float pitch = TorpedoSteer.Pitch(homeClimb, alongRing);

                // RotateAround turns positive degrees anticlockwise from above,
                // which carries the round toward its own -X after LookAt.
                transform.Rotate(0f, 0f, speed > 0f ? -pitch : pitch, Space.Self);
            }
        }

        /// <summary>
        /// Pairs with the registration in OnEnable. Runs on every route out - going
        /// back to the pool, being destroyed, the scene unloading - so the list
        /// cannot accumulate entries across a run.
        /// </summary>
        private void OnDisable()
        {
            live.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sharedCenter = null;
            warnedAboutMissingCenter = false;
            live.Clear();
        }
    }
}
