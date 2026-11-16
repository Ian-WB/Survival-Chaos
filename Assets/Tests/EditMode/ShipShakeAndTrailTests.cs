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
        public void AShipThatCanBackUp_DrawsItsTrailOnlyForTheSpeedAlongItsNose()
        {
            Vector3 nose = Vector3.right;

            Assert.AreEqual(6f, ShipTrail.Speed(new Vector3(6f, 0f, 0f), nose), 0.0001f, "nose-first");
            Assert.AreEqual(6f, ShipTrail.Speed(new Vector3(6f, 8f, 0f), nose), 0.0001f, "climbing as well");
            Assert.AreEqual(0f, ShipTrail.Speed(new Vector3(0f, 8f, 0f), nose), 0.0001f, "only climbing");
            Assert.AreEqual(0f, ShipTrail.Speed(new Vector3(-6f, 0f, 0f), nose), 0.0001f, "tail-first");
            Assert.AreEqual(10f, ShipTrail.Speed(new Vector3(6f, 8f, 0f), Vector3.zero), 0.0001f, "a ship that turns to fly");
        }
    }
}
