using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using RendererRayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The ray-traced rungs of the GI and Reflections rows, and what goes into
    /// the acceleration structure they trace.
    ///
    /// Until 16 October 2026 the default profile's Quality mode meant HDRP read
    /// every ray-traced setting but full resolution from the profile, so RT Low
    /// and RT Medium were one configuration. The director now writes
    /// Performance, and these tables are what the rungs are.
    /// </summary>
    public class RayTracingLadderTests
    {
        private static void ForEachRayTracedTier(System.Action<string, RenderPipelineSettings> check)
        {
            int found = 0;

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                var asset = QualitySettings.GetRenderPipelineAssetAt(i) as HDRenderPipelineAsset;
                Assert.IsNotNull(asset, QualitySettings.names[i]);

                RenderPipelineSettings settings = asset.currentPlatformRenderPipelineSettings;
                if (!settings.supportRayTracing)
                {
                    continue;
                }

                found++;
                check(QualitySettings.names[i], settings);
            }

            Assert.Greater(found, 0, "no tier compiles ray tracing");
        }

        [Test]
        public void EveryRayTracedTier_CompilesPerformanceOnly()
        {
            // The director writes Performance mode so that the tables below are
            // read. A tier compiling Quality as well would render a branch the
            // tables do not describe.
            ForEachRayTracedTier((name, s) =>
                Assert.AreEqual(RenderPipelineSettings.SupportedRayTracingMode.Performance,
                    s.supportedRayTracingMode, name));
        }

        [Test]
        public void GiLow_SkipsTheSecondDenoise_AndDenoisesAtHalfResolution()
        {
            ForEachRayTracedTier((name, s) =>
            {
                GlobalLightingQualitySettings q = s.lightingQualitySettings;

                Assert.AreEqual(new[] { false, true, true }, q.RTGISecondDenoise, name);
                Assert.AreEqual(new[] { true, false, false }, q.RTGIHalfResDenoise, name);
                Assert.AreEqual(new[] { false, false, true }, q.RTGIFullResolution, name);
                Assert.AreEqual(new[] { true, true, true }, q.RTGIDenoise, name);
            });
        }

        [Test]
        public void GiMediumAndHigh_DenoiseAsTheProfileDid()
        {
            // 0.6 is the default profile's radius, which every rung used while
            // the profile's mode decided. HDRP's template has 0.66 and 1.
            ForEachRayTracedTier((name, s) =>
                Assert.AreEqual(new[] { 0.6f, 0.6f, 0.6f }, s.lightingQualitySettings.RTGIDenoiserRadius, name));
        }

        [Test]
        public void RayTracedReflections_KeepTheProfilesSmoothnessGate()
        {
            // HDRP's template goes 0.6, 0.4, 0, which would send reflection rays
            // off the island's rough rock on every rung.
            ForEachRayTracedTier((name, s) =>
            {
                GlobalLightingQualitySettings q = s.lightingQualitySettings;

                Assert.AreEqual(new[] { 0.9f, 0.9f, 0.9f }, q.RTRMinSmoothness, name);
                Assert.AreEqual(new[] { 0.9f, 0.9f, 0.9f }, q.RTRSmoothnessFadeStart, name);
                Assert.AreEqual(new[] { 0.75f, 0.75f, 0.75f }, q.RTRDenoiserRadiusDimmer, name);
            });
        }

        [Test]
        public void ReflectionsRow_SkipsRtMedium_AndKeepsTheScreenSpaceRungsFirst()
        {
            // RT Medium was RT Low under another name, and a lower smoothness
            // gate to tell them apart changed nothing on this island. The menu
            // indexes the first four as the screen-space row when ray tracing is
            // unavailable, so they must stay first and in order.
            Assert.AreEqual(new[]
            {
                EffectQuality.Off, EffectQuality.Low, EffectQuality.Medium, EffectQuality.High,
                EffectQuality.RayTracedLow, EffectQuality.RayTracedHigh
            }, QualityLadder.ReflectionRungs);
        }

        [Test]
        public void SavedRtMediumReflections_ReadAsRtLow()
        {
            Assert.AreEqual(EffectQuality.RayTracedLow, QualityLadder.ReflectionRung(EffectQuality.RayTracedMedium));

            foreach (EffectQuality rung in QualityLadder.ReflectionRungs)
            {
                Assert.AreEqual(rung, QualityLadder.ReflectionRung(rung), rung.ToString());
            }
        }

        [TestCase("Assets/Prefabs/Enemies/Enemy.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Enemy 1.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Enemy 2.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Enemy 3.prefab")]
        [TestCase("Assets/Prefabs/Enemies/enemy_shoot 1.prefab")]
        [TestCase("Assets/Prefabs/Enemies/enemy_shoot 2.prefab")]
        [TestCase("Assets/Prefabs/Enemies/temp_shoot.prefab")]
        [TestCase("Assets/Prefabs/Enemies/temp_shoot1.prefab")]
        [TestCase("Assets/Prefabs/Projectiles/shoot.prefab")]
        [TestCase("Assets/Prefabs/Projectiles/shoot 1.prefab")]
        [TestCase("Assets/Prefabs/Boss/boss_shoot 3.prefab")]
        [TestCase("Assets/Prefabs/Boss/boss_shoot 4.prefab")]
        [TestCase("Assets/Prefabs/Boss/boss_shoot 5.prefab")]
        [TestCase("Assets/Prefabs/Boss/boss_shoot 6.prefab")]
        [TestCase("Assets/Prefabs/Pickups/Pickup.prefab")]
        public void SmallMovers_StayOutOfTheAccelerationStructure(string path)
        {
            // 102 of them at the boss cost 0.36 ms of RT GI (16 October 2026),
            // and at their size their bounce was not expected to show.
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(root, path);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 0, path);

            foreach (Renderer r in renderers)
            {
                Assert.AreEqual(RendererRayTracingMode.Off, r.rayTracingMode, path + " " + r.name);
            }
        }

        [Test]
        public void TheBoss_StaysInTheAccelerationStructure()
        {
            // Large enough that its bounce shows.
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss.prefab");
            Assert.IsNotNull(root);

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Assert.AreNotEqual(RendererRayTracingMode.Off, r.rayTracingMode, r.name);
            }
        }

        [Test]
        public void ShadowsOff_ReachesTheBulletLights()
        {
            var holder = new GameObject("Bullet light pool under test");
            bool before = BulletLightPool.RowCasts;

            try
            {
                var pool = holder.AddComponent<BulletLightPool>();
                const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

                typeof(BulletLightPool).GetField("pool", Private).SetValue(pool, new Light[4]);
                typeof(BulletLightPool).GetField("shadowCastingCount", Private).SetValue(pool, 2);
                MethodInfo budget = typeof(BulletLightPool).GetMethod("ShadowBudget", Private);
                Assert.IsNotNull(budget, "ShadowBudget");

                if (QualitySettings.shadows == ShadowQuality.Disable)
                {
                    Assert.Inconclusive("the editor's tier disables shadows outright");
                }

                BulletLightPool.RowCasts = true;
                Assert.AreEqual(2, budget.Invoke(pool, null));

                BulletLightPool.RowCasts = false;
                Assert.AreEqual(0, budget.Invoke(pool, null));
            }
            finally
            {
                BulletLightPool.RowCasts = before;
                Object.DestroyImmediate(holder);
            }
        }
    }
}
