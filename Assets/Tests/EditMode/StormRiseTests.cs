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
    /// The storm grows at the end of the run (StormRise; built 15 October
    /// 2026 sinking, turned round 15 November): calm, the wall under the
    /// horizon, for three quarters of the way to the boss, then rising to
    /// full over the moon as the boss is due. The arithmetic, the scene's
    /// wiring, and the shader that reads the wall's height off the layer's
    /// depth. What the cloud then looks like, calm and full, is in
    /// CloudRingTests.
    /// </summary>
    public class StormRiseTests
    {
        private const float BossAt = 400f;
        private const string Shader = "Assets/Art/Shaders/VolumetricCloudsUtilities.hlsl";
        private const string ProfilePath = "Assets/Scenes/Game/Scene Volume Profile.asset";

        [Test]
        public void TheStorm_IsCalmFor75Percent_AndFullWhenTheBossIsDue()
        {
            Assert.AreEqual(0f, StormRiseCurve.Grown(0f, BossAt, 0.75f), 1e-6f);
            Assert.AreEqual(0f, StormRiseCurve.Grown(299f, BossAt, 0.75f), 1e-6f, "it grew before 75% of the run");
            Assert.AreEqual(0.5f, StormRiseCurve.Grown(350f, BossAt, 0.75f), 1e-5f, "half grown half way from 300 s to the boss");
            Assert.AreEqual(1f, StormRiseCurve.Grown(BossAt, BossAt, 0.75f), 1e-6f);
            Assert.AreEqual(1f, StormRiseCurve.Grown(BossAt + 60f, BossAt, 0.75f), 1e-6f, "the storm calmed during the fight");
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(0.75f)]
        [TestCase(0.95f)]
        public void TheStorm_NeverCalms_AsTheRunGoesOn(float startsAt)
        {
            float previous = 0f;
            for (float elapsed = 0f; elapsed <= BossAt + 20f; elapsed += 5f)
            {
                float grown = StormRiseCurve.Grown(elapsed, BossAt, startsAt);
                Assert.That(grown, Is.GreaterThanOrEqualTo(previous), "the wall sank again at " + elapsed + " s");
                previous = grown;
            }

            Assert.AreEqual(1f, previous, 1e-6f);
        }

        /// <summary>
        /// Ian, 15 November 2026: the eye and the clouds only show up when
        /// the storm starts, "it should start from nothing". The clouds'
        /// density is the scene's times this share.
        /// </summary>
        [Test]
        public void TheClouds_AreNothingUntilTheStormStarts_AndWholeBy30PercentOfIt()
        {
            Assert.AreEqual(0f, StormRiseCurve.Formed(StormRiseCurve.Grown(0f, BossAt, 0.75f), 0.3f), 1e-6f);
            Assert.AreEqual(0f, StormRiseCurve.Formed(StormRiseCurve.Grown(300f, BossAt, 0.75f), 0.3f), 1e-6f, "cloud before the storm started");
            Assert.That(StormRiseCurve.Formed(StormRiseCurve.Grown(315f, BossAt, 0.75f), 0.3f), Is.InRange(0.3f, 0.7f));
            Assert.AreEqual(1f, StormRiseCurve.Formed(StormRiseCurve.Grown(330f, BossAt, 0.75f), 0.3f), 1e-6f);
            Assert.AreEqual(1f, StormRiseCurve.Formed(1f, 0.3f), 1e-6f);

            float previous = 0f;
            for (float grown = 0f; grown <= 1f; grown += 0.02f)
            {
                float formed = StormRiseCurve.Formed(grown, 0.3f);
                Assert.That(formed, Is.GreaterThanOrEqualTo(previous), "the clouds thinned again at " + grown);
                previous = formed;
            }
        }

        /// <summary>
        /// The storm's lightning comes faster by this (15 November 2026):
        /// nothing until the clouds are whole, at 330 s, and all of it when
        /// the storm is full, as the boss is due and through the fight.
        /// </summary>
        [Test]
        public void TheStormsFury_StartsWhenTheCloudsAreWhole_AndIsAllThereAtTheBoss()
        {
            Assert.AreEqual(0f, StormRiseCurve.Fury(StormRiseCurve.Grown(0f, BossAt, 0.75f), 0.3f), 1e-6f);
            Assert.AreEqual(0f, StormRiseCurve.Fury(StormRiseCurve.Grown(315f, BossAt, 0.75f), 0.3f), 1e-6f, "fury while the clouds are still forming");
            Assert.AreEqual(0f, StormRiseCurve.Fury(StormRiseCurve.Grown(330f, BossAt, 0.75f), 0.3f), 1e-5f);
            Assert.AreEqual(0.5f, StormRiseCurve.Fury(StormRiseCurve.Grown(365f, BossAt, 0.75f), 0.3f), 1e-5f, "half way from whole clouds to the boss");
            Assert.AreEqual(1f, StormRiseCurve.Fury(StormRiseCurve.Grown(BossAt, BossAt, 0.75f), 0.3f), 1e-6f);
            Assert.AreEqual(1f, StormRiseCurve.Fury(StormRiseCurve.Grown(BossAt + 90f, BossAt, 0.75f), 0.3f), 1e-6f, "the storm calmed during the fight");

            float previous = 0f;
            for (float grown = 0f; grown <= 1f; grown += 0.02f)
            {
                float fury = StormRiseCurve.Fury(grown, 0.3f);
                Assert.That(fury, Is.GreaterThanOrEqualTo(previous), "the storm calmed again at " + grown);
                previous = fury;
            }
        }

        [Test]
        public void TheWall_GoesFromItsLowest_ToItsTallest()
        {
            Assert.AreEqual(0.6f, StormRiseCurve.Rise(0f, 2.2f, 0.6f), 1e-6f);
            Assert.AreEqual(2.2f, StormRiseCurve.Rise(1f, 2.2f, 0.6f), 1e-6f);
            Assert.AreEqual(1.4f, StormRiseCurve.Rise(0.5f, 2.2f, 0.6f), 1e-6f);
        }

        /// <summary>
        /// The floor under the island is in the same layer as the wall, so
        /// the layer never gets shallower than the scene's, however low the
        /// wall sinks: calm, the layer and the tiling StormRise sets are the
        /// scene's own numbers, to the last digit.
        /// </summary>
        [Test]
        public void CalmStorm_KeepsTheScenesLayer()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Assert.IsNotNull(profile, ProfilePath);
            Assert.IsTrue(profile.TryGet(out VolumetricClouds clouds), "the scene has no clouds");

            float lowDepth = clouds.altitudeRange.value;
            float depth = StormRiseCurve.Depth(lowDepth, StormRiseCurve.Rise(0f, 2.2f, 0.6f));
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

            Assert.That(scene.Float(rise, "tallest"), Is.InRange(1.5f, 3f), "the full storm has to stand over the moon");
            Assert.That(scene.Float(rise, "lowest"), Is.InRange(0.3f, 1f), "the calm wall is lower than the full one, and not under the floor");
            Assert.AreEqual(0.75f, scene.Float(rise, "startsAt"), 1e-4f, "Ian: the storm starts at 75% of the run");
            Assert.That(scene.Float(rise, "formsBy"), Is.InRange(0.05f, 0.5f), "the clouds have to be whole well before the boss");

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
