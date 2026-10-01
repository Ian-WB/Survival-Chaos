using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Keeps the wave's enemies from flying inside one another.
    ///
    /// Every enemy on the ring closes on the same bearing and the same height,
    /// the ship's, and nothing kept them apart: their colliders are triggers on
    /// a layer that does not meet itself. So a group arrived as one ship drawn
    /// several times over, and shooting it read as one enemy taking several
    /// rounds (1 October 2026).
    ///
    /// The camera looks along the radius, so what has to stay clear is each
    /// enemy's box as seen from the axis: its width along the lane and its
    /// height. Two that overlap are eased apart along whichever of the two is
    /// the shorter way out, half each. Wide, flat ships therefore stack, and a
    /// group chasing the ship arrives as a formation.
    ///
    /// Added to each enemy by <see cref="WaveDirector"/> the first time it
    /// spawns, as <see cref="EnemyArrival"/> is, so no prefab needs it and the
    /// boss never has it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpacing : MonoBehaviour
    {
        /// <summary>Clear air kept between two enemies, in world units.</summary>
        public const float Gap = 0.12f;

        /// <summary>
        /// How quickly an overlap is closed, in ShipMotion.Approach's units: most
        /// of it inside a quarter of a second, so two that meet slide apart and
        /// do not snap.
        /// </summary>
        public const float Response = 10f;

        /// <summary>Rounds of clearing a frame.</summary>
        private const int Passes = 4;

        private static readonly List<EnemySpacing> live = new List<EnemySpacing>();
        private static ApplyBounds band;
        private static Vector3 center;
        private static int lastFrame = -1;

        private BoxCollider box;

        // This frame's place and size as the camera sees them, and where the
        // pass has moved it to so far.
        private float bearing;
        private float height;
        private float halfWidth;
        private float halfHeight;
        private float startBearing;
        private float startHeight;

        /// <summary>
        /// Puts <paramref name="enemy"/> among the enemies that keep clear of each
        /// other, adding the component the first time.
        /// </summary>
        /// <param name="playerBand">The band their heights are kept inside; null leaves them unclamped.</param>
        public static void Begin(GameObject enemy, Vector3 arenaCenter, ApplyBounds playerBand)
        {
            if (enemy == null)
            {
                return;
            }

            center = arenaCenter;
            band = playerBand;

            if (!enemy.TryGetComponent(out EnemySpacing _))
            {
                enemy.AddComponent<EnemySpacing>();
            }
        }

        /// <summary>
        /// How far one of two overlapping boxes has to move, and along which
        /// axis, to clear the other: half the overlap on the axis where it is
        /// smaller, with the sign that carries the first box away from the
        /// second. The second moves by the same amount the other way. Zero on
        /// both when they are already clear.
        /// </summary>
        /// <param name="along">The second box's centre minus the first's, along the lane.</param>
        /// <param name="up">The same, in height.</param>
        /// <param name="reachAlong">Both half widths and the gap, added up.</param>
        /// <param name="reachUp">Both half heights and the gap, added up.</param>
        public static void Clear(
            float along, float up, float reachAlong, float reachUp, out float moveAlong, out float moveUp)
        {
            moveAlong = 0f;
            moveUp = 0f;

            float overAlong = reachAlong - Mathf.Abs(along);
            float overUp = reachUp - Mathf.Abs(up);

            if (overAlong <= 0f || overUp <= 0f)
            {
                return;
            }

            // Two on exactly the same spot have no way out to prefer, and the
            // first is sent down or back. Any answer does, so long as the two
            // get opposite ones.
            if (overUp <= overAlong)
            {
                moveUp = (up >= 0f ? -0.5f : 0.5f) * overUp;
            }
            else
            {
                moveAlong = (along >= 0f ? -0.5f : 0.5f) * overAlong;
            }
        }

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
        }

        private void OnEnable()
        {
            live.Add(this);
        }

        private void OnDisable()
        {
            live.Remove(this);
        }

        /// <summary>
        /// After every enemy has moved. One pass a frame for all of them, run by
        /// whichever gets here first.
        /// </summary>
        private void LateUpdate()
        {
            if (lastFrame == Time.frameCount)
            {
                return;
            }

            lastFrame = Time.frameCount;
            SpaceOut(Time.deltaTime);
        }

        private static void SpaceOut(float deltaTime)
        {
            if (deltaTime <= 0f || live.Count < 2)
            {
                return;
            }

            float lane = ArenaGeometry.LaneRadius;

            if (lane <= 0f)
            {
                return;
            }

            bool banded = band != null;
            float floor = 0f;
            float ceiling = 0f;

            if (banded)
            {
                banded = band.TryGetBand(out floor, out ceiling);
            }

            foreach (EnemySpacing enemy in live)
            {
                enemy.Measure();
            }

            // Worked out on paper first, several times over: in a crowd, clearing
            // one neighbour pushes into the next, and a single round leaves the
            // ones in the middle squeezed from both sides.
            for (int pass = 0; pass < Passes; pass++)
            {
                for (int i = 0; i < live.Count; i++)
                {
                    EnemySpacing a = live[i];

                    for (int j = i + 1; j < live.Count; j++)
                    {
                        EnemySpacing b = live[j];

                        float up = b.height - a.height;
                        float reachUp = a.halfHeight + b.halfHeight + Gap;

                        if (Mathf.Abs(up) >= reachUp)
                        {
                            continue;
                        }

                        float along = Mathf.DeltaAngle(a.bearing, b.bearing) * Mathf.Deg2Rad * lane;

                        Clear(along, up, a.halfWidth + b.halfWidth + Gap, reachUp, out float pushAlong, out float pushUp);

                        float turn = pushAlong / lane * Mathf.Rad2Deg;
                        a.bearing += turn;
                        b.bearing -= turn;

                        if (!banded)
                        {
                            a.height += pushUp;
                            b.height -= pushUp;
                            continue;
                        }

                        // The ship cannot leave its band, so an enemy pushed out
                        // of it would be one no round could reach. What the edge
                        // refuses one of them, the other makes up: a row along
                        // the floor would otherwise never part.
                        float wantA = a.height + pushUp;
                        float gotA = Mathf.Clamp(wantA, floor, ceiling);
                        float wantB = b.height - pushUp - (wantA - gotA);
                        float gotB = Mathf.Clamp(wantB, floor, ceiling);

                        a.height = Mathf.Clamp(gotA - (wantB - gotB), floor, ceiling);
                        b.height = gotB;
                    }
                }
            }

            // Frame-rate independent share of the way there, as Approach takes it.
            float share = 1f - Mathf.Exp(-Response * deltaTime);

            foreach (EnemySpacing enemy in live)
            {
                enemy.Move(share);
            }
        }

        private void Measure()
        {
            Vector3 position = transform.position;
            bearing = PickupPlacement.BearingOf(position, center);
            height = position.y;
            startBearing = bearing;
            startHeight = height;

            // Facing the axis, an enemy's own x runs along the lane. Read at its
            // current scale, so one still growing in takes the room it has grown
            // to and no more.
            Vector3 scale = transform.lossyScale;
            Vector3 size = box != null ? box.size : new Vector3(1f, 0.4f, 0.4f);
            halfWidth = Mathf.Abs(size.x * scale.x) * 0.5f;
            halfHeight = Mathf.Abs(size.y * scale.y) * 0.5f;
        }

        private void Move(float share)
        {
            float turn = Mathf.DeltaAngle(startBearing, bearing) * share;
            float climb = (height - startHeight) * share;

            if (turn == 0f && climb == 0f)
            {
                return;
            }

            Vector3 position = transform.position;

            if (turn != 0f)
            {
                // Round the axis, which keeps whatever radius it is at: one still
                // flying in is turned aside, not put on the lane early.
                Vector3 offset = position - center;
                offset.y = 0f;
                offset = Quaternion.AngleAxis(turn, Vector3.up) * offset;
                position.x = center.x + offset.x;
                position.z = center.z + offset.z;
            }

            position.y += climb;
            transform.position = position;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            live.Clear();
            band = null;
            center = Vector3.zero;
            lastFrame = -1;
        }
    }
}
