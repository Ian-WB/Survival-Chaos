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
    ///
    /// Since 7 October the storm turns at three rates: the cloud's shapes
    /// at the wind's, the eye's outline at a sixth of that, the fine detail
    /// a sixth faster. Each is rigid, so nothing winds up.
    ///
    /// Since 12 October a ray's step grows with the distance along it,
    /// because the camera flies inside the cloud layer and HDRP's one long
    /// step left the storm's wall a sample or two a frame.
    ///
    /// And since the same day the ground takes no history from the sky.
    /// A history that follows the storm is read from somewhere else on
    /// every frame, and at the island's edge it carried the sky's cloud
    /// over the rock.
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
        [TestCase("volumetricCloudsCS", "VolumetricClouds.compute", "ReprojectClouds")]
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
                ", so the storm no longer turns. Run Survival Chaos > Environment > Build Cloud Ring.");
            Assert.IsTrue(ours.HasKernel(kernel), Folder + file + " has no " + kernel + " kernel: it did not compile.");
        }

        /// <summary>
        /// HDRP blends each frame of cloud with the frames before, and looks
        /// for last frame's cloud where the cloud is now. This storm turns
        /// and rises, so the project's copy of that pass looks where the
        /// cloud was. The three numbers it does that by are the scene's wind
        /// and rise and the tracer's radius, written out again in the
        /// shader; if one of them moves and the other does not, the history
        /// is read from the wrong place again and the clouds go grainy, as
        /// Ian saw on 8 October 2026.
        /// </summary>
        [Test]
        public void TheCloudsHistory_FollowsTheStorm_ByTheScenesOwnNumbers()
        {
            string history = File.ReadAllText(Folder + "VolumetricClouds.compute");
            StringAssert.Contains("#include \"" + Folder + "VolumetricCloudsDenoising.hlsl\"", history,
                "the history pass reads HDRP's own include, which leaves the cloud standing still");

            string include = File.ReadAllText(Folder + "VolumetricCloudsDenoising.hlsl");
            StringAssert.Contains("prevPos.xyz += thenPS - nowPS;", include, "the place a frame ago is worked out and never used");

            float Number(string text, string name)
            {
                Match match = Regex.Match(text, @"#define\s+" + name + @"\s+([0-9.]+)");
                Assert.IsTrue(match.Success, "no " + name);
                return float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Assert.IsNotNull(profile, ProfilePath);
            Assert.IsTrue(profile.TryGet(out UnityEngine.Rendering.HighDefinition.VolumetricClouds clouds), "the scene has no clouds");

            Assert.AreEqual(clouds.globalWindSpeed.value.customValue, Number(include, "STORM_WIND_KMH"), 0.01f,
                "the scene's Global Wind Speed and the history's copy of it have parted");
            Assert.AreEqual(clouds.verticalShapeWindSpeed.value, Number(include, "STORM_RISE_KMH"), 0.01f,
                "the scene's Vertical Shape Wind Speed and the history's copy of it have parted");
            Assert.AreEqual(Number(File.ReadAllText(Folder + Include), "STORM_TURN_RADIUS"), Number(include, "STORM_TURN_RADIUS"), 0.01f,
                "the tracer and the history turn the storm at different radii");
        }

        /// <summary>
        /// HDRP reads the history between four texels and tests the blend's
        /// depth, which lets a pixel of ground take up to a sixth of its
        /// history from the sky beside it. With HDRP's own clouds that only
        /// happens while the camera turns. Here the history follows the
        /// storm, so it happened on every frame, and the turn carried the
        /// cloud on across the island as a pale trail.
        /// </summary>
        [Test]
        public void TheGround_TakesNoHistory_FromTheSky()
        {
            string history = File.ReadAllText(Folder + "VolumetricClouds.compute");

            StringAssert.Contains("GATHER_GREEN_TEXTURE2D_X(_HistoryVolumetricClouds1Texture", history,
                "the four texels the history is read between are no longer looked at one by one");
            StringAssert.Contains("currentSceneDepth != UNITY_RAW_FAR_CLIP_VALUE && any(historyDepths == UNITY_RAW_FAR_CLIP_VALUE)", history,
                "the test for ground reading the sky's history has changed");

            Match accepted = Regex.Match(history, @"if \(all\(0\.0 <= float4\(historyUV[^\n]*");
            Assert.IsTrue(accepted.Success, "the line that accepts the history was not found");
            StringAssert.Contains("!groundTakesSky", accepted.Value,
                "the history is accepted without asking whether ground is reading the sky");

            // Green is where the pass keeps the scene's depth. Move it and the test above reads something else.
            StringAssert.Contains("float4(finalSampleCount, currentSceneDepth, finalCloudDepth, finalColor.a)", history,
                "the history no longer keeps the scene's depth in its second channel");
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

        /// <summary>
        /// The map and both noises, each at its own rate: miss one and that
        /// part of the storm stands still while the rest goes round it.
        /// </summary>
        [TestCase("AnimateCloudMapPosition", "STORM_OUTLINE_TURN")]
        [TestCase("AnimateShapeNoisePosition", "1.0")]
        [TestCase("AnimateErosionNoisePosition", "STORM_DETAIL_TURN")]
        public void EveryRead_IsTurnedFirst(string function, string share)
        {
            string text = File.ReadAllText(Folder + Include);

            Match body = Regex.Match(text, @"float3 " + function + @"\(float3 positionPS\)\s*\{\s*(.*?);", RegexOptions.Singleline);
            Assert.IsTrue(body.Success, function + " is gone from the project's include.");
            Assert.AreEqual("positionPS = TurnWithTheStorm(positionPS, " + share + ")", body.Groups[1].Value.Trim(),
                function + " does not turn before it reads, or not at its own rate.");
        }

        /// <summary>
        /// The eye's outline has to turn, or the wall is a fixed fence with
        /// cloud sliding along it; and slowly, or the storm is one body
        /// again, a backdrop wound past. The fine detail has to creep over
        /// the shapes, and no faster: at this wind a tenth of the rate is
        /// 16 metres a second at the wall, and much more of that boils.
        /// </summary>
        [Test]
        public void TheOutlineTurnsSlowly_AndTheDetailALittleFaster()
        {
            string text = File.ReadAllText(Folder + Include);

            Assert.That(Share(text, "STORM_OUTLINE_TURN"), Is.InRange(0.05f, 0.4f));
            Assert.That(Share(text, "STORM_DETAIL_TURN"), Is.InRange(1.05f, 1.4f));
        }

        private static float Share(string text, string name)
        {
            Match define = Regex.Match(text, @"#define " + name + @" ([0-9.]+)");
            Assert.IsTrue(define.Success, "the project's include no longer defines " + name);
            return float.Parse(define.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The camera flies inside the cloud layer, so a ray that looks along
        /// it would take HDRP's longest step, an eighth of the layer's
        /// height, all the way out: 187 metres through a wall 1500 metres
        /// off. That was the grain Ian saw boil in the clouds until
        /// 12 October 2026. The tracer's step now grows with distance, and
        /// two things can go wrong with the two numbers it grows by. Too
        /// long at the wall and the grain is back. Too short for too far and
        /// the ray's steps run out early: with half this growth a ray had
        /// 2 km of cloud left in it past the wall, and the far cloud was
        /// seen cut off; with this growth it has over 4, and was not.
        /// Checked at both step counts the Clouds row uses.
        /// </summary>
        [TestCase(64)]
        [TestCase(128)]
        public void ARaysStep_IsShortAtTheWall_AndStillReachesTheFarCloud(int steps)
        {
            string text = File.ReadAllText(Folder + Include);
            float shortest = Share(text, "STORM_SHORTEST_STEP");
            float growth = Share(text, "STORM_STEP_GROWTH");
            float wall = Share(text, "STORM_TURN_RADIUS");

            Match march = Regex.Match(text, @"VolumetricRayResult TraceVolumetricRay\(CloudRay cloudRay\)(.*?)// Normalized the depth we computed", RegexOptions.Singleline);
            Assert.IsTrue(march.Success, "the tracer's march is gone from the project's include");
            StringAssert.Contains("float stepHere = StormStep(stepS, rayMarchRange.start + currentDistance);", march.Value,
                "the march no longer asks for its step at each distance");
            Assert.IsFalse(Regex.IsMatch(march.Value, @"cloudRay\.direction \* stepS"),
                "part of the march still moves by HDRP's one step");

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Assert.IsTrue(profile.TryGet(out VolumetricClouds clouds), "the scene has no clouds override");
            float longest = clouds.altitudeRange.value / 8f;

            Assert.That(Step(wall, steps, longest, shortest, growth), Is.LessThanOrEqualTo(longest / 4f),
                "the step at the storm's wall is long enough to grain again");
            Assert.That(Reach(steps, longest, shortest, growth, float.PositiveInfinity), Is.GreaterThanOrEqualTo(0.75f * steps * longest),
                "a ray through clear air stops well short of where HDRP's did");
            Assert.That(Reach(steps, longest, shortest, growth, wall) - wall, Is.GreaterThanOrEqualTo(4000f),
                "a ray that meets cloud at the wall runs out of steps too soon after it");
        }

        // StormStep, as the include has it.
        private static float Step(float distance, int steps, float longest, float shortest, float growth) =>
            Mathf.Clamp(distance * growth / steps, longest * Mathf.Min(shortest / steps, 1f), longest);

        // The tracer's march along a ray that starts inside the layer, with the jitter at its lowest:
        // clear air out to cloudFrom, unbroken cloud after it. How far out the last sample is.
        private static float Reach(int steps, float longest, float shortest, float growth, float cloudFrom)
        {
            float distance = 0f;
            float limit = steps * longest;
            bool active = true;
            int empty = 0;
            for (int index = 0; index < steps && distance < limit; index++)
            {
                float step = Step(distance, steps, longest, shortest, growth);
                bool cloud = distance >= cloudFrom;
                if (active)
                {
                    if (cloud) empty = 0;
                    else if (++empty == 8) active = false;
                    distance += step;
                }
                else if (!cloud)
                {
                    distance += 2f * step;
                }
                else
                {
                    distance -= step;
                    index--;
                    active = true;
                    empty = 0;
                }
            }

            return distance;
        }

        /// <summary>
        /// The copies were made from these, as Unity 6000.6.4f1 ships them.
        /// If this fails HDRP has changed one: diff it against the project's
        /// copy, re-copy, put TurnWithTheStorm and the lightning back, and put
        /// the new hash here.
        /// </summary>
        [TestCase("VolumetricCloudsTrace.compute", "385b4f4f1ffef03814ebfd6eb4093a9b2aa72601a8dfbb087fc14af260a402a9")]
        [TestCase("VolumetricCloudsTraceShadows.compute", "50fe985c30e62cd6b9b8d36ec838c1cdac1e7afd467bc22ed4031c1158268a1d")]
        [TestCase(Include, "3d50db296eb4092e6003c6ee5f8f44af038f10a019fa43d45c8b498c4d17b38c")]
        [TestCase("VolumetricClouds.compute", "facbfb2daadd52ce8285bc9a61dda2f622ae1a07292bd6b73a1f0afe1b34af0e")]
        [TestCase("VolumetricCloudsDenoising.hlsl", "06dbdc95d23f4b6b42617ed03ffb4d1b51b3945f49f3f3597ed6fcb1f412f7c9")]
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
        /// minute: Ian found a wall moving at a third of this too slow. It
        /// is the cloud's shapes that turn at this rate.
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
