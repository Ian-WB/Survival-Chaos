using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Fireflies drifting through the dead trees around the island.
    ///
    /// One particle system for all seventy-three trees, not one per tree. Each
    /// birth is placed by hand through ParticleSystem.Emit, which costs a call
    /// and buys exact placement: the alternative is a shape module, and no shape
    /// Shuriken has is "the canopies of seventy-three trees scattered round a
    /// ring". A box over the island would put half the swarm above the lava,
    /// where glowing motes read as embers rather than as insects.
    ///
    /// The trees are found by name. They arrive as children of the scenario
    /// model, so there is nothing else to match on - no tag, no layer, no
    /// component of their own - and the names are the art's, in Portuguese.
    /// Finding none is reported rather than passed over, because an empty swarm
    /// and a working swarm look identical from outside this component.
    ///
    /// The glow is the material's, not a light's. Seventy-three trees of real
    /// point lights would cost more than the rest of the scene put together, and
    /// HDRP's bloom turns an emissive particle into a soft bead for nothing. It
    /// does mean fireflies light nothing around them, which at this size and
    /// this camera distance is not a thing anyone can see them failing to do.
    ///
    /// **What keeps them in the trees is the noise strength, not the placement.**
    /// A firefly is born inside its canopy and then goes wherever the noise
    /// module carries it for the rest of its life, and over several seconds
    /// that is much further than a tree is wide. Measured as the mean distance
    /// from a firefly to the nearest tree's centre, at a settled population:
    /// noise at 0.18-0.42 puts them 2.06 units out with 11% of them within a
    /// unit of any tree, which on trees about 1.0-2.1 units across is not a
    /// tree with fireflies in it but an evenly sprinkled island. At 0.05-0.12
    /// it is 0.57 units and 90%. Frequency and scroll speed were raised to pay
    /// some of that back as liveliness: they move as much per second, over a
    /// shorter leash.
    ///
    /// **Brightening them takes their colour away.** The start colour on the
    /// particle system is an HDR value, and the tone mapper desaturates hard
    /// above a point that sits just past where it is set. Measured against a
    /// control frame with the swarm emptied: at (3.5, 5.5, 0.8) the lit pixels
    /// come back at hue 51-60 and saturation 0.44, which is the yellow-green
    /// that makes them read as insects rather than as sparks off the lava; one
    /// step up at (5.5, 7.5, 1.2) saturation collapses to 0.04 and they are
    /// white. Presence past that point has to be bought with start size, which
    /// covers more pixels without pushing any one of them further up the curve.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class TreeFireflies : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The scenery to search for trees. Left empty, the whole scene is searched, " +
                 "which works but walks every renderer in it.")]
        private Transform scenery;

        [SerializeField]
        [Tooltip("Name a renderer must start with to count as a tree. The scenario model names " +
                 "them arvore 1, arvore 2.003 and so on - arvore is Portuguese for tree.")]
        private string treeNamePrefix = "arvore";

        [SerializeField]
        [Range(0f, 8f)]
        [Tooltip("How many fireflies hover around each tree on average. The emission rate is " +
                 "worked out from this and the lifetime on the particle system.")]
        private float firefliesPerTree = 2f;

        private ParticleSystem swarm;
        private readonly List<Bounds> canopies = new List<Bounds>();
        private ParticleSystem.EmitParams birth;
        private float pending;
        private int nextTree;

        /// <summary>How many trees the swarm found. Zero means it is doing nothing.</summary>
        public int TreeCount => canopies.Count;

        private void Awake()
        {
            swarm = GetComponent<ParticleSystem>();
        }

        private void OnEnable()
        {
            Collect();

            // Started by hand rather than left to playOnAwake. A particle system
            // that is not yet playing drops Emit on the floor and returns nothing
            // to say it did, and whether playOnAwake has run by the time this
            // does is script execution order that nothing here sets. The failure
            // that would cause is an island with no fireflies on it and no error
            // anywhere to explain why.
            swarm.Play();

            Prewarm();
        }

        private void Update()
        {
            if (canopies.Count == 0)
            {
                return;
            }

            // Fractional births are carried rather than rounded away. At two per
            // tree over a six second life the whole island births about
            // twenty-four a second, so a 60Hz frame is owed well under one.
            pending += FireflyField.BirthRate(canopies.Count, firefliesPerTree, MeanLifetime()) * Time.deltaTime;

            int due = Mathf.FloorToInt(pending);
            pending -= due;

            for (int i = 0; i < due; i++)
            {
                Emit(RandomLifetime());
            }
        }

        /// <summary>
        /// Fills the swarm before the first frame is drawn.
        ///
        /// Without this the island starts bare and the fireflies fade in over the
        /// first few seconds, which looks like the effect loading rather than
        /// like dusk. Each one is emitted part-used - a random slice of a full
        /// life - so they do not then all expire together.
        /// </summary>
        private void Prewarm()
        {
            int population = Mathf.RoundToInt(canopies.Count * firefliesPerTree);

            for (int i = 0; i < population; i++)
            {
                Emit(RandomLifetime() * Random.value);
            }
        }

        private void Collect()
        {
            canopies.Clear();
            nextTree = 0;

            MeshRenderer[] found = scenery != null
                ? scenery.GetComponentsInChildren<MeshRenderer>()
                : FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);

            foreach (MeshRenderer renderer in found)
            {
                if (renderer.name.StartsWith(treeNamePrefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    // Cached, not read per birth. The trees are lightmap static,
                    // so their bounds cannot move while the scene is running.
                    canopies.Add(renderer.bounds);
                }
            }

            if (canopies.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(TreeFireflies)} found no renderer named \"{treeNamePrefix}...\" to settle on, " +
                    "so there will be no fireflies. The scenario model's trees carry Portuguese names; " +
                    "if the art was renamed, this prefix has to follow it.",
                    this);
            }
        }

        private void Emit(float lifetime)
        {
            // Each birth takes the next tree round the ring rather than one
            // drawn at random. Drawing at random gives every tree the same
            // share on average and none of them that share at any moment: at
            // two apiece across seventy-three trees, chance alone leaves about
            // a seventh of them empty and hands the surplus to their
            // neighbours, which is the even sprinkle this is meant not to be.
            Bounds canopy = canopies[nextTree];
            nextTree = (nextTree + 1) % canopies.Count;

            Vector3 point = FireflyField.PointInCanopy(canopy, Random.value, Random.value, Random.value);

            // EmitParams.position is read in the system's own simulation space.
            // World is what this wants and what the scene sets, but a system
            // switched to Local would otherwise drop the entire swarm at the
            // island's centre with nothing to say why.
            if (swarm.main.simulationSpace == ParticleSystemSimulationSpace.Local)
            {
                point = transform.InverseTransformPoint(point);
            }

            birth.position = point;
            birth.startLifetime = lifetime;
            swarm.Emit(birth, 1);
        }

        private float RandomLifetime()
        {
            ParticleSystem.MinMaxCurve life = swarm.main.startLifetime;

            return life.mode == ParticleSystemCurveMode.TwoConstants
                ? Random.Range(life.constantMin, life.constantMax)
                : life.constant;
        }

        private float MeanLifetime()
        {
            ParticleSystem.MinMaxCurve life = swarm.main.startLifetime;

            return life.mode == ParticleSystemCurveMode.TwoConstants
                ? (life.constantMin + life.constantMax) * 0.5f
                : life.constant;
        }
    }
}
