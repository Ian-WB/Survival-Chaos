using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Lightning in the volcano's ash (environment roadmap, item 39, built
    /// 8 October 2026): a light and a bolt in the plume, driven by
    /// AshLightning in the Game scene.
    ///
    /// It was put off for being a flash in a game about tracking small
    /// rounds, so most of what is held here is that it stays rare and soft:
    /// not before the plume is thick, seconds apart, few strokes, a light
    /// weak enough not to wash the frame, and never on top of the storm's
    /// own lightning.
    /// </summary>
    public class AshLightningTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private static string Lightning(SavedScene scene)
        {
            string gameObject = scene.GameObjectNamed("Ash Lightning");
            Assert.That(gameObject, Is.Not.Null, "there is no Ash Lightning in the Game scene");

            string lightning = scene.ScriptWith(gameObject, "fromLevel");
            Assert.That(lightning, Is.Not.Null, "the Ash Lightning object has no AshLightning on it");
            return lightning;
        }

        [Test]
        public void ABolt_KeepsItsEnds_AndBendsNoFurtherThanItMay()
        {
            Vector3 from = new Vector3(1f, 14f, -2f);
            Vector3 to = new Vector3(2.5f, 20f, 0.5f);
            const float wander = 0.16f;
            Vector3[] corners = new Vector3[AshLightning.MostCorners];

            AshLightning.Bend(from, to, wander, 7, corners, 14);

            Assert.AreEqual(from, corners[0]);
            Assert.AreEqual(to, corners[13]);

            Vector3 along = (to - from).normalized;
            float reach = (to - from).magnitude * wander;
            float furthest = 0f;

            for (int i = 1; i < 13; i++)
            {
                Vector3 off = corners[i] - from;
                float aside = (off - along * Vector3.Dot(off, along)).magnitude;
                Assert.That(aside, Is.LessThanOrEqualTo(reach + 1e-4f), "corner " + i + " stands further off the line than a bolt may");
                furthest = Mathf.Max(furthest, aside);
            }

            Assert.That(furthest, Is.GreaterThan(reach * 0.1f), "a bolt with no bend in it is a stick");
        }

        [Test]
        public void TheSameSeed_BendsTheSameBolt_AndAnotherBendsAnother()
        {
            Vector3 from = Vector3.zero;
            Vector3 to = new Vector3(0f, 6f, 0f);
            Vector3[] a = new Vector3[AshLightning.MostCorners];
            Vector3[] b = new Vector3[AshLightning.MostCorners];
            Vector3[] c = new Vector3[AshLightning.MostCorners];

            AshLightning.Bend(from, to, 0.16f, 31, a, 14);
            AshLightning.Bend(from, to, 0.16f, 31, b, 14);
            AshLightning.Bend(from, to, 0.16f, 32, c, 14);

            Assert.AreEqual(a[6], b[6]);
            Assert.That((a[6] - c[6]).magnitude, Is.GreaterThan(0.01f));

            // A bolt straight up has no "up" to stand square to, and must still bend.
            Assert.That(new Vector2(a[6].x, a[6].z).magnitude, Is.GreaterThan(0f));
            Assert.That(float.IsNaN(a[6].x), Is.False);
        }

        [Test]
        public void FlashesComeSooner_AsTheEruptionGrows()
        {
            Vector2 atFirst = new Vector2(30f, 50f);
            Vector2 atFull = new Vector2(12f, 24f);

            Assert.AreEqual(30f, AshLightning.Gap(0.35f, 0.35f, atFirst, atFull, 0f), 1e-4f);
            Assert.AreEqual(50f, AshLightning.Gap(0.35f, 0.35f, atFirst, atFull, 1f), 1e-4f);
            Assert.AreEqual(12f, AshLightning.Gap(1f, 0.35f, atFirst, atFull, 0f), 1e-4f);
            Assert.AreEqual(24f, AshLightning.Gap(1f, 0.35f, atFirst, atFull, 1f), 1e-4f);

            float before = float.MaxValue;
            for (float level = 0.35f; level <= 1f; level += 0.05f)
            {
                float gap = AshLightning.Gap(level, 0.35f, atFirst, atFull, 0.5f);
                Assert.That(gap, Is.LessThanOrEqualTo(before), "at level " + level);
                before = gap;
            }

            // A pick outside 0 to 1 is not a gap outside the range.
            Assert.AreEqual(12f, AshLightning.Gap(1f, 0.35f, atFirst, atFull, -3f), 1e-4f);
        }

        [Test]
        public void TheGameScene_HasTheLightning_Wired_AndTheStormKnowsIt()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string lightning = Lightning(scene);

            foreach (string field in new[] { "eruption", "smoke", "flash", "bolt", "storm" })
            {
                Assert.That(scene.Reference(lightning, field), Is.Not.EqualTo("0"), "Ash Lightning's " + field + " is not assigned");
            }

            string storm = scene.ScriptWith(scene.GameObjectNamed("Storm Lightning"), "strokeFade");
            Assert.That(scene.Reference(storm, "ash"), Is.Not.EqualTo("0"),
                "the storm's lightning does not know of the ash's, so the two can flash in the same second");
        }

        /// <summary>
        /// The usual limit for flashing light is three flashes in a second.
        /// The storm has up to three strokes and the ash up to two, so the
        /// limit holds only while they take turns a second apart and a
        /// second flash of ash is never close behind the first.
        /// </summary>
        [Test]
        public void ItIsRare_AndNeverOnTopOfTheStorm()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string lightning = Lightning(scene);

            Assert.That(AshLightning.MostStrokes, Is.LessThanOrEqualTo(2));
            Assert.That(Mathf.Max(AshLightning.MostStrokes, StormLightning.MostStrokes), Is.LessThanOrEqualTo(3));
            Assert.That(AshLightning.Turn, Is.GreaterThanOrEqualTo(1f));

            Assert.That(scene.Float(lightning, "fromLevel"), Is.GreaterThanOrEqualTo(0.2f), "the plume is too thin to carry a flash this early");
            Assert.That(scene.Vector(lightning, "gapAtFull").x, Is.GreaterThanOrEqualTo(8f), "even at full, one flash follows another too soon");
            Assert.That(scene.Vector(lightning, "gapAtFirst").x, Is.GreaterThanOrEqualTo(scene.Vector(lightning, "gapAtFull").x));
            Assert.That(scene.Vector(lightning, "strokeGap").x, Is.GreaterThanOrEqualTo(0.08f));
        }

        /// <summary>
        /// Measured in play on 8 October: at 3,000 lumens a flash nearly
        /// doubled the brightness of the whole frame, the lane included. At
        /// 900 the island rises by a sixth and the lane by a hundredth.
        /// </summary>
        [Test]
        public void TheLight_IsSoft_CastsNothing_AndIsOffBetweenFlashes()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string light = scene.Component(scene.GameObjectNamed("Ash Flash"), "Light");
            Assert.That(light, Is.Not.Null, "there is no Ash Flash light");

            Assert.That(scene.Float(light, "m_Intensity"), Is.InRange(300f, 1500f), "a flash this bright washes the frame, or one this weak lights nothing");
            Assert.AreEqual(0f, scene.Float(light, "m_LightUnit"), "the brightness is not in lumens");
            Assert.AreEqual(4f, scene.Float(light, "m_Lightmapping"), "the flash is baked into the island");
            Assert.AreEqual(0f, scene.Float(light, "m_Shadows", "m_Type"), "a flash that casts shadows pays for a shadow map for a tenth of a second");
            Assert.AreEqual(0f, scene.Float(light, "m_Enabled"), "saved switched on, the flash is a lamp over the crater whenever the script is not running");
        }

        [Test]
        public void TheBolt_IsOverTheCrater_AndHiddenBetweenFlashes()
        {
            SavedScene scene = SavedScene.Load(ScenePath);
            string lightning = Lightning(scene);
            string line = scene.Component(scene.GameObjectNamed("Ash Bolt"), "LineRenderer");
            Assert.That(line, Is.Not.Null, "there is no Ash Bolt line");

            Assert.AreEqual(0f, scene.Float(line, "m_Enabled"), "saved switched on, the bolt stands over the crater for good");
            Assert.AreEqual(1f, scene.Float(line, "m_UseWorldSpace"), "the bolt's corners are places in the world");
            Assert.AreEqual(0f, scene.Float(line, "m_CastShadows"));

            Vector2 height = scene.Vector(lightning, "height");
            Assert.That(height.x, Is.GreaterThanOrEqualTo(0f));
            Assert.That(height.y, Is.LessThanOrEqualTo(0.5f), "a bolt this high is behind the HUD's top bar");
            Assert.That(scene.Float(lightning, "offAxis"), Is.LessThanOrEqualTo(0.5f), "a bolt this far out is outside the plume");
            Assert.That(scene.Vector(lightning, "length").y, Is.LessThanOrEqualTo(10f));
            Assert.That(scene.Float(lightning, "corners"), Is.InRange(6f, AshLightning.MostCorners));
        }
    }
}
