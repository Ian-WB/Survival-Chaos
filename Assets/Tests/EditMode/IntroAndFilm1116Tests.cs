using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// What 16 November 2026 changed in the openings: the title film powers
    /// its logo on once, in view, and loops past it; the arena intro opens on
    /// the ship and the moon and follows the drop; and the trailer winds the
    /// wave clock on so the storm is up when the boss comes.
    /// </summary>
    public class IntroAndFilm1116Tests
    {
        private const string MenuScene = "Assets/Scenes/Menu.unity";
        private const string GameScene = "Assets/Scenes/Game.unity";
        private const string TitleFilmClip = "Assets/UI/Video/TitleScreen.mp4";

        [TearDown]
        public void TearDown()
        {
            TitleFilm.HeldBack = false;
        }

        // The title film

        [Test]
        public void TheTitleFilm_JumpsBackAFewFramesBeforeItsEnd()
        {
            // A seek on the last two frames froze the film; three before lands.
            Assert.IsTrue(TitleFilm.LoopDue(374, 373, 377, 81), "three before the end");
            Assert.IsTrue(TitleFilm.LoopDue(377, 373, 377, 81), "the last, if a slow frame skipped the others");
            Assert.IsFalse(TitleFilm.LoopDue(373, 372, 377, 81), "four before the end");
            Assert.IsFalse(TitleFilm.LoopDue(200, 199, 377, 81), "mid film");
            Assert.IsFalse(TitleFilm.LoopDue(80, 80, 377, 81), "just after its own jump");
        }

        [Test]
        public void AJump_HasLandedOnlyOnceTheFilmIsBackBeforeTheEnd()
        {
            // Just after the seek completes, frame still reads the one it left.
            Assert.IsFalse(TitleFilm.Landed(374, 377), "still on the frame it jumped from");
            Assert.IsTrue(TitleFilm.Landed(80, 377));
            Assert.IsFalse(TitleFilm.Landed(-1, 377), "no frame yet");
        }

        [Test]
        public void TheTitleFilm_LandsOnTheFrameThatFollowsInTheLoop()
        {
            // 374 is blended towards 77, so 78 follows it; the seek takes two frames.
            Assert.AreEqual(78 + TitleFilm.SeekLead, TitleFilm.LoopTarget(374, 377, 81));
            Assert.AreEqual(81 + TitleFilm.SeekLead, TitleFilm.LoopTarget(377, 377, 81));
            Assert.AreEqual(81, TitleFilm.LoopTarget(3, 377, 81), "from the top, after a missed jump");
        }

        [Test]
        public void TheTitleFilm_PlaysItsPowerOnOnceFromTheTop()
        {
            Assert.IsFalse(TitleFilm.LoopDue(0, -1, 377, 81), "the first frame, first time");
            Assert.IsFalse(TitleFilm.LoopDue(10, 9, 377, 81), "the power-on, first time");
        }

        [Test]
        public void TheTitleFilm_RecoversWhenItsOwnLoopTookItToTheStart()
        {
            Assert.IsTrue(TitleFilm.LoopDue(2, 375, 377, 81));
        }

        [Test]
        public void ATooShortClip_LoopsWhole()
        {
            Assert.IsFalse(TitleFilm.LoopDue(50, 49, 50, 81));
            Assert.IsFalse(TitleFilm.LoopDue(50, 49, 50, 0));
        }

        [Test]
        public void TheMenusVideoPlayer_LoopsTheTitleFilmPastItsPowerOn()
        {
            SavedScene scene = SavedScene.Load(MenuScene);
            string player = scene.GameObjectNamed("Video Player");
            Assert.IsNotNull(player, "no Video Player in " + MenuScene);
            Assert.IsNotNull(scene.Component(player, "VideoPlayer"), "the Video Player has no VideoPlayer");

            string film = scene.ScriptWith(player, "loopFrame");
            Assert.IsNotNull(film, "the Video Player has no TitleFilm");

            int loopFrame = Mathf.RoundToInt(scene.Float(film, "loopFrame"));
            VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(TitleFilmClip);
            Assert.IsNotNull(clip, "no " + TitleFilmClip);

            // The cut of 16 November: 378 frames, the last six blended into 75-80.
            Assert.AreEqual(81, loopFrame);
            Assert.Greater((long)clip.frameCount, loopFrame + 2L, "the loop frame is not inside the clip");
        }

        [Test]
        public void TheAttractPrompt_WaitsForTheLogo()
        {
            Assert.AreEqual(0f, TitleSplashDirector.PromptLevel(0f));
            Assert.AreEqual(0f, TitleSplashDirector.PromptLevel(TitleSplashDirector.PromptDelay));
            Assert.AreEqual(1f, TitleSplashDirector.PromptLevel(TitleSplashDirector.PromptDelay + 0.4f), 1e-5f);
            Assert.AreEqual(1f, TitleSplashDirector.PromptLevel(40f));
        }

        // The arena intro

        [Test]
        public void TheIntro_OpensLookingInwardAndUp()
        {
            Vector3 view = ArenaIntroDirector.OpeningView(new Vector3(0f, 7f, -19.72f));
            Assert.AreEqual(1f, view.magnitude, 1e-4f);
            Assert.AreEqual(0f, view.x, 1e-4f);
            Assert.Greater(view.z, 0f, "not inward");
            Assert.AreEqual(ArenaIntroDirector.OpeningTilt, Mathf.Asin(view.y) * Mathf.Rad2Deg, 0.01f);
        }

        [Test]
        public void TheIntro_OpensBehindAndJustBelowTheHangingShip()
        {
            Vector3 hang = new Vector3(0f, 19f, -19.72f);
            Vector3 view = ArenaIntroDirector.OpeningView(hang);
            Vector3 camera = ArenaIntroDirector.OpeningCamera(hang, view);

            Assert.Less(camera.z, hang.z, "not behind the ship");
            Assert.Less(camera.y, hang.y, "not below the ship");

            // The ship sits above the middle of the frame, where the moon is,
            // and well inside the opening lens.
            Vector3 toShip = (hang - camera).normalized;
            Assert.Greater(toShip.y, view.y, "the ship is below the middle of the frame");
            Assert.Less(Vector3.Angle(view, toShip), ArenaIntroDirector.OpeningFov * 0.25f, "the ship is near the edge of the frame");
        }

        [Test]
        public void TheIntrosOpeningShot_HasTheMoonBehindTheShip()
        {
            SavedScene scene = SavedScene.Load(GameScene);

            string moon = scene.GameObjectNamed("Directional Light");
            Assert.IsNotNull(moon, "no Directional Light in " + GameScene);
            Vector3 toMoon = -(scene.WorldRotation(scene.Component(moon, "Transform")) * Vector3.forward);

            string ship = scene.GameObjectNamed("Player");
            Assert.IsNotNull(ship, "no Player in " + GameScene);
            string shipTransform = scene.Component(ship, "Transform");
            Assert.AreEqual("0", scene.Reference(shipTransform, "m_Father"), "the Player is no longer at the root");
            Vector3 start = scene.Vector(shipTransform, "m_LocalPosition");

            // The moon is 24 degrees across: within 4 of the shot's line it is
            // in the middle of the frame, behind the ship.
            float off = Vector3.Angle(ArenaIntroDirector.OpeningView(start), toMoon);
            Assert.Less(off, 4f, "The moonlight or the ship's start has moved: the intro's opening misses the moon by " + off.ToString("F1") + " degrees.");
        }

        [Test]
        public void TheShip_FallsFromAHoverAndBrakesIntoTheLane()
        {
            Assert.AreEqual(0f, ArenaIntroDirector.DropShare(0f));
            Assert.AreEqual(1f, ArenaIntroDirector.DropShare(1f), 1e-5f);
            Assert.AreEqual(0.5f, ArenaIntroDirector.DropShare(0.5f), 1e-5f);

            // Slow off the hover, so the camera that follows it does not whip down.
            Assert.Less(ArenaIntroDirector.DropShare(0.1f), 0.01f);
            // And slow into the lane.
            Assert.Greater(ArenaIntroDirector.DropShare(0.9f), 0.99f);
        }

        [Test]
        public void TheIntrosCamera_LagsTheDropAndEndsOnTheStation()
        {
            Assert.AreEqual(0f, ArenaIntroDirector.CameraTravel(0f));
            Assert.AreEqual(0f, ArenaIntroDirector.CameraTravel(0.5f));
            Assert.AreEqual(1f, ArenaIntroDirector.CameraTravel(3.5f));
            Assert.Less(ArenaIntroDirector.CameraTravel(2.15f), 0.7f, "the camera is already home as the ship lands");

            Assert.AreEqual(0f, ArenaIntroDirector.LookHandBack(2.0f));
            Assert.AreEqual(1f, ArenaIntroDirector.LookHandBack(3.5f));
            Assert.AreEqual(1f, ArenaIntroDirector.LensOpen(3.5f));
        }

        [Test]
        public void TheCamera_RidesDownWithTheShipAndALittleBehindIt()
        {
            Vector3 opening = new Vector3(0f, 17f, -25f);
            Vector3 hang = new Vector3(0f, 19f, -19.72f);
            Vector3 landed = new Vector3(0f, 7f, -19.72f);

            Assert.AreEqual(opening, ArenaIntroDirector.RideDown(opening, hang, hang), "before the drop");

            Vector3 ride = ArenaIntroDirector.RideDown(opening, hang, landed);
            float cameraFell = opening.y - ride.y;
            Assert.Greater(cameraFell, 0f, "the camera stayed up");
            Assert.Less(cameraFell, hang.y - landed.y, "the camera fell as far as the ship, which then never moves in the frame");
            Assert.AreEqual(opening.z, ride.z, "riding down moves the camera only down");
        }

        // The trailer

        [Test]
        public void TheTrailer_ReachesTheBossShotTwentyThreeGameSecondsIn()
        {
            // 26 s of trailer, 4 of them at a quarter speed.
            Assert.AreEqual(23f, CinematicTrailerDirector.GameSecondsToBossShot, 1e-4f);
        }

        [Test]
        public void TheTrailer_WindsTheClockToJustShortOfTheBoss()
        {
            float lead = CinematicTrailerDirector.GameSecondsToBossShot + CinematicTrailerDirector.BossLead;
            Assert.AreEqual(400f - 24f - 0.3f, CinematicTrailerDirector.ClockJump(0.3f, 400f, lead), 1e-3f);
        }

        [Test]
        public void TheTrailer_NeverWindsTheClockBack()
        {
            Assert.AreEqual(0f, CinematicTrailerDirector.ClockJump(390f, 400f, 24f));
            Assert.AreEqual(0f, CinematicTrailerDirector.ClockJump(10f, 0f, 24f), "a wave with no boss");
        }
    }
}
