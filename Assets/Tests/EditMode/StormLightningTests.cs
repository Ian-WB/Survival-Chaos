using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Lightning inside the storm's wall, built 7 October 2026 on the
    /// environment roadmap's item 40. The glow is drawn by the project's copy of
    /// HDRP's cloud tracer, and StormLightning in the Game scene says where
    /// and how bright.
    ///
    /// Three things would fail without a sound. The scene's object losing
    /// its tracer, which leaves the storm dark. An HDRP re-copy of the
    /// include dropping the glow, the same. And the numbers drifting to a
    /// flash that strobes, or one that sits behind the ships.
    /// </summary>
    public class StormLightningTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string TracerPath = "Assets/Art/Shaders/VolumetricCloudsTrace.compute";
        private const string IncludePath = "Assets/Art/Shaders/VolumetricCloudsUtilities.hlsl";

        private static string Lightning(SavedScene scene)
        {
            string gameObject = scene.GameObjectNamed("Storm Lightning");
            Assert.That(gameObject, Is.Not.Null, "there is no Storm Lightning in the Game scene");

            string lightning = scene.ScriptWith(gameObject, "strokeFade");
            Assert.That(lightning, Is.Not.Null, "the Storm Lightning object has no StormLightning on it");
            return lightning;
        }

        [Test]
        public void AStroke_ArrivesQuickly_AndDiesAway()
        {
            float[] at = { 0f, 0.3f };
            float[] heights = { 1f, 0.5f };

            Assert.AreEqual(0f, StormLightning.Glow(-0.01f, at, heights, 2, 0.05f, 0.07f));
            Assert.AreEqual(0f, StormLightning.Glow(0f, at, heights, 2, 0.05f, 0.07f), 1e-4f);
            Assert.AreEqual(0.5f, StormLightning.Glow(0.025f, at, heights, 2, 0.05f, 0.07f), 1e-4f);
            Assert.AreEqual(1f, StormLightning.Glow(0.05f, at, heights, 2, 0.05f, 0.07f), 1e-4f);
            Assert.That(StormLightning.Glow(0.12f, at, heights, 2, 0.05f, 0.07f), Is.EqualTo(1f / Mathf.Exp(1f)).Within(1e-4f));
            Assert.That(StormLightning.Glow(0.29f, at, heights, 2, 0.05f, 0.07f), Is.LessThan(0.1f), "the first stroke is still burning when the second arrives");

            // With no rise it is there at once, which is what shows the tracer's squares.
            Assert.AreEqual(1f, StormLightning.Glow(0f, at, heights, 2, 0f, 0.07f), 1e-4f);
        }

        [Test]
        public void TheBrightestStrokeStillBurning_IsWhatShows()
        {
            float[] at = { 0f, 0.3f };
            float[] heights = { 1f, 0.5f };

            Assert.AreEqual(0.5f, StormLightning.Glow(0.35f, at, heights, 2, 0.05f, 0.07f), 1e-4f);

            // A weak stroke on the heels of a strong one does not dim it.
            float[] close = { 0f, 0.06f };
            Assert.That(StormLightning.Glow(0.07f, close, heights, 2, 0.05f, 0.07f), Is.GreaterThan(0.7f));
        }

        [Test]
        public void AFlash_IsOverWhenItsLastStrokeIsGone()
        {
            float[] at = { 0f, 0.15f, 0.33f };
            float[] heights = { 0.6f, 1f, 0.4f };

            float length = StormLightning.Length(at, 3, 0.05f, 0.07f);
            Assert.That(length, Is.GreaterThan(0.38f));
            Assert.That(StormLightning.Glow(length, at, heights, 3, 0.05f, 0.07f), Is.LessThan(0.011f));
        }

        [Test]
        public void DeeperUnderTheFace_IsFurtherOutAndLower()
        {
            Vector2 foot = new Vector2(1500f, 160f);
            Vector2 top = new Vector2(1800f, 420f);

            Assert.AreEqual(foot, StormLightning.Place(foot, top, 0f, 0f));
            Assert.AreEqual(top, StormLightning.Place(foot, top, 1f, 0f));

            Vector2 deep = StormLightning.Place(foot, top, 0.5f, 100f);
            Vector2 shallow = StormLightning.Place(foot, top, 0.5f, 0f);
            Assert.That(deep.x, Is.GreaterThan(shallow.x));
            Assert.That(deep.y, Is.LessThan(shallow.y));
        }

        [Test]
        public void TheGameScene_HasTheLightning_WiredToTheProjectsTracer()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string lightning = Lightning(scene);

            Assert.That(scene.Reference(lightning, "tracer"), Is.Not.EqualTo("0"), "Storm Lightning's tracer is not assigned");

            string guid = AssetDatabase.AssetPathToGUID(TracerPath);
            Assert.That(guid, Is.Not.Empty, TracerPath + " is missing");
            StringAssert.Contains("tracer: {fileID: 7200000, guid: " + guid, File.ReadAllText(ScenePath),
                "Storm Lightning sets its flash on some other shader than the one the clouds are traced by");
        }

        /// <summary>
        /// The usual limit for flashing light is three flashes in a second.
        /// A flash has at most <see cref="StormLightning.MostStrokes"/>
        /// strokes; the gap to the next flash has to be longer than that
        /// second, and the strokes far enough apart to be strokes.
        /// </summary>
        [Test]
        public void TheScreen_NeverFlashesMoreThanThreeTimesInASecond()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string lightning = Lightning(scene);

            Assert.That(StormLightning.MostStrokes, Is.LessThanOrEqualTo(3));
            Assert.That(scene.Vector(lightning, "gap").x, Is.GreaterThanOrEqualTo(2f), "one flash follows another too soon");
            Assert.That(scene.Vector(lightning, "strokeGap").x, Is.GreaterThanOrEqualTo(0.08f));
            Assert.That(scene.Float(lightning, "strokeRise"), Is.GreaterThanOrEqualTo(0.03f),
                "a stroke that arrives in a frame shows the cloud tracer's squares");
        }

        /// <summary>
        /// A flash under the horizon would be behind the ships and their
        /// fire, and one too far round is outside the frame, where it lights
        /// nothing the player sees.
        /// </summary>
        [Test]
        public void EveryFlash_IsOverTheHorizon_AndNearWhereTheCameraLooks()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string lightning = Lightning(scene);

            Vector2 foot = scene.Vector(lightning, "faceFoot");
            Vector2 top = scene.Vector(lightning, "faceTop");
            Vector2 depth = scene.Vector(lightning, "depth");

            Vector2 lowest = StormLightning.Place(foot, top, 0f, depth.y);
            Assert.That(lowest.y, Is.GreaterThan(50f), "the lowest flash is at the horizon or under it");
            Assert.That(lowest.x, Is.GreaterThan(1300f), "the nearest flash is inside the eye, in clear air");
            Assert.That(scene.Float(lightning, "spread"), Is.InRange(30f, 90f));
        }

        /// <summary>The three places the glow lives in the include; a re-copy from the package loses all three.</summary>
        [Test]
        public void TheTracer_DrawsTheGlow()
        {
            string text = File.ReadAllText(IncludePath);

            StringAssert.Contains("float4 _StormFlash;", text);
            StringAssert.Contains("float4 _StormFlashLight;", text);
            StringAssert.Contains("volumetricRay.flash      += StormFlashReach(currentPositionPS)", text, "the glow is not gathered along the ray");
            StringAssert.Contains("volumetricRay.scattering += _StormFlashLight.rgb * volumetricRay.flash;", text, "the glow is gathered and never added to the cloud's light");
            StringAssert.Contains("volumetricRay.flash = 0.0;", text, "the glow starts from whatever was in memory");
        }
    }
}
