using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The prow lance: a laser fired along the ring from the prow, rather than a
    /// stream of projectiles.
    ///
    /// It was forty rounds until 8 September, and the measurement that ended that
    /// is worth keeping: the ring is 86.2 units around and a lance round crossed it
    /// at 19.16 units a second, so rounds leaving 0.06s apart were 1.15 units
    /// apart while each was 1.286 units long. Consecutive rounds overlapped by 0.14
    /// units before they had gone anywhere. The stream was never a row of bullets
    /// waiting to be separated - it was already a solid line, costing forty
    /// objects, forty colliders and forty lights to draw one thing, into a light
    /// cluster that holds 24 lights per cell and silently drops the rest.
    ///
    /// Welding them into four 3.3-unit rounds was tried first and rejected on
    /// sight: at that length they read as sticks flying in formation, not as a
    /// beam. So it became one arc - and from 8 to 21 September 2026 that arc was
    /// a 12.8-unit square tube thrown along the ring, which in play read as a
    /// bent log rather than a laser. The throw was as much of that as the shape:
    /// a chunk that leaves the prow and slides away is a projectile, whatever it
    /// is drawn with. A laser stays attached to what fired it.
    ///
    /// So the beam now flashes out from the prow to its full reach, holds, and
    /// goes out, drawn as a thin hot core inside a soft glow with no hard edge
    /// anywhere. That changed how it is dodged, on purpose. The thrown beam could
    /// be watched coming; this one arrives almost at once, so the charge before it
    /// is the whole warning - which is what the charge was sized for. Its 1.2s is
    /// one full crossing of the band at the player's climb rate (see
    /// BossEmitter.RunLance).
    ///
    /// It carries no collider. The arena is a ring, so every position in it is
    /// really a bearing, a radius and a height, and asking whether the player is
    /// inside the beam is three comparisons in those terms instead of a mesh
    /// intersection. That is the reason a beam is *cheaper* here than the bullets
    /// it replaces rather than more expensive, and it is why the damage below is a
    /// test rather than a trigger.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class BossLanceBeam : MonoBehaviour
    {
        /// <summary>
        /// How far round the ring the beam reaches from the prow, in degrees.
        ///
        /// The thrown beam's head covered 160 degrees in its two seconds at 80 a
        /// second, so the laser threatens the stretch of ring the old beam passed
        /// over. Held under 360 so the hit test never has to reason about a beam
        /// that overlaps itself.
        /// </summary>
        private const float ReachDegrees = 160f;

        /// <summary>
        /// Seconds for the beam to flash out from the prow to its full reach:
        /// quick enough to read as a laser firing rather than as something
        /// travelling, slow enough to see which way round the ring it went.
        /// </summary>
        private const float ExtendSeconds = 0.15f;

        /// <summary>
        /// Seconds it holds at full reach. With the extend before it, a player
        /// caught in it takes four hits at the far end and five by the prow; a
        /// full pass of the thrown beam cost about four.
        /// </summary>
        private const float HoldSeconds = 0.5f;

        /// <summary>
        /// Seconds it takes to go out, thinning to nothing. It hurts no one
        /// while it does: the moment it starts to thin, it is off.
        /// </summary>
        private const float FadeSeconds = 0.15f;

        /// <summary>
        /// Seconds of contact per point of damage.
        ///
        /// The old lance had no rate at all: it was one point per round that
        /// touched you, so standing in it was up to forty against twenty health -
        /// death twice over - and clipping its edge was one. A beam wants the
        /// opposite shape, where brushing it is cheap and staying in it is costly,
        /// and that is a rate rather than a count.
        /// </summary>
        private const float DamageInterval = 0.15f;

        /// <summary>
        /// The width the hit test uses, which the drawn beam does not have: its
        /// core is thinner and its glow wider and soft, and the glow's visible
        /// edge lands about where this does. 0.2 is the cross-section of the
        /// rounds the beam replaced.
        /// </summary>
        private const float HitWidth = 0.2f;

        /// <summary>The white-hot line down the middle.</summary>
        private const float CoreWidth = 0.06f;

        /// <summary>The halo around it, fading to nothing at this width.</summary>
        private const float GlowWidth = 0.5f;

        /// <summary>
        /// How much wider than it settles the beam is the instant it fires, and
        /// how long it takes to settle. The punch is what makes it read as
        /// fired rather than switched on. It changes nothing the hit test reads.
        /// </summary>
        private const float FirePunch = 1.6f;
        private const float PunchSeconds = 0.2f;

        /// <summary>
        /// The last stretch of the beam, in units, over which it narrows to a
        /// point, so its far end reads as a tip rather than as a cut.
        /// </summary>
        private const float TipTaper = 0.6f;

        /// <summary>
        /// Segments along the arc. The mesh is rebuilt every frame the beam is
        /// on, so this is a per-frame cost, but at 64 it is 260 vertices.
        ///
        /// It is also what keeps the beam on the lane and round rather than
        /// polygonal. At 2.5 degrees a segment the chord sags under a
        /// two-hundredth of a unit off the true arc, well inside the core's own
        /// width.
        /// </summary>
        private const int Segments = 64;

        private MeshFilter filter;
        private MeshRenderer body;
        private Mesh mesh;

        // Two ribbons along the same spine, the core over submesh 0 and the glow
        // over submesh 1, each two vertices a cut. UV x is the distance out from
        // the prow in units and y runs across the ribbon, 0 to 1.
        private Vector3[] vertices;
        private Vector2[] uvs;

        private Light[] lights;
        private Transform[] lightPivots;
        private float[] lightIntensities;

        private Transform centre;
        private Transform anchor;
        private Vector3 anchorOffset;
        private bool anchored;
        private BossWeakPoint emplacement;
        private Camera viewer;
        private Player target;
        private Collider targetBody;

        private float radius;
        private float height;
        private float startAngle;
        private float direction = 1f;

        /// <summary>
        /// Which way round the ring the beam runs: +1 the way RotateAround takes
        /// a positive angle, which is the way the boss travels while
        /// TravellingLeft. Taken from the round on every shot - see
        /// <see cref="Fire"/>.
        /// </summary>
        public float Direction => direction;

        private float elapsed;
        private float fadeStart;
        private float contactTimer;
        private bool firing;
        private bool fading;

        /// <summary>
        /// Builds the beam object, taking its look from the round it replaces so
        /// the weapon's art lives in one place. Its direction comes from the round
        /// as well, but per shot rather than here - see <see cref="Fire"/>.
        /// </summary>
        public static BossLanceBeam Create(GameObject roundPrefab, Light lightTemplate, int lightCount)
        {
            var host = new GameObject("Boss Lance Beam");

            // Left at the origin, unrotated and unscaled, and it has to stay that
            // way: the arc below is computed in world space and written straight
            // into the mesh, so any transform on the host would be applied to it a
            // second time. It is deliberately not parented to the boss for the same
            // reason; it follows the boss itself, in Follow.
            host.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            BossLanceBeam beam = host.AddComponent<BossLanceBeam>();
            beam.Configure(roundPrefab, lightTemplate, lightCount);
            host.SetActive(false);
            return beam;
        }

        /// <summary>
        /// The mesh is made in code and belongs to no object, so it goes when
        /// the beam does - BossEmitter destroys the beam with itself. The lights
        /// are children and go on their own.
        /// </summary>
        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        private void Configure(GameObject roundPrefab, Light lightTemplate, int lightCount)
        {
            filter = GetComponent<MeshFilter>();
            body = GetComponent<MeshRenderer>();

            mesh = new Mesh { name = "Boss Lance Beam" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;

            if (roundPrefab != null)
            {
                // In children, not on the root. The round is a pivot with the art
                // parented under it - measured, not assumed: the root carries the
                // ShootScript and the collider, and tiro1 the art.
                //
                // Its two materials are this beam's two ribbons, in submesh order:
                // BossLance.mat for the core and BossLanceGlow.mat for the glow,
                // both put there by BossLanceBuilder. Both shaders read the UVs
                // Rebuild writes, so on the round's own model they would draw
                // nonsense, which matters to nothing because nothing spawns it.
                Renderer art = roundPrefab.GetComponentInChildren<Renderer>(includeInactive: true);

                if (art != null)
                {
                    body.sharedMaterials = art.sharedMaterials;
                }
            }

            direction = DirectionOf(roundPrefab, direction);

            // Shadows off deliberately. A beam is a light source in the fiction and
            // the thing it would shadow is the arena it is lighting, which reads as
            // the beam punching a hole in its own glow.
            body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            BuildTopology();
            BuildLights(lightTemplate, lightCount);
        }

        /// <summary>
        /// The way a round flies round the ring, from the sign of its own speed -
        /// which is where left and right have always been encoded on this attack -
        /// or <paramref name="fallback"/> when it has none to read.
        /// </summary>
        private static float DirectionOf(GameObject roundPrefab, float fallback)
        {
            ShootScript round = roundPrefab != null
                ? roundPrefab.GetComponentInChildren<ShootScript>(includeInactive: true)
                : null;

            return round != null && round.Speed != 0f ? Mathf.Sign(round.Speed) : fallback;
        }

        /// <summary>
        /// Vertices and triangles are laid out once and only the positions move
        /// afterwards. The arc always uses the same number of segments however
        /// short it is, so the counts never change and the index buffers are
        /// written exactly once in the life of the fight.
        /// </summary>
        private void BuildTopology()
        {
            int perRibbon = (Segments + 1) * 2;
            vertices = new Vector3[perRibbon * 2];
            uvs = new Vector2[vertices.Length];

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(RibbonTriangles(0), 0, calculateBounds: false);
            mesh.SetTriangles(RibbonTriangles(perRibbon), 1, calculateBounds: false);
        }

        /// <summary>
        /// Each cut's two vertices are the ribbon's two edges: the spine minus
        /// and plus an offset along cross(along, to camera). Wound minus, plus,
        /// next minus. Worked through with the triple product, that order puts
        /// every face's normal toward the camera, which is the side an opaque,
        /// single-sided core is drawn from.
        /// </summary>
        private static int[] RibbonTriangles(int first)
        {
            var triangles = new int[Segments * 6];

            for (int i = 0; i < Segments; i++)
            {
                int minus = first + i * 2;
                int t = i * 6;

                triangles[t] = minus;
                triangles[t + 1] = minus + 1;
                triangles[t + 2] = minus + 2;
                triangles[t + 3] = minus + 1;
                triangles[t + 4] = minus + 3;
                triangles[t + 5] = minus + 2;
            }

            return triangles;
        }

        private void BuildLights(Light template, int count)
        {
            if (template == null || count <= 0)
            {
                return;
            }

            lights = new Light[count];
            lightPivots = new Transform[count];
            lightIntensities = new float[count];

            for (int i = 0; i < count; i++)
            {
                Light copy = Instantiate(template, transform);
                copy.name = "Lance Beam Light " + (i + 1);
                copy.transform.localPosition = Vector3.zero;

                // The beam is one object where the rounds were forty, so its lights
                // are not competing with themselves for cluster cells the way
                // theirs were. Shadows still stay off: the caster would be the beam
                // itself, which is not solid.
                copy.shadows = LightShadows.None;
                copy.gameObject.SetActive(true);

                lights[i] = copy;
                lightPivots[i] = copy.transform;
                lightIntensities[i] = copy.intensity;
            }
        }

        /// <summary>
        /// Fires from a point on the prow, onto the orbit lane rather than at the
        /// muzzle's own radius, because that is where the rounds always ended up -
        /// laneResponse pulled every one of them onto it within about a fifth of a
        /// second.
        ///
        /// The owner is what the beam stays attached to while it is on, and the
        /// emplacement is the prow's weak point: shoot it out while the beam is
        /// firing and the beam goes out with it, as the charge already does.
        /// Either may be null, and a beam with no owner simply stays where it was
        /// fired.
        ///
        /// The round is the one the boss would fire the way it is travelling now,
        /// and the beam runs the way that round flies. It is read on every shot.
        /// Until 25 September 2026 it was read once, when the beam was built on the
        /// first lance, and kept for the rest of the fight - and the boss turns
        /// round every time the player crosses it. So whenever the boss was
        /// travelling the other way from its first lance, the beam ran out of its
        /// tail, back across its own hull and away from the player it was chasing.
        /// Reported from play that day.
        /// </summary>
        public void Fire(Vector3 origin, Transform arenaCentre, Transform owner, BossWeakPoint mounting,
                         GameObject round)
        {
            direction = DirectionOf(round, direction);
            centre = arenaCentre;

            if (centre == null)
            {
                return;
            }

            anchor = owner;
            anchored = owner != null;
            anchorOffset = anchored ? owner.InverseTransformPoint(origin) : origin;
            emplacement = mounting;

            Vector3 offset = origin - centre.position;
            offset.y = 0f;

            startAngle = BearingOf(offset);
            radius = ArenaGeometry.LaneRadius;
            height = origin.y;

            elapsed = 0f;
            contactTimer = 0f;
            fading = false;
            firing = true;

            gameObject.SetActive(true);
            Step(0f);
        }

        private void Update()
        {
            if (!firing)
            {
                return;
            }

            elapsed += Time.deltaTime;

            if (!fading)
            {
                bool ownerGone = anchored && anchor == null;
                bool mountingGone = emplacement != null && emplacement.Destroyed;

                if (ownerGone || mountingGone || elapsed >= ExtendSeconds + HoldSeconds)
                {
                    fading = true;
                    fadeStart = elapsed;
                }
            }

            if (fading && elapsed - fadeStart >= FadeSeconds)
            {
                firing = false;
                gameObject.SetActive(false);
                return;
            }

            Step(Time.deltaTime);
        }

        private void Step(float deltaTime)
        {
            Follow();

            // Once it starts to go out it stops growing, so a beam cut short
            // mid-extend thins out where it got to rather than finishing first.
            float grown = fading ? fadeStart : elapsed;
            float travel = ReachDegrees * Mathf.Clamp01(grown / ExtendSeconds);

            float thin = fading ? 1f - Mathf.Clamp01((elapsed - fadeStart) / FadeSeconds) : 1f;
            float punch = Mathf.Lerp(FirePunch, 1f, Mathf.Clamp01(elapsed / PunchSeconds));

            Rebuild(travel, thin * punch);
            PlaceLights(travel, thin);

            if (fading)
            {
                contactTimer = 0f;
                return;
            }

            ApplyDamage(travel, deltaTime);
        }

        /// <summary>
        /// Keeps the beam's root on the prow as the boss moves round the ring.
        ///
        /// The bearing follows; the height does not. The boss holds its height
        /// today - its EnemyMovement has a chase radius of 0, so it never takes
        /// up the player's - but the height at the moment of firing is the whole
        /// of what the charge warned about, and a beam that followed the prow up
        /// or down would move the thing the player spent the charge leaving. So
        /// it stays put even if the boss is ever given a chase. The boss moves
        /// round the ring slowly enough that the root stays on the prow for the
        /// half second this is on.
        /// </summary>
        private void Follow()
        {
            if (!anchored || anchor == null)
            {
                return;
            }

            Vector3 offset = anchor.TransformPoint(anchorOffset) - centre.position;
            offset.y = 0f;
            startAngle = BearingOf(offset);
        }

        /// <summary>
        /// Bearing to a direction, in Unity's sense of the word rather than the
        /// textbook one.
        ///
        /// This is the trap the first version fell into. A round travels by
        /// Transform.RotateAround about Vector3.up, and a positive angle there
        /// takes +X toward -Z. The obvious (cos, sin) parametrisation takes +X
        /// toward +Z, so a beam built from it ran the opposite way round the ring
        /// from the rounds it replaced - correct in every other respect, and
        /// pointing the wrong way.
        ///
        /// Written as a rotation rather than as trigonometry with a minus sign in
        /// it, so it is the same statement the rounds make and cannot drift from
        /// them again.
        /// </summary>
        private Vector3 RadialAt(float degrees)
        {
            return Quaternion.Euler(0f, degrees, 0f) * Vector3.right;
        }

        /// <summary>
        /// The inverse of <see cref="RadialAt"/>: which bearing a point sits at.
        /// The negated z is what makes it the inverse rather than a mirror of it.
        /// </summary>
        private static float BearingOf(Vector3 offset)
        {
            return Mathf.Atan2(-offset.z, offset.x) * Mathf.Rad2Deg;
        }

        private Vector3 PointAt(float degrees)
        {
            Vector3 radial = RadialAt(degrees);
            Vector3 origin = centre.position;
            return new Vector3(origin.x + radial.x * radius, height, origin.z + radial.z * radius);
        }

        /// <summary>
        /// Writes both ribbons along the arc from the prow out to
        /// <paramref name="travel"/> degrees, each turned to face the camera.
        ///
        /// Facing the camera is what makes a flat strip read as a round beam,
        /// and it is why this is no longer a tube: a square cross-section shows
        /// its faces and its edges, and a laser has neither.
        /// </summary>
        private void Rebuild(float travel, float widthScale)
        {
            Vector3 eye = ResolveViewer();
            int glowFirst = (Segments + 1) * 2;
            float unitsPerDegree = radius * Mathf.Deg2Rad;
            float length = travel * unitsPerDegree;

            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                float angle = startAngle + direction * travel * t;
                Vector3 spine = PointAt(angle);

                // The way the cuts run, which is what the winding in
                // RibbonTriangles was worked out against.
                Vector3 along = RadialAt(angle + 90f) * direction;
                Vector3 across = Vector3.Cross(along, eye - spine);

                across = across.sqrMagnitude > 1e-8f ? across.normalized : Vector3.up;

                float fromProw = length * t;
                float tip = Mathf.Sqrt(Mathf.Clamp01((length - fromProw) / TipTaper));
                float scale = widthScale * tip * 0.5f;

                Ribbon(i * 2, spine, across * (CoreWidth * scale), fromProw);
                Ribbon(glowFirst + i * 2, spine, across * (GlowWidth * scale), fromProw);
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void Ribbon(int at, Vector3 spine, Vector3 halfWidth, float distance)
        {
            vertices[at] = spine - halfWidth;
            vertices[at + 1] = spine + halfWidth;
            uvs[at] = new Vector2(distance, 0f);
            uvs[at + 1] = new Vector2(distance, 1f);
        }

        /// <summary>
        /// Where the ribbons face. The game has one camera, and it is found
        /// rather than handed in because the boss does not know it either. With
        /// none, the ribbons lie flat, as if seen from level.
        /// </summary>
        private Vector3 ResolveViewer()
        {
            if (viewer == null)
            {
                viewer = Camera.main;
            }

            return viewer != null ? viewer.transform.position : PointAt(startAngle) + Vector3.up;
        }

        private void PlaceLights(float travel, float brightness)
        {
            if (lightPivots == null)
            {
                return;
            }

            for (int i = 0; i < lightPivots.Length; i++)
            {
                // Spread across the middle of the arc rather than out to its ends,
                // so a light never sits exactly on the tip where half its range is
                // spent lighting the empty ring beyond the beam.
                float t = (i + 0.5f) / lightPivots.Length;
                lightPivots[i].position = PointAt(startAngle + direction * travel * t);
                lights[i].intensity = lightIntensities[i] * brightness;
            }
        }

        /// <summary>
        /// The whole hit test, in the arena's own terms. The player is inside the
        /// beam when its bearing lies between the prow and the tip, and when it is
        /// on the beam's lane and at its height.
        ///
        /// Travel is measured forward from the prow in the direction the beam
        /// runs, so the wrap at 360 is handled once, by Repeat, rather than at
        /// every comparison.
        /// </summary>
        private void ApplyDamage(float travel, float deltaTime)
        {
            if (!ResolveTarget())
            {
                return;
            }

            Vector3 offset = target.transform.position - centre.position;
            float above = offset.y - height;
            offset.y = 0f;

            float bearing = BearingOf(offset);
            float travelled = Mathf.Repeat(direction * (bearing - startAngle), 360f);

            bool inside = travelled <= travel
                && Mathf.Abs(offset.magnitude - radius) <= LaneTolerance()
                && Mathf.Abs(above) <= HeightTolerance();

            if (!inside)
            {
                // Reset rather than decay, so leaving and re-entering costs a point
                // immediately. A player who crosses the beam twice has been hit
                // twice, whatever the clock says.
                contactTimer = 0f;
                return;
            }

            contactTimer -= deltaTime;

            if (contactTimer > 0f)
            {
                return;
            }

            contactTimer = DamageInterval;
            target.TakeBeamHit();
        }

        private bool ResolveTarget()
        {
            if (target != null)
            {
                return true;
            }

            // Any, not First: there is one player, and FindFirstObjectByType is
            // deprecated on 6.6 for relying on instance-id ordering to decide
            // which of several it means. That distinction cannot arise here.
            target = FindAnyObjectByType<Player>();

            if (target == null)
            {
                return false;
            }

            targetBody = target.GetComponent<Collider>();
            return true;
        }

        /// <summary>
        /// Half the beam plus half the ship. The rounds hit through a collider
        /// overlap, so a beam that only counted its own width would be harder
        /// to be hit by than the stream it replaced - the ship would have to be
        /// centred on the lane rather than touching it.
        /// </summary>
        private float LaneTolerance()
        {
            float ship = targetBody != null
                ? Mathf.Max(targetBody.bounds.extents.x, targetBody.bounds.extents.z)
                : 0f;

            return HitWidth * 0.5f + ship;
        }

        private float HeightTolerance()
        {
            float ship = targetBody != null ? targetBody.bounds.extents.y : 0f;
            return HitWidth * 0.5f + ship;
        }
    }
}
