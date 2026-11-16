using System;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>The upgrades that bolt something onto the ship. Heal does not: it adds nothing to keep.</summary>
    public enum ShipUpgrade
    {
        Hydra,
        AttackSpeed,
        MaxHealth,
        MoveSpeed,
        DashRecovery,
        SlowMo,
        Deflector,
        Magnet,
    }

    /// <summary>One pick's worth of hull: the mesh it adds, and what it is made of.</summary>
    [Serializable]
    public struct ShipPartStage
    {
        public ShipUpgrade upgrade;

        [Min(1)]
        [Tooltip("The pick of that upgrade this arrives with: 1 for the first.")]
        public int pick;

        [Tooltip("In the ship model's own space, nose along +X, so it needs no placing.")]
        public Mesh mesh;

        [Tooltip("One per submesh: hull, dark hull, trim, glow.")]
        public Material[] materials;
    }

    /// <summary>
    /// The parts an upgrade adds to the ship, so the build a run has made is
    /// on the ship and not only in its numbers.
    ///
    /// Until 16 November 2026 the ship that ended a run was the ship that
    /// started it. Eight guns fired out of the same bare nose as one did, and
    /// the only record of four armour picks was a longer bar in the corner.
    /// Every pick now fits one piece, and all of them together are the ship's
    /// finished design: the starting model is that design with everything
    /// taken off.
    ///
    /// The parts are meshes authored in the model's own space by
    /// <c>ShipPartsBuilder</c>, built here once and switched on as picks
    /// arrive. Built in Awake and never destroyed, for the reason the flares
    /// are: <see cref="HitFlash"/> lists a hull's renderers once, and a part
    /// added after that would stay dark through every hit.
    ///
    /// They are looks only. The ship's collider does not grow with them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipParts : MonoBehaviour
    {
        [SerializeField]
        private ShipPartStage[] stages = new ShipPartStage[0];

        [Header("Fitting")]
        [SerializeField]
        [Tooltip("How fast a part snaps on, in cycles a second.")]
        private float fitFrequency = 4.5f;

        [SerializeField]
        [Range(0.1f, 1f)]
        [Tooltip("Under 1 the part overshoots its size and settles, which is what makes it read " +
                 "as arriving and not as having been there.")]
        private float fitDamping = 0.42f;

        private Transform[] pivots;
        private Renderer[] renderers;
        private Spring[] growth;
        private bool[] fitting;
        private int[] picks;
        private int fittingCount;

        /// <summary>The stages this ship can carry, for the tests and the tools.</summary>
        public ShipPartStage[] Stages => stages;

        /// <summary>Whether a stage is on a ship that has taken this many picks of its upgrade.</summary>
        public static bool Shows(int stagePick, int picksTaken)
        {
            return stagePick >= 1 && picksTaken >= stagePick;
        }

        /// <summary>How many picks of an upgrade the ship is wearing.</summary>
        public int Fitted(ShipUpgrade upgrade)
        {
            return picks != null ? picks[(int)upgrade] : 0;
        }

        private void Awake()
        {
            int count = stages != null ? stages.Length : 0;

            pivots = new Transform[count];
            renderers = new Renderer[count];
            growth = new Spring[count];
            fitting = new bool[count];
            picks = new int[Enum.GetValues(typeof(ShipUpgrade)).Length];

            // Read before any part exists, so it is the model's own.
            MeshRenderer hull = GetComponentInChildren<MeshRenderer>(includeInactive: true);

            for (int i = 0; i < count; i++)
            {
                ShipPartStage stage = stages[i];

                if (stage.mesh == null || stage.materials == null || stage.materials.Length == 0)
                {
                    continue;
                }

                // Grown from its own middle, not the ship's: a part scaled
                // about the ship's origin would slide out of the hull.
                Vector3 middle = stage.mesh.bounds.center;

                var pivot = new GameObject(stage.upgrade + " " + stage.pick);
                pivot.layer = gameObject.layer;
                pivot.transform.SetParent(transform, false);
                pivot.transform.localPosition = middle;
                pivot.transform.localScale = Vector3.zero;

                var body = new GameObject("Part");
                body.layer = gameObject.layer;
                body.transform.SetParent(pivot.transform, false);
                body.transform.localPosition = -middle;

                body.AddComponent<MeshFilter>().sharedMesh = stage.mesh;

                MeshRenderer renderer = body.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = stage.materials;

                // Lit as the hull is. The model sits on a light layer of its
                // own, and a part left on the default one misses the light
                // that layer is for: the first parts drew near black beside
                // a hull in full colour.
                if (hull != null)
                {
                    renderer.renderingLayerMask = hull.renderingLayerMask;
                    renderer.lightProbeUsage = hull.lightProbeUsage;
                    renderer.shadowCastingMode = hull.shadowCastingMode;
                    renderer.receiveShadows = hull.receiveShadows;
                }

                renderer.enabled = false;

                pivots[i] = pivot.transform;
                renderers[i] = renderer;
            }
        }

        /// <summary>
        /// Tells the ship how many picks of an upgrade it has. Every stage up
        /// to that pick goes on; none comes off. Safe to call with a count it
        /// already has.
        /// </summary>
        public void Set(ShipUpgrade upgrade, int picksTaken)
        {
            if (picks == null || picksTaken <= picks[(int)upgrade])
            {
                return;
            }

            picks[(int)upgrade] = picksTaken;

            for (int i = 0; i < stages.Length; i++)
            {
                if (renderers[i] == null || renderers[i].enabled
                    || stages[i].upgrade != upgrade || !Shows(stages[i].pick, picksTaken))
                {
                    continue;
                }

                renderers[i].enabled = true;
                growth[i].Reset(0f);
                fitting[i] = true;
                fittingCount++;
            }
        }

        private void Update()
        {
            if (fittingCount == 0)
            {
                return;
            }

            // Real time: a pick can land inside Slow Mo or a hit-stop, and
            // the part should still arrive at the speed it always does.
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            for (int i = 0; i < fitting.Length; i++)
            {
                if (!fitting[i])
                {
                    continue;
                }

                growth[i].Step(1f, fitFrequency, fitDamping, delta);

                bool settled = Mathf.Abs(growth[i].Value - 1f) < 0.004f && Mathf.Abs(growth[i].Velocity) < 0.05f;
                float size = settled ? 1f : Mathf.Max(0f, growth[i].Value);

                pivots[i].localScale = new Vector3(size, size, size);

                if (settled)
                {
                    fitting[i] = false;
                    fittingCount--;
                }
            }
        }
    }
}
