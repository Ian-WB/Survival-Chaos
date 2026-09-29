#if ENABLE_UPSCALER_FRAMEWORK
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The parts of FSR 3 that can go wrong without anything on screen saying
    /// so: a stored setting meaning a different upscaler, a jitter sequence a
    /// phase short, a pipeline asset left listing FSR 3 so it takes over when
    /// the menu says Off, and the DLLs quietly left out of a build.
    /// </summary>
    public class Fsr3Tests
    {
        private const string PluginFolder = "Assets/Plugins/FidelityFX/x86_64/";

        [Test]
        public void StoredUpscalerNumbersNeverMove()
        {
            // A player's settings file stores these numbers.
            Assert.AreEqual(0, (int)UpscaleMethod.Off);
            Assert.AreEqual(1, (int)UpscaleMethod.Fsr);
            Assert.AreEqual(2, (int)UpscaleMethod.Dlss);
            Assert.AreEqual(3, (int)UpscaleMethod.Fsr3);

            Assert.AreEqual("FSR 2", DisplayOptions.UpscaleMethodNames[(int)UpscaleMethod.Fsr]);
            Assert.AreEqual("FSR 3", DisplayOptions.UpscaleMethodNames[(int)UpscaleMethod.Fsr3]);
        }

        [TestCase(1.5f, 18)]
        [TestCase(1.7f, 23)]
        [TestCase(2.0f, 32)]
        [TestCase(3.0f, 72)]
        public void JitterPhasesMatchAmdsTable(float ratio, int phases)
        {
            Assert.AreEqual(phases, Fsr3Upscaler.JitterPhaseCount(ratio));
        }

        [Test]
        public void JitterPhasesSurviveARatioRebuiltFromPixels()
        {
            // 2560 wide at Quality renders 1707, and 2560 / 1707 is a hair
            // under 1.5. Truncating 8 x ratio squared straight would give 17.
            Assert.AreEqual(18, Fsr3Upscaler.JitterPhaseCount(2560f / 1707f));
        }

        [Test]
        public void JitterStaysInsideAPixelAndStartsWhereAmdsDoes()
        {
            Fsr3Upscaler upscaler = new Fsr3Upscaler();

            upscaler.CalculateJitter(0, 1.5f, out Vector2 first, out _);
            Assert.AreEqual(0f, first.x, 1e-5f);
            Assert.AreEqual(1f / 3f - 0.5f, first.y, 1e-5f);

            HashSet<Vector2> seen = new HashSet<Vector2>();
            for (int frame = 0; frame < 18; frame++)
            {
                upscaler.CalculateJitter(frame, 1.5f, out Vector2 jitter, out _);
                Assert.That(Mathf.Abs(jitter.x), Is.LessThanOrEqualTo(0.5f));
                Assert.That(Mathf.Abs(jitter.y), Is.LessThanOrEqualTo(0.5f));
                seen.Add(jitter);
            }

            Assert.AreEqual(18, seen.Count, "every phase of the sequence should be a different offset");

            upscaler.CalculateJitter(18, 1.5f, out Vector2 wrapped, out _);
            Assert.AreEqual(first, wrapped, "the sequence should repeat after its phase count");
        }

        [Test]
        public void NoPipelineAssetOnDiskListsFsr3()
        {
            int checkedCount = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:HDRenderPipelineAsset", new[] { "Assets" }))
            {
                HDRenderPipelineAsset asset =
                    AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                List<string> names =
                    asset.currentPlatformRenderPipelineSettings.dynamicResolutionSettings.advancedUpscalerNames;

                checkedCount++;
                Assert.IsFalse(names != null && names.Contains(Fsr3Upscaler.UpscalerName),
                    asset.name + " lists FSR 3, so it would run whenever the menu says Off at a reduced scale");
            }

            Assert.Greater(checkedCount, 0, "found no pipeline assets to check");
        }

        [TestCase("SurvivalChaosFfx")]
        [TestCase("amd_fidelityfx_loader_dx12")]
        [TestCase("amd_fidelityfx_upscaler_dx12")]
        public void PluginShipsWithWindowsBuildsAndLoadsInTheEditor(string dll)
        {
            PluginImporter importer = AssetImporter.GetAtPath(PluginFolder + dll + ".dll") as PluginImporter;
            Assert.IsNotNull(importer, dll + ".dll is missing from " + PluginFolder);

            Assert.IsTrue(importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64),
                dll + " would be left out of Windows builds");
            Assert.IsTrue(importer.GetCompatibleWithEditor(), dll + " would not load in the editor");
            Assert.AreEqual("x86_64", importer.GetPlatformData(BuildTarget.StandaloneWindows64, "CPU"));
        }

        [Test]
        public void FrameworkRegistersFsr3AndNotUnitysSpareCopies()
        {
            Dictionary<System.Type, (System.Type OptionsType, string ID)> registered =
                UpscalerRegistry.s_RegisteredUpscalers;

            Assert.IsTrue(registered.ContainsKey(typeof(Fsr3Upscaler)), "FSR 3 is not registered");
            Assert.AreEqual(Fsr3Upscaler.UpscalerName, registered[typeof(Fsr3Upscaler)].ID);

            foreach (KeyValuePair<System.Type, (System.Type OptionsType, string ID)> entry in registered)
            {
                Assert.AreNotEqual("DLSSIUpscaler", entry.Key.Name,
                    "Unity's framework DLSS is back, and warns on every non-NVIDIA card");
                Assert.AreNotEqual("FSR2IUpscaler", entry.Key.Name);
            }
        }
    }
}
#endif
