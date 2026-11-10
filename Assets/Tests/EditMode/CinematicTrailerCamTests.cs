using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    public class CinematicTrailerCamTests
    {
        [Test]
        public void TrailerCam_IsInactiveByDefault()
        {
            Assert.IsFalse(CinematicTrailerCam.IsActive);
            Assert.AreEqual(CinematicTrailerCam.CamMode.StandardGameplay, CinematicTrailerCam.CurrentMode);
        }

        [Test]
        public void TrailerCam_Modes_HaveExpectedValues()
        {
            Assert.AreEqual(0, (int)CinematicTrailerCam.CamMode.StandardGameplay);
            Assert.AreEqual(1, (int)CinematicTrailerCam.CamMode.LowAngleChase);
            Assert.AreEqual(2, (int)CinematicTrailerCam.CamMode.SideProfileOrbit);
            Assert.AreEqual(3, (int)CinematicTrailerCam.CamMode.CalderaSweeper);
            Assert.AreEqual(4, (int)CinematicTrailerCam.CamMode.ForwardTarget);
        }

        [Test]
        public void TrailerCam_SetMode_SwitchesActiveState()
        {
            var go = new GameObject("TestTrailerCam");
            try
            {
                var camComp = go.AddComponent<CinematicTrailerCam>();
                camComp.SetMode(CinematicTrailerCam.CamMode.LowAngleChase);
                Assert.IsTrue(CinematicTrailerCam.IsActive);
                Assert.AreEqual(CinematicTrailerCam.CamMode.LowAngleChase, CinematicTrailerCam.CurrentMode);

                camComp.SetMode(CinematicTrailerCam.CamMode.StandardGameplay);
                Assert.IsFalse(CinematicTrailerCam.IsActive);
                Assert.AreEqual(CinematicTrailerCam.CamMode.StandardGameplay, CinematicTrailerCam.CurrentMode);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TrailerCam_ToggleHud_DoesNotThrowWithoutCanvas()
        {
            var go = new GameObject("TestTrailerCam");
            try
            {
                var camComp = go.AddComponent<CinematicTrailerCam>();
                Assert.DoesNotThrow(() => camComp.ToggleHud());
                Assert.DoesNotThrow(() => camComp.ToggleHud());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
