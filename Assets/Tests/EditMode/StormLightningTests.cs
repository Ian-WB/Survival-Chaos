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
            Assert.That(scene.Vector(lightning, "gapAtFull").x, Is.GreaterThanOrEqualTo(2f), "one flash follows another too soon when the storm is full");
            Assert.That(scene.Vector(lightning, "strokeGap").x, Is.GreaterThanOrEqualTo(0.08f));
            Assert.That(scene.Float(lightning, "strokeRise"), Is.GreaterThanOrEqualTo(0.03f),
                "a stroke that arrives in a frame shows the cloud tracer's squares");

            // A flicker is one stroke more. It is kept a second from the end of whatever was lit
            // before it, by its own gap, and a flash is kept a second from a flicker's start.
            Assert.That(StormLightning.Clear, Is.GreaterThanOrEqualTo(1f));
            Assert.That(scene.Vector(lightning, "flickerGap").x, Is.GreaterThanOrEqualTo(StormLightning.Clear), "a flicker follows a flash too soon");
            Assert.That(scene.Vector(lightning, "flickerGapAtFull").x, Is.GreaterThanOrEqualTo(StormLightning.Clear),
                "a flicker follows a flash too soon when the storm is full");
            Assert.That(scene.Float(lightning, "flickerRise"), Is.GreaterThanOrEqualTo(0.03f),
                "a flicker that arrives in a frame shows the cloud tracer's squares");
        }

        /// <summary>
        /// 15 November 2026: the full storm is the darkest the sky gets, so
        /// the flashes come faster as the wall rises, and a gap chosen while
        /// the storm was young shortens with it.
        /// </summary>
        [Test]
        public void TheWait_ShortensAsTheStormGrows()
        {
            Vector2 first = new Vector2(6f, 14f);
            Vector2 full = new Vector2(2f, 5f);

            Assert.AreEqual(6f, StormLightning.Wait(first, full, 0f, 0f), 1e-5f);
            Assert.AreEqual(14f, StormLightning.Wait(first, full, 0f, 1f), 1e-5f);
            Assert.AreEqual(2f, StormLightning.Wait(first, full, 1f, 0f), 1e-5f);
            Assert.AreEqual(5f, StormLightning.Wait(first, full, 1f, 1f), 1e-5f);
            Assert.AreEqual(6.75f, StormLightning.Wait(first, full, 0.5f, 0.5f), 1e-5f);
            Assert.AreEqual(5f, StormLightning.Wait(first, full, 3f, 2f), 1e-5f, "past full, the gap went on shortening");

            for (float roll = 0f; roll <= 1f; roll += 0.25f)
            {
                float previous = float.PositiveInfinity;
                for (float fury = 0f; fury <= 1f; fury += 0.1f)
                {
                    float wait = StormLightning.Wait(first, full, fury, roll);
                    Assert.That(wait, Is.LessThanOrEqualTo(previous), "the wait grew as the storm did");
                    previous = wait;
                }
            }
        }

        [Test]
        public void TheSavedGaps_AreShorterWhenTheStormIsFull()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string lightning = Lightning(scene);

            foreach (string name in new[] { "gap", "flickerGap" })
            {
                Vector2 first = scene.Vector(lightning, name);
                Vector2 full = scene.Vector(lightning, name + "AtFull");
                Assert.That(first.y, Is.GreaterThanOrEqualTo(first.x), name + " has its most under its least");
                Assert.That(full.y, Is.GreaterThanOrEqualTo(full.x), name + "AtFull has its most under its least");
                Assert.That(full.x, Is.LessThan(first.x), name + " is no shorter when the storm is full");
                Assert.That(full.y, Is.LessThan(first.y), name + " is no shorter when the storm is full");
            }
        }

        /// <summary>
        /// A flicker and a flash share the one light in the tracer and the
        /// one count of strokes in a second. A flicker waits its own gap and
        /// never starts when a flash is due within the clear second, so it
        /// cannot hold a flash up; a flash waits out the second after a
        /// flicker began.
        /// </summary>
        [Test]
        public void AFlicker_NeverHoldsAFlashUp_AndAFlashKeepsClearOfAFlicker()
        {
            Assert.IsFalse(StormLightning.FlickerMayStart(10f, 11f, 30f), "a flicker before its wait is over");
            Assert.IsTrue(StormLightning.FlickerMayStart(11f, 11f, 30f));
            Assert.IsFalse(StormLightning.FlickerMayStart(11f, 11f, 11.5f), "a flicker with a flash due in half a second");
            Assert.IsTrue(StormLightning.FlickerMayStart(11f, 11f, 11f + StormLightning.Clear));
            Assert.IsFalse(StormLightning.FlickerMayStart(12f, 11f, 11.5f), "a flicker after the flash was due, in its place");

            Assert.IsFalse(StormLightning.FlashMayStart(10f, 11f, float.NegativeInfinity), "a flash before its wait is over");
            Assert.IsTrue(StormLightning.FlashMayStart(11f, 11f, float.NegativeInfinity), "with no flicker yet, a flash waits for one");
            Assert.IsFalse(StormLightning.FlashMayStart(11f, 11f, 10.5f), "a flash half a second after a flicker began");
            Assert.IsTrue(StormLightning.FlashMayStart(11.5f, 11f, 10.5f));
        }

        /// <summary>
        /// A flicker is the wall's pulse and not a second flash: dimmer than
        /// the dimmest flash, deeper in the cloud, broader, slower to arrive,
        /// and out before its clear second is, so a flash never waits for one
        /// to die. Like a flash it stays over the horizon and out in the wall.
        /// </summary>
        [Test]
        public void AFlicker_IsDimmerDeeperAndSlowerThanAFlash()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string lightning = Lightning(scene);

            Assert.That(scene.Vector(lightning, "flickerBrightness").y, Is.LessThan(scene.Vector(lightning, "brightness").x),
                "the brightest flicker is as bright as a flash");
            Assert.That(scene.Vector(lightning, "flickerBrightness").x, Is.GreaterThan(0f));
            Assert.That(scene.Float(lightning, "flickerReach"), Is.GreaterThanOrEqualTo(scene.Float(lightning, "reach")));
            Assert.That(scene.Float(lightning, "flickerRise"), Is.GreaterThan(scene.Float(lightning, "strokeRise")));

            Vector2 depth = scene.Vector(lightning, "flickerDepth");
            Assert.That(depth.x, Is.GreaterThan(scene.Vector(lightning, "depth").x), "a flicker can sit on the wall's face, where it is a bright patch");

            Vector2 foot = scene.Vector(lightning, "faceFoot");
            Vector2 top = scene.Vector(lightning, "faceTop");
            Vector2 lowest = StormLightning.Place(foot, top, 0f, depth.y);
            Assert.That(lowest.y, Is.GreaterThan(50f), "the lowest flicker is at the horizon or under it");
            Assert.That(lowest.x, Is.LessThan(2000f), "the deepest flicker is too far into the wall to show");

            float[] at = { 0f };
            float length = StormLightning.Length(at, 1, scene.Float(lightning, "flickerRise"), scene.Float(lightning, "flickerFade"));
            Assert.That(length, Is.LessThanOrEqualTo(StormLightning.Clear), "a flicker is still lit when the flash it kept clear of is due");
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

        /// <summary>
        /// The bolt's lines are opaque and black under their glow, so they
        /// show only while a stroke is bright: dim, they drew darker than the
        /// cloud behind them.
        /// </summary>
        [Test]
        public void TheBoltsLines_ShowOnlyWhileAStrokeIsBright()
        {
            Assert.IsFalse(StormLightning.LineShows(0f, 0f), "a flash that has not lit yet shows its lines");
            Assert.IsFalse(StormLightning.LineShows(0.1f, 0.2f));
            Assert.IsTrue(StormLightning.LineShows(0.2f, 0.2f));
            Assert.IsTrue(StormLightning.LineShows(1f, 0.2f));

            float[] at = { 0f, 0.15f };
            float[] heights = { 1f, 0.6f };
            Assert.IsFalse(StormLightning.LineShows(StormLightning.Glow(0f, at, heights, 2, 0.05f, 0.07f), 0.2f), "the lines show before the first stroke has risen");
            Assert.IsTrue(StormLightning.LineShows(StormLightning.Glow(0.06f, at, heights, 2, 0.05f, 0.07f), 0.2f), "the lines miss the first stroke");
            float end = StormLightning.Length(at, 2, 0.05f, 0.07f);
            Assert.IsFalse(StormLightning.LineShows(StormLightning.Glow(end - 0.01f, at, heights, 2, 0.05f, 0.07f), 0.2f), "the lines hang on, dark, while the last stroke dies away");
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
