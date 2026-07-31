using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    public class ShipMotionTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Approach_MovesTowardTheTarget()
        {
            float value = ShipMotion.Approach(0f, 10f, 5f, 0.1f);

            Assert.Greater(value, 0f);
            Assert.Less(value, 10f);
        }

        [Test]
        public void Approach_NeverOvershoots_EvenWithAHugeStep()
        {
            float value = ShipMotion.Approach(0f, 10f, 5f, 100f);

            Assert.LessOrEqual(value, 10f);
            Assert.That(value, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void Approach_ConvergesOnTheTarget()
        {
            float value = 0f;
            for (int i = 0; i < 500; i++)
            {
                value = ShipMotion.Approach(value, 10f, 8f, 1f / 60f);
            }

            Assert.AreEqual(10f, value, 0.001f);
        }

        [Test]
        public void Approach_IsFrameRateIndependent()
        {
            // The whole reason for the exponential form: one big step and many
            // small steps covering the same time must land in the same place.
            float coarse = ShipMotion.Approach(0f, 10f, 6f, 0.5f);

            float fine = 0f;
            for (int i = 0; i < 50; i++)
            {
                fine = ShipMotion.Approach(fine, 10f, 6f, 0.01f);
            }

            Assert.AreEqual(coarse, fine, 0.001f);
        }

        [Test]
        public void Approach_WithZeroResponse_SnapsInstantly()
        {
            Assert.AreEqual(10f, ShipMotion.Approach(0f, 10f, 0f, 0.016f), Tolerance);
        }

        [Test]
        public void Approach_WithZeroDeltaTime_DoesNotMove()
        {
            Assert.AreEqual(3f, ShipMotion.Approach(3f, 10f, 5f, 0f), Tolerance);
        }

        [Test]
        public void Approach_HandlesADescendingTarget()
        {
            float value = ShipMotion.Approach(10f, -10f, 5f, 0.1f);

            Assert.Less(value, 10f);
            Assert.Greater(value, -10f);
        }

        [Test]
        public void ApproachAngle_TakesTheShortWayAround()
        {
            // 350 -> 10 is +20 degrees, not -340.
            float value = ShipMotion.ApproachAngle(350f, 10f, 100f, 0.1f);

            Assert.AreEqual(360f, value, 0.001f);
        }

        [Test]
        public void ApproachAngle_MovesAtTheGivenRate()
        {
            float value = ShipMotion.ApproachAngle(0f, 180f, 540f, 0.1f);

            Assert.AreEqual(54f, value, 0.001f);
        }

        [Test]
        public void ApproachAngle_StopsAtTheTarget()
        {
            float value = ShipMotion.ApproachAngle(0f, 180f, 540f, 10f);

            Assert.AreEqual(180f, Mathf.Abs(value), 0.001f);
        }

        [Test]
        public void ApproachAngle_WithZeroRate_SnapsInstantly()
        {
            Assert.AreEqual(180f, ShipMotion.ApproachAngle(0f, 180f, 0f, 0.016f), Tolerance);
        }

        [Test]
        public void FlipSweepTakesTheExpectedTime()
        {
            // 180 degrees at 540 deg/s should land in a third of a second.
            float value = 0f;
            float elapsed = 0f;
            const float step = 1f / 60f;

            while (elapsed < 1f && !Mathf.Approximately(Mathf.Abs(value), 180f))
            {
                value = ShipMotion.ApproachAngle(value, 180f, 540f, step);
                elapsed += step;
            }

            Assert.That(elapsed, Is.EqualTo(0.333f).Within(0.02f));
        }
    }
}
