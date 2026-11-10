using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// What the arena intro, the trailer and the title splash give back when
    /// they are done: the camera's height clamp, the player's camera preset, and
    /// the attract prompt only where it covers nothing (10 November 2026).
    /// </summary>
    public class CinematicHandbackTests
    {
        [TearDown]
        public void TearDown()
        {
            ArenaIntroDirector.Enabled = true;
            TitleSplashDirector.Enabled = true;
        }

        // B - the camera's clamp lets go while a cinematic holds it, and only the camera's.

        [Test]
        public void Clamp_ReleasedOnTheCameraWhileHeld()
        {
            Assert.IsFalse(ApplyBounds.Clamps(isCamera: true, cameraHeld: true));
        }

        [Test]
        public void Clamp_RestoredOnTheCameraOnceLetGo()
        {
            Assert.IsTrue(ApplyBounds.Clamps(isCamera: true, cameraHeld: false));
        }

        [Test]
        public void Clamp_NeverReleasedOnTheShip()
        {
            Assert.IsTrue(ApplyBounds.Clamps(isCamera: false, cameraHeld: true));
            Assert.IsTrue(ApplyBounds.Clamps(isCamera: false, cameraHeld: false));
        }

        [Test]
        public void Clamp_NothingHoldsTheCameraOutsidePlay()
        {
            Assert.IsFalse(ApplyBounds.CameraHeld);
        }

        // C - the preset the player was on comes back, not the default.

        [Test]
        public void Preset_TheOneTakenIsHandedBack()
        {
            for (int i = 0; i < CameraFraming.Presets.Length; i++)
            {
                Assert.AreEqual(i, CameraFraming.RestoreIndex(i));
            }
        }

        [Test]
        public void Preset_ANonPresetFallsBackToTheDefault()
        {
            Assert.AreEqual(CameraFraming.DefaultIndex, CameraFraming.RestoreIndex(-1));
            Assert.AreEqual(CameraFraming.DefaultIndex, CameraFraming.RestoreIndex(CameraFraming.Presets.Length));
        }

        [Test]
        public void Preset_IntroLandsLevelWithTheShipOnAClimbingPreset()
        {
            CameraPreset climbing = CameraFraming.Presets[0];
            Assume.That(climbing.HoldsBandMiddle, Is.False);

            Assert.AreEqual(5f, CameraFraming.StationHeight(climbing, 5f, 3.42f, 12.65f), 1e-5f);
        }

        [Test]
        public void Preset_IntroLandsOnTheBandMiddleOnAHoldingPreset()
        {
            CameraPreset holding = CameraFraming.Default;
            Assume.That(holding.HoldsBandMiddle, Is.True);

            Assert.AreEqual(8.035f, CameraFraming.StationHeight(holding, 5f, 3.42f, 12.65f), 1e-4f);
        }

        [Test]
        public void Preset_IntroLensIsThePresetsOwn()
        {
            CameraPreset classic = CameraFraming.Presets[0];
            Assert.AreEqual(CameraFraming.ClassicFieldOfView,
                CameraFraming.Lens(classic, classic.Distance, CameraFraming.BandHeight, 0f), 1e-4f);
        }

        // D - idle counts only on the title panel, and moving anything is activity.

        [Test]
        public void Idle_CountsOnlyWithTheTitlePanelUpAndShowing()
        {
            Assert.IsTrue(TitleSplashDirector.IdleCounts(titleActive: true, titleAlpha: 1f));
            Assert.IsFalse(TitleSplashDirector.IdleCounts(titleActive: false, titleAlpha: 1f));
            Assert.IsFalse(TitleSplashDirector.IdleCounts(titleActive: true, titleAlpha: 0f));
        }

        [Test]
        public void Idle_ReachesTheAttractPromptOnTheTitlePanel()
        {
            float idle = 0f;
            for (int i = 0; i < 41; i++)
            {
                idle = TitleSplashDirector.NextIdle(idle, 1f, counts: true, activity: false);
            }

            Assert.IsTrue(TitleSplashDirector.AttractDue(idle));
        }

        [Test]
        public void Idle_NeverReachesItOnAnotherScreen()
        {
            float idle = 0f;
            for (int i = 0; i < 100; i++)
            {
                idle = TitleSplashDirector.NextIdle(idle, 1f, counts: false, activity: false);
            }

            Assert.AreEqual(0f, idle);
            Assert.IsFalse(TitleSplashDirector.AttractDue(idle));
        }

        [Test]
        public void Idle_StartsAgainAfterLeavingTheTitlePanel()
        {
            float idle = 0f;
            for (int i = 0; i < 39; i++)
            {
                idle = TitleSplashDirector.NextIdle(idle, 1f, counts: true, activity: false);
            }

            idle = TitleSplashDirector.NextIdle(idle, 1f, counts: false, activity: false);
            idle = TitleSplashDirector.NextIdle(idle, 1f, counts: true, activity: false);

            Assert.IsFalse(TitleSplashDirector.AttractDue(idle));
        }

        [Test]
        public void Idle_ActivityStartsItAgain()
        {
            float idle = TitleSplashDirector.NextIdle(39f, 1f, counts: true, activity: true);
            Assert.AreEqual(0f, idle);
        }

        [Test]
        public void Moved_StillInputIsNotActivity()
        {
            Assert.IsFalse(TitleSplashDirector.Moved(Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero));
        }

        [Test]
        public void Moved_StickDriftIsNotActivity()
        {
            Vector2 drift = new Vector2(0.1f, 0.05f);
            Assert.IsFalse(TitleSplashDirector.Moved(Vector2.zero, Vector2.zero, drift, drift, Vector2.zero));
        }

        [Test]
        public void Moved_MouseWheelSticksAndDpadAreActivity()
        {
            Vector2 z = Vector2.zero;
            Assert.IsTrue(TitleSplashDirector.Moved(new Vector2(3f, 0f), z, z, z, z), "mouse");
            Assert.IsTrue(TitleSplashDirector.Moved(z, new Vector2(0f, 120f), z, z, z), "wheel");
            Assert.IsTrue(TitleSplashDirector.Moved(z, z, new Vector2(0.6f, 0f), z, z), "left stick");
            Assert.IsTrue(TitleSplashDirector.Moved(z, z, z, new Vector2(0f, -0.6f), z), "right stick");
            Assert.IsTrue(TitleSplashDirector.Moved(z, z, z, z, Vector2.up), "d-pad");
        }

        // F4 - a switch turned off in one play is on again in the next.

        [Test]
        public void EnterPlay_TurnsTheIntroBackOn()
        {
            ArenaIntroDirector.Enabled = false;
            InvokeReset(typeof(ArenaIntroDirector));
            Assert.IsTrue(ArenaIntroDirector.Enabled);
        }

        [Test]
        public void EnterPlay_TurnsTheSplashBackOn()
        {
            TitleSplashDirector.Enabled = false;
            InvokeReset(typeof(TitleSplashDirector));
            Assert.IsTrue(TitleSplashDirector.Enabled);
        }

        private static void InvokeReset(System.Type type)
        {
            MethodInfo reset = type.GetMethod("ResetOnEnterPlayMode", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(reset, type.Name + " has no ResetOnEnterPlayMode");
            reset.Invoke(null, null);
        }
    }
}
