using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The prow lance, drawn as one continuous beam along the ring rather than as
    /// a stream of projectiles.
    ///
    /// It was forty rounds until 8 September, and the measurement that ended that
    /// is worth keeping: the ring is 862 units around and a lance round crossed it
    /// at 191.6 units a second, so rounds leaving 0.06s apart were 11.5 units
    /// apart while each was 12.86 units long. Consecutive rounds overlapped by 1.4
    /// units before they had gone anywhere. The stream was never a row of bullets
    /// waiting to be separated - it was already a solid line, costing forty
    /// objects, forty colliders and forty lights to draw one thing, into a light
    /// cluster that holds 24 lights per cell and silently drops the rest.
    ///
    /// Welding them into four 33-unit rounds was tried first and rejected on
    /// sight: at that length they read as sticks flying in formation, not as a
    /// beam. So this draws the arc itself.
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
        /// How far the beam reaches behind its own head, in units of arc.
        ///
        /// 128 is not a taste: it is what the forty-round stream covered - 0.6s of
        /// firing at 191.6 units a second plus the length of the last round - so
        /// the beam threatens exactly the span the rounds did. The attack is not
        /// bigger than it was, only continuous.
        /// </summary>
        private const float BeamLength = 128f;

        /// <summary>
        /// Cross-section, matching the round it replaces so the art still reads as
        /// the same weapon. The rounds measured 2.00 by 2.00.
        /// </summary>
        private const float Thickness = 2f;

        /// <summary>
        /// Segments along the arc. The mesh is rebuilt every frame, so this is a
        /// per-frame cost - but at 24 it is 392 vertices, which is less than the
        /// single round this replaced forty of.
        ///
        /// It is also what keeps the beam on the lane. A straight mesh across the
        /// whole 128 units would sit 15.8 units off the true arc at this radius;
        /// broken into 24 it is under a twentieth of a unit.
        /// </summary>
        private const int Segments = 24;

        /// <summary>
        /// Seconds of contact per point of damage.
        ///
        /// The old lance had no rate at all: it was one point per round that
        /// touched you, so standing in it was up to forty against twenty health -
        /// death twice over - and clipping its edge was one. A beam wants the
        /// opposite shape, where brushing it is cheap and staying in it is fatal,
        /// and that is a rate rather than a count. At 0.15 a full pass costs about
        /// four, and refusing to move costs everything.
        /// </summary>
        private const float DamageInterval = 0.15f;

        /// <summary>
        /// How long the beam lives after the head leaves the prow.
        ///
        /// Held under the point where the head laps the tail: at 80 degrees a
        /// second, two seconds is 160 degrees of a 360 degree ring, so the arc
        /// test below never has to reason about a beam that overlaps itself.
        /// </summary>
        private const float Life = 2f;

        private MeshFilter filter;
        private MeshRenderer body;
        private Mesh mesh;

        private Vector3[] vertices;
        private Light[] lights;
        private Transform[] lightPivots;

        private Transform centre;
        private Player target;
        private Collider targetBody;

        private float radius;
        private float height;
        private float startAngle;
        private float degreesPerSecond;

        private float elapsed;
        private float contactTimer;
        private bool firing;

        /// <summary>
        /// Builds the beam object, taking its look from the round it replaces so
        /// the weapon still reads as the same weapon, and its direction from that
        /// round's own speed - which is where left and right have always been
        /// encoded on this attack.
        /// </summary>
        public static BossLanceBeam Create(GameObject roundPrefab, Light lightTemplate, int lightCount)
        {
            var host = new GameObject("Boss Lance Beam");

            // Left at the origin, unrotated and unscaled, and it has to stay that
            // way: the arc below is computed in world space and written straight
            // into the mesh, so any transform on the host would be applied to it a
            // second time. It is deliberately not parented to the boss for the same
            // reason - the boss moves, and the beam it has already fired does not.
            host.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            BossLanceBeam beam = host.AddComponent<BossLanceBeam>();
            beam.Configure(roundPrefab, lightTemplate, lightCount);
            host.SetActive(false);
            return beam;
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
                // ShootScript and the collider, and tiro1 carries the one material
                // over one submesh, which is exactly the shape this mesh is.
                Renderer art = roundPrefab.GetComponentInChildren<Renderer>(includeInactive: true);

                if (art != null)
                {
                    body.sharedMaterials = art.sharedMaterials;
                }

                ShootScript round = roundPrefab.GetComponentInChildren<ShootScript>(includeInactive: true);

                if (round != null)
                {
                    degreesPerSecond = round.Speed;
                }
            }

            // Shadows off deliberately. A beam is a light source in the fiction and
            // the thing it would shadow is the arena it is lighting, which reads as
            // the beam punching a hole in its own glow.
            body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            BuildTopology();
            BuildLights(lightTemplate, lightCount);
        }

        /// <summary>
        /// Vertices and triangles are laid out once and only the positions move
        /// afterwards. The arc always uses the same number of segments however
        /// short it is, so the counts never change and the index buffer is written
        /// exactly once in the life of the fight.
        /// </summary>
        private void BuildTopology()
        {
            // Four faces per segment, four corners each, plus two end caps.
            int quads = Segments * 4 + 2;
            vertices = new Vector3[quads * 4];

            var triangles = new int[quads * 6];

            for (int q = 0; q < quads; q++)
            {
                int v = q * 4;
                int t = q * 6;

                // Wound the opposite way round from the obvious 0-1-2 / 0-2-3.
                // Worked through by hand for every face: with the corners ordered
                // outer-top, outer-bottom, inner-bottom, inner-top and the arc
                // walked in the direction of increasing bearing, the naive winding
                // points all six faces inward, and a beam you can only see from
                // inside is a beam nobody sees.
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
        }

        private void BuildLights(Light template, int count)
        {
            if (template == null || count <= 0)
            {
                return;
            }

            lights = new Light[count];
            lightPivots = new Transform[count];

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
            }
        }

        /// <summary>
        /// Starts a pass from a point on the prow. The beam is placed on the orbit
        /// lane rather than at the muzzle's own radius, because that is where the
        /// rounds ended up anyway - laneResponse pulled every one of them onto it
        /// within about a fifth of a second, which is the behaviour this inherits
        /// rather than reproduces.
        /// </summary>
        public void Fire(Vector3 origin, Transform arenaCentre)
        {
            centre = arenaCentre;

            if (centre == null)
            {
                return;
            }

            Vector3 offset = origin - centre.position;
            offset.y = 0f;

            startAngle = BearingOf(offset);
            radius = ArenaGeometry.OrbitRadius;
            height = origin.y;

            elapsed = 0f;
            contactTimer = 0f;
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

            if (elapsed >= Life)
            {
                firing = false;
                gameObject.SetActive(false);
                return;
            }

            Step(Time.deltaTime);
        }

        /// <summary>
        /// The head runs from the prow at the round's own speed and the tail
        /// follows one beam-length behind it, so the beam grows out of the muzzle
        /// and then slides along the ring at full length. That is the same shape
        /// the stream had - a leading round and a last round 128 units back - with
        /// the gap between them filled in rather than implied.
        /// </summary>
        private void Step(float deltaTime)
        {
            float speed = Mathf.Abs(degreesPerSecond);
            float grow = speed > 0f ? BeamLength / (radius * Mathf.Deg2Rad * speed) : 0f;

            float headTravel = speed * elapsed;
            float tailTravel = speed * Mathf.Max(0f, elapsed - grow);

            float direction = Mathf.Sign(degreesPerSecond);
            float head = startAngle + direction * headTravel;
            float tail = startAngle + direction * tailTravel;

            // Always walked in the direction of increasing bearing, whichever way
            // the beam is actually travelling. The winding in BuildTopology is
            // fixed, so building a right-to-left pass back to front would turn
            // every face inside out - and the boss fires this attack both ways.
            Rebuild(Mathf.Min(tail, head), Mathf.Max(tail, head));
            PlaceLights(tail, head);
            ApplyDamage(tailTravel, headTravel, deltaTime);
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
        /// Writes the four faces of a square tube along the arc, plus a cap at each
        /// end. Corners are ordered outer-top, outer-bottom, inner-bottom,
        /// inner-top, and each face is emitted with its own vertices so the normals
        /// come out per-face rather than smoothed across the tube's edges - which
        /// is what keeps a low-poly beam looking faceted rather than inflated.
        /// </summary>
        private void Rebuild(float from, float to)
        {
            float half = Thickness * 0.5f;
            int at = 0;

            Vector3 previousOuterTop = Vector3.zero;
            Vector3 previousOuterBottom = Vector3.zero;
            Vector3 previousInnerBottom = Vector3.zero;
            Vector3 previousInnerTop = Vector3.zero;

            Vector3 firstOuterTop = Vector3.zero;
            Vector3 firstOuterBottom = Vector3.zero;
            Vector3 firstInnerBottom = Vector3.zero;
            Vector3 firstInnerTop = Vector3.zero;

            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                float angle = Mathf.Lerp(from, to, t);

                Vector3 radial = RadialAt(angle);
                Vector3 spine = PointAt(angle);

                Vector3 outerTop = spine + radial * half + Vector3.up * half;
                Vector3 outerBottom = spine + radial * half - Vector3.up * half;
                Vector3 innerBottom = spine - radial * half - Vector3.up * half;
                Vector3 innerTop = spine - radial * half + Vector3.up * half;

                if (i == 0)
                {
                    firstOuterTop = outerTop;
                    firstOuterBottom = outerBottom;
                    firstInnerBottom = innerBottom;
                    firstInnerTop = innerTop;
                }
                else
                {
                    at = Quad(at, previousOuterTop, previousOuterBottom, outerBottom, outerTop);
                    at = Quad(at, previousInnerBottom, previousInnerTop, innerTop, innerBottom);
                    at = Quad(at, previousInnerTop, previousOuterTop, outerTop, innerTop);
                    at = Quad(at, previousOuterBottom, previousInnerBottom, innerBottom, outerBottom);
                }

                previousOuterTop = outerTop;
                previousOuterBottom = outerBottom;
                previousInnerBottom = innerBottom;
                previousInnerTop = innerTop;
            }

            at = Quad(at, firstInnerTop, firstInnerBottom, firstOuterBottom, firstOuterTop);
            Quad(at, previousOuterTop, previousOuterBottom, previousInnerBottom, previousInnerTop);

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private int Quad(int at, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            vertices[at] = a;
            vertices[at + 1] = b;
            vertices[at + 2] = c;
            vertices[at + 3] = d;
            return at + 4;
        }

        private void PlaceLights(float tail, float head)
        {
            if (lightPivots == null)
            {
                return;
            }

            for (int i = 0; i < lightPivots.Length; i++)
            {
                // Spread across the middle of the arc rather than out to its ends,
                // so a light never sits exactly on the head where half its range is
                // spent lighting the empty ring in front of the beam.
                float t = (i + 0.5f) / lightPivots.Length;
                lightPivots[i].position = PointAt(Mathf.Lerp(tail, head, t));
            }
        }

        /// <summary>
        /// The whole hit test, in the arena's own terms. The player is inside the
        /// beam when its bearing has been passed by the head and not yet by the
        /// tail, and when it is on the beam's lane and at its height.
        ///
        /// Travel is measured forward from the muzzle in the direction the beam is
        /// going, so both ends are positive numbers growing from zero and the wrap
        /// at 360 is handled once, by Repeat, rather than at every comparison.
        /// </summary>
        private void ApplyDamage(float tailTravel, float headTravel, float deltaTime)
        {
            if (!ResolveTarget())
            {
                return;
            }

            Vector3 offset = target.transform.position - centre.position;
            float above = offset.y - height;
            offset.y = 0f;

            float bearing = BearingOf(offset);
            float travelled = Mathf.Repeat(Mathf.Sign(degreesPerSecond) * (bearing - startAngle), 360f);

            bool inside = travelled >= tailTravel
                && travelled <= headTravel
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
        /// overlap, so a beam that only counted its own two units would be harder
        /// to be hit by than the stream it replaced - the ship would have to be
        /// centred on the lane rather than touching it.
        /// </summary>
        private float LaneTolerance()
        {
            float ship = targetBody != null
                ? Mathf.Max(targetBody.bounds.extents.x, targetBody.bounds.extents.z)
                : 0f;

            return Thickness * 0.5f + ship;
        }

        private float HeightTolerance()
        {
            float ship = targetBody != null ? targetBody.bounds.extents.y : 0f;
            return Thickness * 0.5f + ship;
        }
    }
}
