using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Pins the stick's reshaping: a corner of the stick flies both axes at
    /// full speed, as two held keys do, and nothing else about a push changes.
    /// </summary>
    public class StickShapeTests
    {
        private const float Tolerance = 0.0001f;

        private static readonly float Diagonal = Mathf.Sqrt(0.5f);

        [Test]
        public void AFullPushIntoACorner_ReadsFullOnBothAxes()
        {
            Vector2 square = StickShape.ToSquare(new Vector2(Diagonal, -Diagonal));

            Assert.AreEqual(1f, square.x, Tolerance);
            Assert.AreEqual(-1f, square.y, Tolerance);
        }

        [Test]
        public void APushStraightAlongAnAxis_IsUntouched()
        {
            Assert.AreEqual(new Vector2(0.6f, 0f), StickShape.ToSquare(new Vector2(0.6f, 0f)));
            Assert.AreEqual(new Vector2(0f, -1f), StickShape.ToSquare(new Vector2(0f, -1f)));
        }

        [Test]
        public void AHalfPushIntoACorner_ReadsHalfOnBothAxes()
        {
            Vector2 square = StickShape.ToSquare(new Vector2(-Diagonal, Diagonal) * 0.5f);

            Assert.AreEqual(-0.5f, square.x, Tolerance);
            Assert.AreEqual(0.5f, square.y, Tolerance);
        }

        [Test]
        public void EveryFullPush_KeepsItsDirection_AndReadsFullOnItsLargerAxis()
        {
            for (int degrees = 0; degrees < 360; degrees += 5)
            {
                float radians = degrees * Mathf.Deg2Rad;
                Vector2 stick = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector2 square = StickShape.ToSquare(stick);

                // Parallel and the same way round. Vector2.Angle would read a few
                // hundredths of a degree here from acos alone, on vectors that
                // differ only by a scale.
                Assert.AreEqual(0f, stick.x * square.y - stick.y * square.x, Tolerance, degrees + " degrees");
                Assert.Greater(Vector2.Dot(stick, square), 0f, degrees + " degrees");
                Assert.AreEqual(1f, Mathf.Max(Mathf.Abs(square.x), Mathf.Abs(square.y)), Tolerance,
                    degrees + " degrees");
            }
        }

        [Test]
        public void ACentredStick_ReadsZero()
        {
            Assert.AreEqual(Vector2.zero, StickShape.ToSquare(Vector2.zero));
        }
    }
}
