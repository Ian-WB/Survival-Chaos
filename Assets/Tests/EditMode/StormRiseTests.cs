using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The storm opens over the run (StormRise, 15 October 2026): the wall
    /// starts a run over the moon and sinks to a stub of itself by the time
    /// the boss is due. The arithmetic, the scene's wiring, and the
    /// shader that reads the wall's height off the layer's depth. What the
    /// cloud then looks like, shut and open, is in CloudRingTests.
    /// </summary>
    public class StormRiseTests
    {
        private const float BossAt = 400f;
        private const string Shader = "Assets/Art/Shaders/VolumetricCloudsUtilities.hlsl";
        private const string ProfilePath = "Assets/Scenes/Game/Scene Volume Profile.asset";

        [Test]
        public void TheStorm_IsShutAsTheRunStarts_AndOpenWhenTheBossIsDue()
        {
            Assert.AreEqual(0f, StormRiseCurve.Open(0f, BossAt, 1.5f), 1e-6f);
            Assert.AreEqual(1f, StormRiseCurve.Open(BossAt, BossAt, 1.5f), 1e-6f);
            Assert.AreEqual(1f, StormRiseCurve.Open(BossAt + 60f, BossAt, 1.5f), 1e-6f, "the wall comes back up after the boss");
        }

        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(1.5f)]
        [TestCase(3f)]
        public void TheStorm_NeverCloses_AsTheRunGoesOn(float lateness)
        {
            float previous = 0f;
            for (float elapsed = 0f; elapsed <= BossAt + 20f; elapsed += 5f)
            {
                float open = StormRiseCurve.Open(elapsed, BossAt, lateness);
                Assert.That(open, Is.GreaterThanOrEqualTo(previous), "the wall rose again at " + elapsed + " s");
                previous = open;
            }
        }

        [Test]
        public void Lateness_HoldsTheWallHighThroughTheFirstMinutes()
        {
            Assert.That(StormRiseCurve.Open(BossAt * 0.5f, BossAt, 1.5f), Is.LessThan(0.5f));
            Assert.That(StormRiseCurve.Open(BossAt * 0.5f, BossAt, 1f), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void TheWall_GoesFromItsTallest_ToItsLowest()
        {
            Assert.AreEqual(2.2f, StormRiseCurve.Rise(0f, 2.2f, 0.6f), 1e-6f);
            Assert.AreEqual(0.6f, StormRiseCurve.Rise(1f, 2.2f, 0.6f), 1e-6f);
            Assert.AreEqual(1.4f, StormRiseCurve.Rise(0.5f, 2.2f, 0.6f), 1e-6f);
        }

        /// <summary>
        /// The floor under the island is in the same layer as the wall, so
        /// the layer never gets shallower than the scene's, however low the
        /// wall sinks: open, the layer and the tiling StormRise sets are the
        /// scene's own numbers, to the last digit.
        /// </summary>
        [Test]
        public void OpenStorm_KeepsTheScenesLayer()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Assert.IsNotNull(profile, ProfilePath);
            Assert.IsTrue(profile.TryGet(out VolumetricClouds clouds), "the scene has no clouds");

            float lowDepth = clouds.altitudeRange.value;
            float depth = StormRiseCurve.Depth(lowDepth, StormRiseCurve.Rise(1f, 2.2f, 0.6f));
            Assert.AreEqual(lowDepth, depth);
            Assert.AreEqual(1f, StormRiseCurve.Tiling(1f, clouds.bottomAltitude.value, lowDepth, depth));
        }

        /// <summary>
        /// The shader reads how tall the wall stands off how deep the layer
        /// is, against the depth the lookup was painted for. That depth and
        /// the scene's have to be one number, or the run would end on a wall
        /// that is not the scene's.
        /// </summary>
        [Test]
        public void TheShadersLowWall_IsTheScenesLayer()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Assert.IsTrue(profile.TryGet(out VolumetricClouds clouds));
            Assert.AreEqual(clouds.altitudeRange.value, Define("STORM_LOW_WALL_DEPTH"), 0.01f);
        }

        [Test]
        public void TheShader_ReadsTheLookupUpTheRisenWall_AndKeepsItsStep()
        {
            string text = File.ReadAllText(Shader);

            StringAssert.Contains("float2(cloudCoverageData.cloudType, StormWallHeight(positionPS, cloudCoverageData.cloudType))", text,
                "the lookup is read up the layer, not up the wall, and the wall cannot rise");
            StringAssert.Contains("min(totalDistance / (float)_NumPrimarySteps, StormLongestStep())", text,
                "a deeper layer lengthens HDRP's step and thins the samples on the wall");
            Assert.AreEqual(0f, Define("STORM_RISE_FROM_KIND"), "the floor under the island must not rise with the wall");
            StringAssert.Contains("return _StormWallRise > 0.0 ? _StormWallRise : 1.0;", text,
                "unset, the wall must stand at its painted height, or the editor's scene view loses it");
        }

        [Test]
        public void TheGameScene_HasTheStormRise_WiredAndFollowingTheRun()
        {
            SavedScene scene = SavedScene.Load("Assets/Scenes/Game.unity");

            string gameObject = scene.GameObjectNamed("Storm Rise");
            Assert.That(gameObject, Is.Not.Null, "there is no Storm Rise in the Game scene");

            string rise = scene.ScriptWith(gameObject, "tallest");
            Assert.That(rise, Is.Not.Null, "the Storm Rise object has no StormRise on it");
            foreach (string field in new[] { "sceneVolume", "tracer", "shadowTracer" })
            {
                Assert.That(scene.Reference(rise, field), Is.Not.Null.And.Not.EqualTo("0"), "StormRise's " + field + " is not assigned");
            }

            Assert.That(scene.Float(rise, "tallest"), Is.InRange(1.5f, 3f), "the wall has to start over the moon");
            Assert.That(scene.Float(rise, "lowest"), Is.InRange(0.3f, 1f), "the wall ends lower than it starts, and not under the floor");

            // Left at or above zero by a tuning session, the storm would hold one height for the whole run.
            Assert.That(scene.Float(rise, "holdAt"), Is.LessThan(0f),
                "Hold At is for trying a look in play; the scene must be saved with it under 0");
        }

        private static float Define(string name)
        {
            Match match = Regex.Match(File.ReadAllText(Shader), @"#define " + name + @" ([0-9.]+)");
            Assert.IsTrue(match.Success, name + " is not defined in " + Shader);
            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }
}
