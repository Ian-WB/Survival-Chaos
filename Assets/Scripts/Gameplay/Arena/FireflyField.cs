using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Where a firefly is born and how often one is born.
    ///
    /// Pure arithmetic over a tree's bounds, so the shape of the swarm can be
    /// tested without a scene. TreeFireflies owns the renderers and the particle
    /// system; this owns where the lights go.
    /// </summary>
    public static class FireflyField
    {
        /// <summary>
        /// How far up a tree the swarm starts, as a fraction of its height.
        ///
        /// The arvore meshes are bare dead trees, and their lower third is trunk
        /// standing against the island. A firefly down there is a lit pixel on
        /// top of lit rock, which reads as nothing; from a third of the way up it
        /// has branches and sky behind it.
        /// </summary>
        public const float CanopyFloor = 0.35f;

        /// <summary>
        /// How much of a tree's footprint the swarm fills, as a fraction.
        ///
        /// Well inside the branches rather than around them. The camera looks
        /// across the island rather than down at it, so a firefly past the
        /// silhouette is a dot on the skybox with nothing to belong to.
        ///
        /// This was 0.85 - nearly the whole footprint - and at that width the
        /// trees are close enough together on the ring that their swarms meet,
        /// and the island reads as evenly sprinkled rather than as trees with
        /// fireflies in them. Keeping each one to the inner half leaves dark
        /// gaps between the trees, and the gaps are what make them clusters.
        /// </summary>
        public const float CanopySpread = 0.45f;

        /// <summary>
        /// A point in one tree's canopy, from three independent 0-1 rolls.
        ///
        /// Rolls rather than a Random call of its own: the caller holds the
        /// randomness, which is what lets this be asserted against fixed values.
        /// </summary>
        public static Vector3 PointInCanopy(Bounds tree, float angleRoll, float radiusRoll, float heightRoll)
        {
            float angle = Mathf.Repeat(angleRoll, 1f) * Mathf.PI * 2f;

            // The radius roll is square rooted before it is used. A disc's area
            // grows with the square of its radius, so feeding the roll in
            // directly would pack most of the swarm onto the trunk.
            float reach = Mathf.Sqrt(Mathf.Clamp01(radiusRoll)) * CanopySpread;

            // An ellipse fitted to the footprint, not one radius used on both
            // axes. The arvore meshes are not round - several are half as deep
            // as they are wide - and a single radius taken from the wider axis
            // hangs part of the swarm outside the narrower one, which is the
            // one thing this is here to prevent.
            float floor = tree.min.y + tree.size.y * CanopyFloor;
            float height = Mathf.Lerp(floor, tree.max.y, Mathf.Clamp01(heightRoll));

            return new Vector3(
                tree.center.x + Mathf.Cos(angle) * reach * tree.extents.x,
                height,
                tree.center.z + Mathf.Sin(angle) * reach * tree.extents.z);
        }

        /// <summary>
        /// Births per second that hold a steady population.
        ///
        /// A population of n with a lifetime of l seconds loses n/l of itself
        /// every second, so replacing them at that rate is what keeps the count
        /// level. Feeding the emission rate from the authored lifetime rather
        /// than typing a rate in means retuning how long a firefly lives does
        /// not quietly change how many of them there are.
        /// </summary>
        public static float BirthRate(int trees, float perTree, float lifetime)
        {
            if (trees <= 0 || perTree <= 0f || lifetime <= 0f)
            {
                return 0f;
            }

            return trees * perTree / lifetime;
        }
    }
}
