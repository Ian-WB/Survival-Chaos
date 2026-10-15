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
        /// second, and the strokes far enough apart to be strokes. This
        /// holds the saved numbers of the one component in the scene. It
        /// does not watch the screen, and Strike() called from outside goes
        /// round it.
        /// </summary>
        [Test]
        public void TheSavedTiming_AllowsNoMoreThanThreeStrokesInASecond()
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
        /// nothing the player sees. This holds the saved numbers flashes are
        /// placed by: over the horizon, out in the wall, and leaning towards
        /// the camera. It does not show that a flash is in the frame, or
        /// that cloud stands where it lands.
        /// </summary>
        [Test]
        public void FlashesArePlaced_OverTheHorizon_AndTowardsTheCamera()
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

        [Test]
        public void AFork_LeavesTheBoltBetweenItsEnds_AndHangsDown()
        {
            Vector3[] path = new Vector3[StormLightning.MostCorners];
            AshLightning.Bend(new Vector3(-300f, 300f, 1700f), new Vector3(300f, 280f, 1700f), 0.09f, 11, path, 14);

            StormLightning.Fork(path, 14, 0.4f, 80f, 1f, out Vector3 start, out Vector3 end);
            Assert.AreEqual(path[5], start);
            Assert.That(end.y, Is.LessThan(start.y - 40f), "a fork that does not hang down is a second bolt");
            Assert.That(Vector3.Distance(start, end), Is.EqualTo(80f).Within(0.01f));

            StormLightning.Fork(path, 14, 0.4f, 80f, -1f, out _, out Vector3 other);
            Assert.That(Mathf.Sign(end.x - start.x), Is.Not.EqualTo(Mathf.Sign(other.x - start.x)), "the side does not choose the way along the bolt");

            // Asked for the very start or end, it still leaves from a corner between them.
            StormLightning.Fork(path, 14, 0f, 80f, 1f, out Vector3 first, out _);
            StormLightning.Fork(path, 14, 1f, 80f, 1f, out Vector3 last, out _);
            Assert.AreEqual(path[1], first);
            Assert.AreEqual(path[12], last);
        }

        [Test]
        public void ABolt_StandsWhereItIsSent_ItsFootLeaningAsAsked()
        {
            StormLightning.Ends(Vector3.forward, 1700f, 250f, -100f, new Vector2(60f, -40f), out Vector3 from, out Vector3 to);

            Assert.AreEqual(new Vector3(0f, 250f, 1700f), from);
            Assert.AreEqual(-100f, to.y, 1e-3f);
            Assert.AreEqual(1760f, to.z, 1e-3f, "the foot does not lean out along where it strikes");
            Assert.AreEqual(40f, Mathf.Abs(to.x), 1e-3f, "the foot does not lean across");

            StormLightning.Ends(Vector3.right, 1700f, 250f, -100f, Vector2.zero, out from, out to);
            Assert.AreEqual(new Vector3(1700f, 250f, 0f), from);
            Assert.AreEqual(new Vector3(1700f, -100f, 0f), to, "with no lean the foot is not under the top");
        }

        [Test]
        public void TheBolt_IsWired_AndHiddenBetweenFlashes()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string lightning = Lightning(scene);

            foreach (string name in new[] { "Storm Bolt", "Storm Fork" })
            {
                string line = scene.Component(scene.GameObjectNamed(name), "LineRenderer");
                Assert.That(line, Is.Not.Null, "there is no " + name + " line");
                Assert.AreEqual(0f, scene.Float(line, "m_Enabled"), "saved switched on, " + name + " hangs in the wall for good");
                Assert.AreEqual(1f, scene.Float(line, "m_UseWorldSpace"), "a bolt's corners are places in the world");
                Assert.AreEqual(0f, scene.Float(line, "m_CastShadows"));
            }

            foreach (string field in new[] { "bolt", "fork" })
            {
                Assert.That(scene.Reference(lightning, field), Is.Not.EqualTo("0"), "Storm Lightning's " + field + " is not assigned");
            }
        }

        [Test]
        public void TheCloudIsLitAlong_EvenlySpacedCorners_WithBothEnds()
        {
            Vector3[] path = new Vector3[StormLightning.MostCorners];
            for (int i = 0; i < 18; i++)
            {
                path[i] = new Vector3(i, 100f - i, 0f);
            }

            Vector4[] into = new Vector4[StormLightning.CloudCorners];
            Assert.AreEqual(12, StormLightning.Thin(path, 18, into));
            Assert.AreEqual((Vector4)path[0], into[0]);
            Assert.AreEqual((Vector4)path[17], into[11], "the bolt's low end is not lit");
            for (int i = 1; i < 12; i++)
            {
                Assert.That(into[i].x, Is.GreaterThan(into[i - 1].x), "the corners are out of order");
            }

            // Fewer corners than the tracer takes are all kept.
            Assert.AreEqual(5, StormLightning.Thin(path, 5, into));
            Assert.AreEqual((Vector4)path[4], into[4]);
        }

        /// <summary>
        /// Ian asked for the bolts to look inside the clouds, and each try
        /// placed against the wall's face sat at the cloud's edge. So a bolt
        /// stands well behind where the wall starts (about 1,050 out, solid
        /// by about 1,800), under the wall's tops, its foot in the cloud the
        /// island floats over. The camera has to draw that far: until
        /// 15 October its far plane was 1,000 metres, so a bolt there would
        /// not have been drawn at all.
        /// </summary>
        [Test]
        public void BoltsStandInsideTheWall_WhereTheCameraDraws()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string lightning = Lightning(scene);

            Vector2 away = scene.Vector(lightning, "boltOut");
            Vector2 boltTop = scene.Vector(lightning, "boltTop");
            Vector2 foot = scene.Vector(lightning, "boltFoot");
            float lean = scene.Float(lightning, "boltLean");

            Assert.That(away.x - lean, Is.GreaterThan(1300f), "a bolt stands at the wall's inner edge, where the cloud is thin, or in the eye");
            Assert.That(boltTop.y, Is.LessThanOrEqualTo(400f), "a bolt's top is over the wall, in the open sky");
            Assert.That(boltTop.x, Is.GreaterThan(foot.y), "a bolt's top is under its foot");
            Assert.That(foot.x, Is.GreaterThanOrEqualTo(-400f), "a bolt's foot is so far under the horizon it is behind the island");
            Assert.That(scene.Float(lightning, "boltWander"), Is.LessThanOrEqualTo(0.12f), "with this much wander a bolt is a saw");
            Assert.That(scene.Float(lightning, "boltShare"), Is.InRange(0f, 1f));
            Assert.That(scene.Float(lightning, "boltCorners"), Is.InRange(2f, StormLightning.MostCorners));

            string camera = scene.Component(scene.GameObjectNamed("Main Camera"), "Camera");
            Assert.That(scene.Float(camera, "far clip plane"), Is.GreaterThan(away.y + lean + 300f),
                "the camera stops drawing before the furthest bolt");
        }

        /// <summary>The three places the glow lives in the include; a re-copy from the package loses all three.</summary>
        [Test]
        public void TheTracer_DrawsTheGlow()
        {
            string text = File.ReadAllText(IncludePath);

            StringAssert.Contains("float4 _StormFlash;", text);
            StringAssert.Contains("float4 _StormFlashLight;", text);
            StringAssert.Contains("volumetricRay.flash      += (StormFlashReach(currentPositionPS) + StormBoltReach(currentPositionPS))", text, "the glow is not gathered along the ray");
            StringAssert.Contains("#define STORM_BOLT_CORNERS " + StormLightning.CloudCorners, text, "the tracer takes another number of bolt corners than StormLightning hands it");
            StringAssert.Contains("float4 _StormBoltCorners[STORM_BOLT_CORNERS];", text);
            StringAssert.Contains("ConvertToPS(GetCameraRelativePositionWS(_StormFlash.xyz))", text,
                "the flash's place is a point in the world, and has to be measured from the camera before ConvertToPS; without it every flash is off by the camera's own position");
            StringAssert.Contains("volumetricRay.scattering += _StormFlashLight.rgb * volumetricRay.flash;", text, "the glow is gathered and never added to the cloud's light");
            StringAssert.Contains("volumetricRay.flash = 0.0;", text, "the glow starts from whatever was in memory");
        }
    }
}
