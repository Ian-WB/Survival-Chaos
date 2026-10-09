using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The volcano as the run's clock (environment roadmap, item 37): the
    /// arithmetic that says how far the eruption has got, when its surges
    /// come on the wave the game ships, and that the Game scene has it wired
    /// and following the run.
    /// </summary>
    public class EruptionTests
    {
        private const float BossAt = 400f;
        private const float Peak = 0.75f;

        /// <summary>
        /// The volcano asks the run's clock at its own Start, which in the Game
        /// scene comes before the director's. Until 8 October 2026 the answer
        /// then was the whole of the game's clock, and a second run began with
        /// the Leviathan's eruption.
        /// </summary>
        [Test]
        public void TheRunsClock_ReadsNothing_UntilTheDirectorHasStarted()
        {
            Assert.AreEqual(0f, WaveDirector.ElapsedAt(633f, 0f, false),
                "a director that has not started took the game's clock for the run's, and the volcano saw the boss's time gone");
            Assert.AreEqual(0f, WaveDirector.ElapsedAt(633f, 633f, true), 1e-4f);
            Assert.AreEqual(12f, WaveDirector.ElapsedAt(645f, 633f, true), 1e-4f);

            // The same question of the component, which nothing has started
            // here: the Test Runner does not run Start.
            GameObject holder = new GameObject("Director under test");

            try
            {
                Assert.AreEqual(0f, holder.AddComponent<WaveDirector>().Elapsed, "the director's own clock does not wait for its Start");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheRestingLevel_StartsAtNothing_AndStopsAtItsPeak()
        {
            Assert.AreEqual(0f, EruptionCurve.Resting(0f, BossAt, Peak, 1f), 1e-6f);
            Assert.AreEqual(Peak, EruptionCurve.Resting(BossAt, BossAt, Peak, 1f), 1e-6f);
            Assert.AreEqual(Peak, EruptionCurve.Resting(BossAt * 3f, BossAt, Peak, 1f), 1e-6f,
                "a fight that runs long does not push the volcano past its peak");
            Assert.AreEqual(0f, EruptionCurve.Resting(-5f, BossAt, Peak, 1f), 1e-6f);
            Assert.AreEqual(0f, EruptionCurve.Resting(100f, 0f, Peak, 1f), 1e-6f, "a wave with no boss has no clock");
        }

        [TestCase(0.6f)]
        [TestCase(1f)]
        [TestCase(2.5f)]
        public void TheRestingLevel_NeverFalls(float lateness)
        {
            float before = -1f;

            for (float t = 0f; t <= BossAt; t += 5f)
            {
                float level = EruptionCurve.Resting(t, BossAt, Peak, lateness);
                Assert.That(level, Is.GreaterThanOrEqualTo(before), "at " + t + " s");
                before = level;
            }
        }

        [Test]
        public void Lateness_KeepsTheFirstHalfQuiet()
        {
            float even = EruptionCurve.Resting(200f, BossAt, Peak, 1f);
            Assert.AreEqual(Peak * 0.5f, even, 1e-5f);
            Assert.That(EruptionCurve.Resting(200f, BossAt, Peak, 2f), Is.LessThan(even));
            Assert.That(EruptionCurve.Resting(200f, BossAt, Peak, 0.6f), Is.GreaterThan(even));
        }

        [Test]
        public void ASurge_Swells_ThenDiesAway()
        {
            const float rise = 0.8f, fall = 9f;

            Assert.AreEqual(0f, EruptionCurve.Pulse(-1f, rise, fall));
            Assert.AreEqual(0f, EruptionCurve.Pulse(0f, rise, fall));
            Assert.AreEqual(1f, EruptionCurve.Pulse(rise, rise, fall), 1e-5f);
            Assert.AreEqual(0f, EruptionCurve.Pulse(rise + fall, rise, fall), 1e-5f);
            Assert.AreEqual(0f, EruptionCurve.Pulse(rise + fall + 60f, rise, fall), 1e-5f);

            float before = 1f;

            for (float since = rise; since <= rise + fall; since += 0.5f)
            {
                float pulse = EruptionCurve.Pulse(since, rise, fall);
                Assert.That(pulse, Is.LessThanOrEqualTo(before + 1e-6f), "still rising " + since + " s in");
                before = pulse;
            }
        }

        /// <summary>
        /// Two groups arriving together are one swell. Summed, a pair of
        /// streams starting on the same second would have surged twice as high
        /// as one.
        /// </summary>
        [Test]
        public void TwoSurgesAtOnce_AreOne()
        {
            var one = new List<float> { 120f };
            var two = new List<float> { 120f, 120f };

            Assert.AreEqual(
                EruptionCurve.Surge(121f, one, 0.8f, 9f),
                EruptionCurve.Surge(121f, two, 0.8f, 9f), 1e-6f);
            Assert.AreEqual(0f, EruptionCurve.Surge(121f, null, 0.8f, 9f));
        }

        [Test]
        public void TheLeviathan_TakesItToFull_AndHoldsIt()
        {
            const float resting = 0.75f, height = 0.25f, arrival = 3f;

            Assert.AreEqual(resting, EruptionCurve.Level(resting, 0f, height, -1f, arrival), 1e-6f);
            Assert.AreEqual(resting, EruptionCurve.Level(resting, 0f, height, 0f, arrival), 1e-6f,
                "it eases up from where it was, with no jump");
            Assert.That(EruptionCurve.Level(resting, 0f, height, 1.5f, arrival), Is.InRange(resting + 0.05f, 0.99f));
            Assert.AreEqual(1f, EruptionCurve.Level(resting, 0f, height, arrival, arrival), 1e-6f);
            Assert.AreEqual(1f, EruptionCurve.Level(0.1f, 0f, height, 500f, arrival), 1e-6f,
                "a boss skipped to early is still the volcano at full");
        }

        [Test]
        public void TheLevel_NeverLeavesNoughtToOne()
        {
            Assert.AreEqual(1f, EruptionCurve.Level(0.9f, 1f, 0.5f, -1f, 3f), 1e-6f);
            Assert.AreEqual(0f, EruptionCurve.Level(0f, 0f, 0.25f, -1f, 3f), 1e-6f);
        }

        /// <summary>
        /// On the wave the game ships: one surge for the Drones, which are
        /// eleven streams starting inside six seconds, one for each later
        /// kind of enemy and each later stream, and none for the boss or for
        /// what is there as the run opens.
        /// </summary>
        [Test]
        public void OnMainRun_TheSurgesComeWithEachNewGroup()
        {
            WaveDefinition wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>("Assets/Data/Waves/MainRun.asset");
            Assert.IsNotNull(wave);

            var surges = new List<float>();
            EruptionCurve.SurgeTimes(wave.Streams, 30f, surges);

            Assert.AreEqual(5, surges.Count, string.Join(", ", surges));
            Assert.AreEqual(14.67f, surges[0], 0.01f);
            Assert.AreEqual(120f, surges[1], 0.01f);
            Assert.AreEqual(160f, surges[2], 0.01f);
            Assert.AreEqual(186.67f, surges[3], 0.01f);
            Assert.AreEqual(226.67f, surges[4], 0.01f);
            Assert.That(surges, Has.None.GreaterThanOrEqualTo(wave.BossArrivesAt));
        }

        [Test]
        public void TheGameScene_HasTheEruption_WiredAndFollowingTheRun()
        {
            SavedScene scene = SavedScene.Load("Assets/Scenes/Game.unity");

            string gameObject = scene.GameObjectNamed("Eruption");
            Assert.That(gameObject, Is.Not.Null, "there is no Eruption in the Game scene");

            string eruption = scene.ScriptWith(gameObject, "restingPeak");
            Assert.That(eruption, Is.Not.Null, "the Eruption object has no Eruption on it");

            foreach (string field in new[] { "lava", "craterLight", "smoke" })
            {
                Assert.That(scene.Reference(eruption, field), Is.Not.Null.And.Not.EqualTo("0"),
                    "Eruption's " + field + " is not assigned");
            }

            // Left above zero by a tuning session, the volcano would sit at
            // one level for the whole run.
            Assert.That(scene.Float(eruption, "holdAt"), Is.LessThan(0f),
                "Hold At is for trying a look in play; the scene must be saved with it under 0");
        }

        /// <summary>
        /// The lava's flow is sped up by leading its clock, a property the
        /// component sets by name. Renamed or dropped from the graph, the
        /// set would do nothing and say nothing.
        /// </summary>
        [Test]
        public void TheLava_HasWhatTheEruptionSets()
        {
            Material lava = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Scenario/lava_.mat");
            Assert.IsNotNull(lava);
            Assert.IsFalse(ShaderUtil.ShaderHasError(lava.shader), "the lava's shader has errors");

            foreach (string property in new[] { "_Glow", "_VeinContrast", "_FlowLead" })
            {
                Assert.IsTrue(lava.HasProperty(property), "the lava's shader has no " + property);
            }

            Assert.AreEqual(0f, lava.GetFloat("_FlowLead"),
                "saved at anything but 0, the lava would start a run with its flow out of step");
        }
    }
}
