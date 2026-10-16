using NUnit.Framework;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The game camera runs with HDRP's async compute off, through its own
    /// frame settings (Custom Frame Settings > Asynchronous Execution).
    ///
    /// Timed on 16 October 2026 in Checked builds with AsyncComputeBench, on an
    /// RX 6700 XT at Ultra, 2560x1440, FSR 3 native, a frame held 41 s into the
    /// run (storm wall tall) and again at the boss. Each run held one setting
    /// from the start, GPU median of 12 phases of 120 frames:
    ///
    ///                       async on   async off
    ///   RT GI High, early    10.61      10.20     (-0.41 ms)
    ///   RT GI High, boss     13.85      12.99     (-0.87 ms)
    ///   GI Off, early         7.99       7.26     (-0.73 ms)
    ///   GI Off, boss          8.22       8.09     (-0.12 ms)
    ///
    /// Switching within one run agreed, on its steady phases: -0.42 / -0.36 ms
    /// (GI Off, FSR), -0.45 / -0.54 ms (GI Off, TAA), -0.42 / -0.50 ms (RT GI
    /// High), against 0.03-0.17 ms between repeats of one setting. Async on also
    /// wobbled more, about 0.3 ms between phases against 0.06 off. Off was never
    /// the slower. The editor had measured -1.0 ms (finding 6 of the 16 October
    /// performance scan).
    ///
    /// Read from the scene as saved, since that is what a build ships.
    /// </summary>
    public class GameCameraAsyncComputeTests
    {
        private const ulong AsyncBit = 1UL << (int)FrameSettingsField.AsyncCompute;

        [Test]
        public void TheGameCameraTurnsAsyncComputeOff()
        {
            SavedScene scene = SavedScene.Load("Assets/Scenes/Game.unity");
            string camera = scene.GameObjectNamed("Main Camera");
            Assert.IsNotNull(camera, "no Main Camera in Game.unity");

            string data = scene.ScriptWith(camera, "customRenderingSettings");
            Assert.IsNotNull(data, "Main Camera has no HDAdditionalCameraData");

            Assert.AreEqual(1f, scene.Float(data, "customRenderingSettings"), "custom frame settings are off");

            // The flag sits in the first 64 bits, so data1 holds it.
            Assert.Less((int)FrameSettingsField.AsyncCompute, 64);
            ulong mask = scene.Bits(data, "renderingPathCustomFrameSettingsOverrideMask", "mask", "data1");
            ulong value = scene.Bits(data, "m_RenderingPathCustomFrameSettings", "bitDatas", "data1");

            Assert.AreEqual(AsyncBit, mask & AsyncBit, "the camera does not override Asynchronous Execution");
            Assert.AreEqual(0UL, value & AsyncBit, "the camera's override leaves Asynchronous Execution on");
        }

        [Test]
        public void ItOverridesNothingElse()
        {
            SavedScene scene = SavedScene.Load("Assets/Scenes/Game.unity");
            string data = scene.ScriptWith(scene.GameObjectNamed("Main Camera"), "customRenderingSettings");

            Assert.AreEqual(AsyncBit, scene.Bits(data, "renderingPathCustomFrameSettingsOverrideMask", "mask", "data1"));
            Assert.AreEqual(0UL, scene.Bits(data, "renderingPathCustomFrameSettingsOverrideMask", "mask", "data2"));
        }
    }
}
