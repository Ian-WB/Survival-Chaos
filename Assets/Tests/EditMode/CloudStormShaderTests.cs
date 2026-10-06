using System;
using System.IO;
using System.Security.Cryptography;

using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The Game scene's clouds turn about the island, which HDRP's wind
    /// cannot do. The project carries copies of HDRP's two cloud tracers and
    /// the include they share, with the turn written into the include, and
    /// CloudRingBuilder points the pipeline at the copies.
    ///
    /// Three things can undo that without a sound. An HDRP upgrade or a
    /// Reset on the global settings puts the package's tracers back, and the
    /// clouds go back to drifting in a line. An HDRP upgrade can change the
    /// package's files, and then the copies are copies of something the
    /// pipeline no longer feeds the way they expect. And the wind in the
    /// scene only means a turn a minute while the shader and the builder
    /// agree on how far out the wind's own speed is.
    /// </summary>
    public class CloudStormShaderTests
    {
        private const string Folder = "Assets/Art/Shaders/";
        private const string Package = "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/VolumetricClouds/";
        private const string Include = "VolumetricCloudsUtilities.hlsl";
        private const string ProfilePath = "Assets/Scenes/Game/Scene Volume Profile.asset";

        private const string ResourcesType =
            "UnityEngine.Rendering.HighDefinition.VolumetricCloudsRuntimeResources, " +
            "Unity.RenderPipelines.HighDefinition.Runtime";

        [TestCase("volumetricCloudsTraceCS", "VolumetricCloudsTrace.compute", "RenderClouds")]
        [TestCase("volumetricCloudsTraceShadowsCS", "VolumetricCloudsTraceShadows.compute", "TraceVolumetricCloudsShadows")]
        public void TheClouds_AreTracedByTheProjectsCopy(string property, string file, string kernel)
        {
            Type resources = Type.GetType(ResourcesType);
            Assert.IsNotNull(resources,
                "HDRP no longer has VolumetricCloudsRuntimeResources. It has been renamed or moved, " +
                "so find where the cloud tracers are set now and check the project's copies still fit.");

            object settings = typeof(GraphicsSettings)
                .GetMethod(nameof(GraphicsSettings.GetRenderPipelineSettings), Type.EmptyTypes)
                .MakeGenericMethod(resources)
                .Invoke(null, null);

            var inUse = resources.GetProperty(property).GetValue(settings) as ComputeShader;
            var ours = AssetDatabase.LoadAssetAtPath<ComputeShader>(Folder + file);

            // Unity's ==, not AreSame: see CloudCombineShaderTests.
            Assert.IsNotNull(ours, Folder + file + " is missing.");
            Assert.IsTrue(inUse == ours,
                "The clouds are traced by " + (inUse != null ? AssetDatabase.GetAssetPath(inUse) : "nothing") +
                ", so the storm no longer turns. Run Survival Chaos > Build Cloud Ring.");
            Assert.IsTrue(ours.HasKernel(kernel), Folder + file + " has no " + kernel + " kernel: it did not compile.");
        }

        [TestCase("VolumetricCloudsTrace.compute")]
        [TestCase("VolumetricCloudsTraceShadows.compute")]
        public void TheCopies_ReadTheProjectsInclude(string file)
        {
            string text = File.ReadAllText(Folder + file);

            StringAssert.Contains("#include \"" + Folder + Include + "\"", text);
            StringAssert.DoesNotContain("#include \"" + Package + Include + "\"", text,
                file + " reads the package's include, so it traces clouds that do not turn.");
        }

        /// <summary>The map and both noises: miss one and the wall turns while its shapes stand still, or the other way round.</summary>
        [Test]
        public void EveryRead_IsTurnedFirst()
        {
            string text = File.ReadAllText(Folder + Include);

            foreach (string function in new[] { "AnimateCloudMapPosition", "AnimateShapeNoisePosition", "AnimateErosionNoisePosition" })
            {
                Match body = Regex.Match(text, @"float3 " + function + @"\(float3 positionPS\)\s*\{\s*(.*?);", RegexOptions.Singleline);
                Assert.IsTrue(body.Success, function + " is gone from the project's include.");
                Assert.AreEqual("positionPS = TurnWithTheStorm(positionPS)", body.Groups[1].Value.Trim(), function + " does not turn before it reads.");
            }
        }

        /// <summary>
        /// The copies were made from these, as Unity 6000.6.4f1 ships them.
        /// If this fails HDRP has changed one: diff it against the project's
        /// copy, re-copy, put TurnWithTheStorm back, and put the new hash here.
        /// </summary>
        [TestCase("VolumetricCloudsTrace.compute", "385b4f4f1ffef03814ebfd6eb4093a9b2aa72601a8dfbb087fc14af260a402a9")]
        [TestCase("VolumetricCloudsTraceShadows.compute", "50fe985c30e62cd6b9b8d36ec838c1cdac1e7afd467bc22ed4031c1158268a1d")]
        [TestCase(Include, "3d50db296eb4092e6003c6ee5f8f44af038f10a019fa43d45c8b498c4d17b38c")]
        public void ThePackagesFile_IsTheOneThatWasCopied(string file, string sha256)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(Package + file);
            Assert.IsNotNull(package, "HDRP is not where " + Package + " says");
            string path = Path.Combine(package.resolvedPath, "Runtime/Lighting/VolumetricClouds", file);
            Assert.IsTrue(File.Exists(path), "HDRP no longer ships " + Package + file);

            // Line endings differ by how the package was unpacked.
            var text = new System.Collections.Generic.List<byte>();
            byte[] raw = File.ReadAllBytes(path);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == (byte)'\r' && i + 1 < raw.Length && raw[i + 1] == (byte)'\n')
                {
                    continue;
                }

                text.Add(raw[i]);
            }

            using (SHA256 hasher = SHA256.Create())
            {
                string actual = BitConverter.ToString(hasher.ComputeHash(text.ToArray())).Replace("-", "").ToLowerInvariant();
                Assert.AreEqual(sha256, actual, "HDRP's " + file + " has changed since it was copied into " + Folder);
            }
        }

        /// <summary>
        /// The shader turns the sky by how far the wind has blown over
        /// STORM_TURN_RADIUS, so the scene's wind is the speed of the cloud
        /// that far out. Between them they have to make a turn of about a
        /// minute: Ian found a wall moving at a third of this too slow.
        /// </summary>
        [Test]
        public void TheStorm_GoesRoundInAboutAMinute()
        {
            Match radius = Regex.Match(File.ReadAllText(Folder + Include), @"#define STORM_TURN_RADIUS ([0-9.]+)");
            Assert.IsTrue(radius.Success, "the project's include no longer says how far out the wind's own speed is");
            float metres = float.Parse(radius.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Assert.IsTrue(profile.TryGet(out VolumetricClouds clouds), "the scene has no clouds override");
            Assert.IsTrue(clouds.globalWindSpeed.overrideState);
            Assert.AreEqual(WindParameter.WindOverrideMode.Custom, clouds.globalWindSpeed.value.mode);

            float metresASecond = clouds.globalWindSpeed.value.customValue / 3.6f;
            float seconds = 2f * Mathf.PI * metres / metresASecond;
            Assert.That(seconds, Is.InRange(40f, 90f));
        }

        /// <summary>
        /// The straight push is what is left of HDRP's wind once the storm
        /// turns. The map takes none, or the eye leaves the island; the
        /// shapes a little, or every turn is the last one again; and not
        /// much, because at this wind a full share is 157 metres a second
        /// sideways through a storm that is meant to be going round.
        /// </summary>
        [Test]
        public void TheWindsStraightPush_IsNearlyOff()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Assert.IsTrue(profile.TryGet(out VolumetricClouds clouds), "the scene has no clouds override");

            Assert.IsTrue(clouds.cloudMapSpeedMultiplier.overrideState && clouds.shapeSpeedMultiplier.overrideState && clouds.erosionSpeedMultiplier.overrideState);
            Assert.AreEqual(0f, clouds.cloudMapSpeedMultiplier.value);
            Assert.That(clouds.shapeSpeedMultiplier.value, Is.InRange(0.02f, 0.2f));
            Assert.That(clouds.erosionSpeedMultiplier.value, Is.LessThanOrEqualTo(0.3f));
        }
    }
}
