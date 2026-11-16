using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    public class ShipShakeAndTrailTests
    {
        [Test]
        public void AShake_IsTheSquareOfItsTrauma_AndNeverMoreThanTheMost()
        {
            Assert.AreEqual(0f, ScreenShake.Degrees(0f), 0.0001f);
            Assert.AreEqual(ScreenShake.MaxDegrees * 0.25f, ScreenShake.Degrees(0.5f), 0.0001f);
            Assert.AreEqual(ScreenShake.MaxDegrees, ScreenShake.Degrees(1f), 0.0001f);
            Assert.AreEqual(ScreenShake.MaxDegrees, ScreenShake.Degrees(7f), 0.0001f);
        }

        [Test]
        public void TheShake_StaysSmall_AndIsSoonOver()
        {
            // Ian left it out of the game for two months; it came back on
            // the understanding that it is a knock and not an earthquake.
            Assert.LessOrEqual(ScreenShake.MaxDegrees, 1f);
            Assert.LessOrEqual(1f / ScreenShake.Drain, 0.6f, "a full shake outlasts half a second");
        }

        [Test]
        public void ATrail_IsNothingBelowItsSpeed_GrowsWithIt_AndStopsAtItsLongest()
        {
            Assert.AreEqual(0f, ShipTrail.Length(1.5f, 2f, 0.12f, 1.4f), 0.0001f);
            Assert.AreEqual(0.432f, ShipTrail.Length(5.6f, 2f, 0.12f, 1.4f), 0.0001f);
            Assert.AreEqual(1.4f, ShipTrail.Length(28f, 2f, 0.12f, 1.4f), 0.0001f);
        }

        [Test]
        public void TheTrail_StartsAtTheHullsEdge_NotItsMiddle()
        {
            Vector2 aft = ShipTrail.Edge(new Vector2(-1f, 0f), 0.25f, 0.12f);
            Vector2 fore = ShipTrail.Edge(new Vector2(1f, 0f), 0.25f, 0.12f);
            Vector2 below = ShipTrail.Edge(new Vector2(0f, -1f), 0.25f, 0.12f);

            Assert.AreEqual(-0.25f, aft.x, 0.0001f);
            Assert.AreEqual(0.25f, fore.x, 0.0001f);
            Assert.AreEqual(-0.12f, below.y, 0.0001f);
            Assert.AreEqual(Vector2.zero, ShipTrail.Edge(Vector2.zero, 0.25f, 0.12f));
        }

        [Test]
        public void TheTrail_IsDimmerOffTheNose()
        {
            Assert.AreEqual(1f, ShipTrail.Dim(-1f), 0.0001f);
            Assert.AreEqual(1f, ShipTrail.Dim(0f), 0.0001f);
            Assert.AreEqual(ShipTrail.TailFirstBrightness, ShipTrail.Dim(1f), 0.0001f);
            Assert.Less(ShipTrail.TailFirstBrightness, 0.6f);
        }
    }
}
