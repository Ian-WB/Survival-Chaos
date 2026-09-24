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
        /// inherits its direction from the round it replaces rather than carrying
        /// a second copy of it. Only the sign, since 21 September 2026: the beam
        /// was thrown along the ring at this pace until it became a laser that
        /// reaches its full length at once.
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
        /// Whether this round is a torpedo - see <see cref="TorpedoSteer"/>. A
        /// torpedo flies on its own heading and speed instead of orbiting at
        /// <see cref="speed"/>, and stays one after its fuel is spent. Cleared on
        /// every spawn.
        /// </summary>
        private bool torpedoing;
        private Torpedo torpedo;
        private TorpedoHandling torpedoHandling;

        /// <summary>What the torpedo is chasing. It flies on at its last belief if this dies.</summary>
        private Transform torpedoTarget;

        /// <summary>The target's hit box, which is what the torpedo has to meet.</summary>
        private BoxCollider torpedoTargetBox;

        /// <summary>
        /// Enemies this round may still pass through, and what it has already
        /// struck. Only the player's rounds are given passes - see
        /// <see cref="Pierce"/> - so every other round stops at its first target.
        /// </summary>
        private readonly RoundPierce pierce = new RoundPierce();

        /// <summary>
        /// Lets this round pass through <paramref name="count"/> enemies before
        /// the next one stops it, for the Piercing Rounds upgrade. Called straight
        /// after the spawn, the way <see cref="Throw"/> and <see cref="Home"/> are.
        /// </summary>
        public void Pierce(int count)
        {
            pierce.Reset(count);
        }

        /// <summary>
        /// What an enemy does with one of the player's rounds that has struck it,
        /// in place of despawning the round outright: the round flies on if it has
        /// a pass left, and goes back to the pool if not.
        ///
        /// Returns false when the hit should not count at all - this round has
        /// struck this enemy already, or was spent earlier in the same physics
        /// step and is only now being reported.
        ///
        /// The boss's parts do not come through here and stop every round, as
        /// they always have. The hull swallows rounds, and a pass that carried one
        /// through an emplacement would only take it into the armour behind.
        /// </summary>
        public static bool Land(Collider round, GameObject target)
        {
            if (!round.TryGetComponent(out ShootScript script))
            {
                ObjectPool.Despawn(round.gameObject);
                return true;
            }

            switch (script.pierce.Strike(target))
            {
                case StrikeResult.PassThrough:
                    return true;

                case StrikeResult.Stop:
                    ObjectPool.Despawn(round.gameObject);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether this round is a torpedo, for the player: a torpedo goes off
        /// when it hits, where a plain round flies on through.
        /// </summary>
        public bool IsTorpedo => torpedoing;

        /// <summary>How hard a torpedo's motor is burning, 0 to 1; 0 for any other round.</summary>
        public float Thrust => torpedoing ? TorpedoSteer.Thrust(torpedoHandling, torpedo.Age) : 0f;

        /// <summary>
        /// Whether this round's nose is modelled on its local -X, which is true
        /// of the ones fired with a positive orbit speed - see
        /// <see cref="TorpedoSteer.Roll"/>.
        /// </summary>
        public bool NoseOnNegativeX => speed >= 0f;

        /// <summary>
        /// The prefab's own scale, taken on the first spawn and put back on
        /// every one after it, because a torpedo is fired smaller than the plain
        /// round the same prefab also is.
        /// </summary>
        private Vector3 authoredScale;
        private bool authoredScaleKnown;

        /// <summary>
        /// How fast a torpedo closes on the lane the player flies in, as well as
        /// on the player's height and place round the ring. The crown's muzzles
        /// sit across the hull's depth, and a torpedo that stayed at its muzzle's
        /// distance from the axis would pass in front of or behind the player it
        /// had flown straight at.
        /// </summary>
        private const float TorpedoLaneResponse = 4f;

        /// <summary>
        /// Makes this round a torpedo that hunts <paramref name="target"/> inside
        /// the band given, flying as <paramref name="handling"/> says.
        ///
        /// It leaves along the ring the way this round was always going to fly,
        /// and it has seen the player where they are at the moment of firing.
        /// Its life is set here too: a torpedo is slower than the round it
        /// replaces and needs longer than the prefab allows, so it lives for its
        /// fuel plus <paramref name="coast"/> seconds. And its size:
        /// <paramref name="scale"/> times the prefab's, hit box and all, so what
        /// can hit you is what you can see.
        /// </summary>
        public void Home(Transform target, in TorpedoHandling handling, float coast, float scale,
                         float floor, float ceiling)
        {
            throwSpeed = 0f;
            throwFloor = floor;
            throwCeiling = ceiling;

            if (center == null || target == null)
            {
                return;
            }

            torpedoing = true;
            torpedoTarget = target;
            torpedoTargetBox = target.GetComponent<BoxCollider>();
            torpedoHandling = handling;

            if (scale > 0f)
            {
                transform.localScale = authoredScale * scale;
            }

            Vector2 start = new Vector2(0f, transform.position.y);
            torpedo = Torpedo.Launch(start, speed >= 0f ? 0f : 180f, FlatTarget(start, AimPoint()));

            if (TryGetComponent(out DestroyAfterTime life))
            {
                life.RetireIn(Mathf.Max(0f, handling.Fuel) + Mathf.Max(0f, coast));
            }
        }

        /// <summary>
        /// Where this round's pivot has to be for its hit box to sit on the
        /// target's.
        ///
        /// Not the target's position. The dart's box hangs 0.27 above its pivot
        /// with the art, and the player's is 0.16 tall, so a torpedo that flew its
        /// pivot into the player's passed clean over them - which is how every
        /// torpedo that reached a still player in play on 21 September 2026
        /// missed. Measured each frame, because the box swings with the roll.
        /// </summary>
        private Vector3 AimPoint()
        {
            Vector3 aim = torpedoTargetBox != null
                ? torpedoTargetBox.transform.TransformPoint(torpedoTargetBox.center)
                : torpedoTarget.position;

            if (hitBox != null)
            {
                aim -= transform.TransformPoint(hitBox.center) - transform.position;
            }

            return aim;
        }

        /// <summary>
        /// Where <paramref name="world"/> is in the torpedo's flat ring, from a
        /// torpedo at <paramref name="from"/>: the short way round the ring, at
        /// the lane's radius, so going round the ring in either direction is
        /// covered.
        /// </summary>
        private Vector2 FlatTarget(Vector2 from, Vector3 world)
        {
            float arc = Mathf.DeltaAngle(AngleAround(transform.position), AngleAround(world));
            return new Vector2(from.x + arc * Mathf.Deg2Rad * ArenaGeometry.LaneRadius, world.y);
        }

        /// <summary>
        /// Degrees round the arena axis, increasing the way a positive
        /// RotateAround carries a round.
        /// </summary>
        private float AngleAround(Vector3 world)
        {
            Vector3 offset = world - center.position;
            return Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Throws this round up or down at <paramref name="verticalSpeed"/>
        /// from the height it is at now, bouncing between the two limits.
        ///
        /// Called straight after the spawn, so "now" is the muzzle. Timed on
        /// scaled time, so a paused game holds every round where it is on its
        /// path rather than letting it jump ahead on resume.
        /// </summary>
        public void Throw(float verticalSpeed, float floor, float ceiling)
        {
            torpedoing = false;
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

            if (authoredScaleKnown)
            {
                transform.localScale = authoredScale;
            }
            else
            {
                authoredScale = transform.localScale;
                authoredScaleKnown = true;
            }

            // A reused round must not sweep from where it died to where it has just
            // been fired, through everything in between.
            hasLastStep = false;

            // A reused round must not keep the throw of the volley it died in,
            // nor go on hunting for the one before.
            throwSpeed = 0f;
            torpedoing = false;
            torpedoTarget = null;
            torpedoTargetBox = null;

            // Nor the passes it had left, or the enemies it had already struck.
            pierce.Reset(0);

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
                // A piercing round is still active here, and goes on down the list.
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

            if (torpedoing)
            {
                FlyTorpedo();
                return;
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
        }

        /// <summary>
        /// One frame of a torpedo: steered in the flat ring by TorpedoSteer, then
        /// put back on the ring - round the axis by the distance it covered, at
        /// the height it reached, closing on the lane - and pointed along its
        /// heading.
        /// </summary>
        private void FlyTorpedo()
        {
            Vector2 before = torpedo.Position;

            // A target that has gone - the player died - leaves the torpedo
            // chasing its last belief, which is where it was going anyway.
            Vector2 target = torpedoTarget != null && torpedoTarget.gameObject.activeInHierarchy
                ? FlatTarget(before, AimPoint())
                : torpedo.Ghost;

            TorpedoSteer.Step(ref torpedo, target, torpedoHandling, throwFloor, throwCeiling, Time.deltaTime);

            Vector3 axis = center.position;
            axis.y = transform.position.y;

            float lane = ArenaGeometry.LaneRadius;
            if (lane > 0f)
            {
                transform.RotateAround(axis, Vector3.up,
                    (torpedo.Position.x - before.x) / lane * Mathf.Rad2Deg);
            }

            Vector3 moved = transform.position;
            moved.y = torpedo.Position.y;
            transform.position = ArenaGeometry.EaseOntoOrbit(
                moved, center.position, lane, TorpedoLaneResponse, Time.deltaTime);

            axis.y = transform.position.y;
            transform.LookAt(axis);
            transform.Rotate(0f, 0f, TorpedoSteer.Roll(torpedo.Heading, speed >= 0f), Space.Self);
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
