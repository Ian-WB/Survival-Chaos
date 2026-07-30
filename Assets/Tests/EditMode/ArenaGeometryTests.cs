using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Projecting onto the orbit must keep bearing and height and change only
    /// the distance from the axis - otherwise snapping the player onto the
    /// enemy lane would also teleport it sideways or drop its altitude.
    /// </summary>
    public class ArenaGeometryTests
    {
        private const float Tolerance = 0.001f;

        private static float RadiusXZ(Vector3 point, Vector3 center)
        {
            Vector3 offset = point - center;
            offset.y = 0f;
            return offset.magnitude;
        }

        [Test]
        public void PlacesThePointAtTheRequestedRadius()
        {
            Vector3 result = ArenaGeometry.ProjectOntoOrbit(
                new Vector3(0f, 70f, -160f), Vector3.zero, ArenaGeometry.OrbitRadius);

            Assert.AreEqual(ArenaGeometry.OrbitRadius, RadiusXZ(result, Vector3.zero), Tolerance);
        }

        [Test]
        public void KeepsHeight()
        {
            Vector3 result = ArenaGeometry.ProjectOntoOrbit(
                new Vector3(0f, 70f, -160f), Vector3.zero, ArenaGeometry.OrbitRadius);

            Assert.AreEqual(70f, result.y, Tolerance);
        }

        [Test]
        public void KeepsBearingAroundTheAxis()
        {
            // Starts on -Z, so it must stay on -Z.
            Vector3 result = ArenaGeometry.ProjectOntoOrbit(
                new Vector3(0f, 70f, -160f), Vector3.zero, 137.2f);

            Assert.AreEqual(0f, result.x, Tolerance);
            Assert.AreEqual(-137.2f, result.z, Tolerance);
        }

        [Test]
        public void KeepsBearingOnADiagonal()
        {
            Vector3 result = ArenaGeometry.ProjectOntoOrbit(
                new Vector3(100f, 12f, 100f), Vector3.zero, 100f);

            // 45 degrees: both axes land on 100/sqrt(2).
            float expected = 100f / Mathf.Sqrt(2f);
            Assert.AreEqual(expected, result.x, Tolerance);
            Assert.AreEqual(expected, result.z, Tolerance);
            Assert.AreEqual(12f, result.y, Tolerance);
        }

        [Test]
        public void WorksAroundAnOffCentreAxis()
        {
            Vector3 center = new Vector3(50f, 0f, -20f);

            Vector3 result = ArenaGeometry.ProjectOntoOrbit(
                new Vector3(50f, 30f, 200f), center, 137.2f);

            Assert.AreEqual(137.2f, RadiusXZ(result, center), Tolerance);
            Assert.AreEqual(30f, result.y, Tolerance);
        }

        [Test]
        public void IgnoresHeightDifferenceWhenMeasuringRadius()
        {
            // The centre is far below the point; radius is purely horizontal.
            Vector3 center = new Vector3(0f, -500f, 0f);

            Vector3 result = ArenaGeometry.ProjectOntoOrbit(
                new Vector3(0f, 70f, -160f), center, 137.2f);

            Assert.AreEqual(137.2f, RadiusXZ(result, center), Tolerance);
            Assert.AreEqual(70f, result.y, Tolerance);
        }

        [Test]
        public void FallsBackToMinusZ_WhenSittingExactlyOnTheAxis()
        {
            Vector3 result = ArenaGeometry.ProjectOntoOrbit(
                new Vector3(0f, 40f, 0f), Vector3.zero, 137.2f);

            Assert.AreEqual(0f, result.x, Tolerance);
            Assert.AreEqual(-137.2f, result.z, Tolerance);
            Assert.AreEqual(40f, result.y, Tolerance);
        }

        [Test]
        public void NegativeRadiusIsTreatedAsZero()
        {
            Vector3 result = ArenaGeometry.ProjectOntoOrbit(
                new Vector3(0f, 70f, -160f), Vector3.zero, -50f);

            Assert.AreEqual(0f, RadiusXZ(result, Vector3.zero), Tolerance);
        }
    }
}
