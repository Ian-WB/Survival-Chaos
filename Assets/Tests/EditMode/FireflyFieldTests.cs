using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The swarm's placement, which has two silent failures in it: a firefly
    /// outside its tree is a dot on the skybox, and a birth rate that drifts
    /// from the lifetime changes the population without changing the field that
    /// is supposed to set it.
    /// </summary>
    public class FireflyFieldTests
    {
        private static Bounds Tree()
        {
            // arvore 3.005, as the scene reports it.
            return new Bounds(new Vector3(-2.78f, 5.12f, -15.46f), new Vector3(2.12f, 1.60f, 1.96f));
        }

        private static Bounds LopsidedTree()
        {
            // arvore 1.036, which is twice as wide as it is deep. A tree this
            // shape is what separates a radius fitted per axis from one radius
            // taken off the wider axis and used on both - the second hangs part
            // of the swarm outside the narrow side, and every near-square tree
            // in the scene hides it.
            return new Bounds(new Vector3(7.89f, 3.37f, 15.29f), new Vector3(0.48f, 1.12f, 1.02f));
        }

        [Test]
        public void PointInCanopy_StaysInsideTheTree()
        {
            foreach (Bounds tree in new[] { Tree(), LopsidedTree() })
            {
                // The full sweep of angles at maximum reach: a radius roll of 1
                // is what pushes a point at the bounds, and the angle decides
                // which axis it is pushed along.
                for (int i = 0; i <= 36; i++)
                {
                    for (int j = 0; j <= 4; j++)
                    {
                        Vector3 point = FireflyField.PointInCanopy(tree, i / 36f, 1f, j / 4f);

                        Assert.That(point.x, Is.InRange(tree.min.x, tree.max.x));
                        Assert.That(point.y, Is.InRange(tree.min.y, tree.max.y));
                        Assert.That(point.z, Is.InRange(tree.min.z, tree.max.z));
                    }
                }
            }
        }

        [Test]
        public void PointInCanopy_FitsTheFootprintOnBothAxes()
        {
            Bounds tree = LopsidedTree();

            // Due east and due north at full reach. Each has to sit at its own
            // axis's extent, not at a shared one.
            Vector3 east = FireflyField.PointInCanopy(tree, 0f, 1f, 0.5f);
            Vector3 north = FireflyField.PointInCanopy(tree, 0.25f, 1f, 0.5f);

            Assert.AreEqual(tree.extents.x * FireflyField.CanopySpread, east.x - tree.center.x, 1e-4f);
            Assert.AreEqual(tree.extents.z * FireflyField.CanopySpread, north.z - tree.center.z, 1e-4f);
        }

        [Test]
        public void PointInCanopy_KeepsOffTheTrunk()
        {
            Bounds tree = Tree();
            float floor = tree.min.y + tree.size.y * FireflyField.CanopyFloor;

            // The lowest roll there is still has to clear the trunk, or the
            // swarm's bottom edge sits on lit rock where it reads as nothing.
            Vector3 lowest = FireflyField.PointInCanopy(tree, 0f, 0f, 0f);

            Assert.AreEqual(floor, lowest.y, 1e-4f);
            Assert.Greater(lowest.y, tree.min.y);
        }

        [Test]
        public void PointInCanopy_SpreadsEvenlyRatherThanCrowdingTheTrunk()
        {
            Bounds tree = Tree();
            float reach = tree.extents.x * FireflyField.CanopySpread;

            // Half the rolls should land beyond half the footprint's AREA, which
            // is at reach/sqrt(2), not at reach/2. Dropping the square root in
            // PointInCanopy puts this point at half the reach and fails here.
            Vector3 middle = FireflyField.PointInCanopy(tree, 0f, 0.5f, 0.5f);
            float radius = middle.x - tree.center.x;

            Assert.AreEqual(reach / Mathf.Sqrt(2f), radius, 1e-4f);
        }

        [Test]
        public void BirthRate_ReplacesThePopulationOverOneLifetime()
        {
            // 73 trees, two each, a six second life: the 146 alive have to be
            // replaced every six seconds to stay at 146.
            Assert.AreEqual(146f / 6f, FireflyField.BirthRate(73, 2f, 6f), 1e-4f);
        }

        [Test]
        public void BirthRate_IsZeroRatherThanInfiniteWhenNothingIsSet()
        {
            // A scene with no trees found, or a particle system whose lifetime
            // is zero, divides by zero on the way to an infinite emit loop.
            Assert.AreEqual(0f, FireflyField.BirthRate(0, 2f, 6f));
            Assert.AreEqual(0f, FireflyField.BirthRate(73, 0f, 6f));
            Assert.AreEqual(0f, FireflyField.BirthRate(73, 2f, 0f));
        }
    }
}
