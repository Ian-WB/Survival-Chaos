using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The Volumetric Clouds row starts from the steps the scene was tuned with
    /// and climbs to HDRP's limit. Both ends of that can drift silently: the
    /// scene's own values can be changed in the Inspector while Low goes on
    /// meaning the old ones, and HDRP clamps a step count past its range
    /// without a word, so a rung can ask for more than it gets and still read
    /// as more in the menu.
    /// </summary>
    public class CloudLadderTests
    {
        private const string SceneProfilePath = "Assets/Scenes/Game/Scene Volume Profile.asset";

        [Test]
        public void Low_IsWhatTheSceneWasTunedWith()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SceneProfilePath);
            Assert.IsNotNull(profile, SceneProfilePath);
            Assert.IsTrue(profile.TryGet(out VolumetricClouds clouds), "the scene has no clouds override");

            Assert.AreEqual(clouds.numPrimarySteps.value, CloudLadder.PrimarySteps(EffectQuality.Low));
            Assert.AreEqual(clouds.numLightSteps.value, CloudLadder.LightSteps(EffectQuality.Low));
        }

        [Test]
        public void Rungs_AreTheChosenStepCounts()
        {
            // The light steps carry the look. Primary steps past 128 hit HDRP's
            // step-length cap from the game camera and only cost time: 512/32
            // rendered the same image as 128/32 for 2.8 ms more.
            Assert.AreEqual(new[] { 64, 64, 128 }, new[]
            {
                CloudLadder.PrimarySteps(EffectQuality.Low),
                CloudLadder.PrimarySteps(EffectQuality.Medium),
                CloudLadder.PrimarySteps(EffectQuality.High)
            });

            Assert.AreEqual(new[] { 6, 16, 32 }, new[]
            {
                CloudLadder.LightSteps(EffectQuality.Low),
                CloudLadder.LightSteps(EffectQuality.Medium),
                CloudLadder.LightSteps(EffectQuality.High)
            });
        }

        [Test]
        public void EveryRung_IsInsideHdrpsRange_AndHighReachesTheLightStepLimit()
        {
            VolumetricClouds clouds = ScriptableObject.CreateInstance<VolumetricClouds>();

            try
            {
                for (int i = 0; i < CloudLadder.Count; i++)
                {
                    EffectQuality rung = CloudLadder.At(i);

                    Assert.That(CloudLadder.PrimarySteps(rung),
                        Is.InRange(clouds.numPrimarySteps.min, clouds.numPrimarySteps.max), rung.ToString());
                    Assert.That(CloudLadder.LightSteps(rung),
                        Is.InRange(clouds.numLightSteps.min, clouds.numLightSteps.max), rung.ToString());
                }

                // High asks for exactly HDRP's ceiling. If an HDRP upgrade
                // raises it, a rung above 32 becomes possible.
                Assert.AreEqual(clouds.numLightSteps.max, CloudLadder.LightSteps(EffectQuality.High));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clouds);
            }
        }

        [Test]
        public void StoredRungsOutsideTheLadder_ClampIntoIt()
        {
            Assert.AreEqual(EffectQuality.Low, CloudLadder.Clamp(EffectQuality.Off));
            Assert.AreEqual(EffectQuality.High, CloudLadder.Clamp(EffectQuality.RayTracedHigh));
            Assert.AreEqual(CloudLadder.PrimarySteps(EffectQuality.High),
                CloudLadder.PrimarySteps(EffectQuality.RayTracedHigh));
        }

        [Test]
        public void RowIndex_RoundTrips()
        {
            Assert.AreEqual(3, CloudLadder.Count);

            for (int i = 0; i < CloudLadder.Count; i++)
            {
                Assert.AreEqual(i, CloudLadder.IndexOf(CloudLadder.At(i)));
            }

            Assert.AreEqual(EffectQuality.Low, CloudLadder.At(0));
            Assert.AreEqual(EffectQuality.High, CloudLadder.At(CloudLadder.Count - 1));
        }

        [Test]
        public void Presets_EveryTierStartsOnLow()
        {
            // Low is the look the clouds were tuned at, and the higher rungs
            // are the player's to buy.
            foreach (GraphicsPreset tier in GraphicsPresets.All)
            {
                Assert.AreEqual(EffectQuality.Low, tier.Clouds, tier.Name);
            }
        }

        [Test]
        public void PickingATier_ResetsTheRow()
        {
            // SetQuality clears exactly the keys in RowKeys. A row missing from
            // it keeps its old rung under a label that names the tier.
            FieldInfo field = typeof(GraphicsDirector).GetField(
                "RowKeys", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "RowKeys");

            Assert.Contains("CloudQuality", (string[])field.GetValue(null));
        }

        [Test]
        public void MenuRow_HasItsOwnNumber_AndNoRetiredOneIsBack()
        {
            Assert.AreEqual(23, (int)GraphicsOptionKind.VolumetricClouds);

            // 11 was the old on/off clouds row. Rows serialised with any of these
            // by an older build must not come back as something else.
            foreach (int retired in new[] { 6, 8, 11, 15, 18, 19, 20, 21 })
            {
                Assert.IsFalse(Enum.IsDefined(typeof(GraphicsOptionKind), retired), retired.ToString());
            }
        }
    }
}
