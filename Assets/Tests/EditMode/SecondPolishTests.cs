using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The second polish roadmap's rules that can be checked without a scene:
    /// where enemies may arrive (19), what a hit is named after (26), and an
    /// ending that is a win (17).
    /// </summary>
    public class SecondPolishTests
    {
        [TearDown]
        public void ClearOutcome()
        {
            RunOutcome.Clear();
            RunTime.ResetForNewRun();
            RunStats.Clear();
        }

        [Test]
        public void ArrivalsKeepClearOfTheShip()
        {
            for (float shipBearing = -180f; shipBearing < 180f; shipBearing += 17f)
            {
                for (float r = 0f; r <= 1f; r += 0.01f)
                {
                    float bearing = SpawnBearing.Pick(r, shipBearing, 30f);
                    float gap = Mathf.Abs(Mathf.DeltaAngle(bearing, shipBearing));

                    Assert.GreaterOrEqual(gap, 30f - 1e-3f, $"ship {shipBearing}, random {r}");
                    Assert.That(bearing, Is.InRange(-180f, 180f));
                }
            }
        }

        [Test]
        public void ArrivalsStillReachEveryOtherBearing()
        {
            // The two ends of the random range land on the two edges of the clear
            // stretch, and the middle lands opposite the ship.
            Assert.AreEqual(40f, SpawnBearing.Pick(0f, 10f, 30f), 1e-3f);
            Assert.AreEqual(-20f, SpawnBearing.Pick(1f, 10f, 30f), 1e-3f);
            Assert.AreEqual(-170f, SpawnBearing.Pick(0.5f, 10f, 30f), 1e-3f);
        }

        [Test]
        public void NoClearStretchMeansAnyBearing()
        {
            Assert.AreEqual(10f, SpawnBearing.Pick(0f, 10f, 0f), 1e-3f);
        }

        [TestCase("Assets/Prefabs/Boss/boss_disc 1.prefab", "the Leviathan's curtain")]
        [TestCase("Assets/Prefabs/Boss/boss_disc 2.prefab", "the Leviathan's curtain")]
        [TestCase("Assets/Prefabs/Boss/boss_shoot 3.prefab", "the Leviathan's guns")]
        [TestCase("Assets/Prefabs/Boss/boss_shoot 4.prefab", "the Leviathan's guns")]
        [TestCase("Assets/Prefabs/Enemies/enemy_shoot 1.prefab", "a Fighter's round")]
        [TestCase("Assets/Prefabs/Enemies/enemy_shoot 2.prefab", "a Fighter's round")]
        [TestCase("Assets/Prefabs/Enemies/temp_shoot.prefab", "a Heavy's round")]
        [TestCase("Assets/Prefabs/Enemies/temp_shoot1.prefab", "a Heavy's round")]
        public void EveryHostileRoundHasAName(string path, string expected)
        {
            // Named by prefab, so a renamed prefab would quietly become "a round".
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is gone, so its hits are no longer named");
            Assert.AreEqual(expected, DamageSource.OfRoundPrefab(prefab.name));
        }

        [TestCase("Assets/Prefabs/Enemies/Enemy.prefab", "ramming a Fighter")]
        [TestCase("Assets/Prefabs/Enemies/Enemy 1.prefab", "ramming a Heavy")]
        [TestCase("Assets/Prefabs/Enemies/Enemy 2.prefab", "ramming a Scout")]
        [TestCase("Assets/Prefabs/Enemies/Enemy 3.prefab", "ramming a Drone")]
        public void EveryShipYouCanRamHasAName(string path, string expected)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            Assert.AreEqual(expected, DamageSource.OfRam(prefab));
        }

        [Test]
        public void TheCardNamesTheKillingHitAndOnlyTheFirst()
        {
            RunStats.RecordKiller(DamageSource.LeviathanTorpedo);
            RunStats.RecordKiller(DamageSource.LeviathanLance);

            Assert.AreEqual(DamageSource.LeviathanTorpedo, RunStats.KilledBy);
            Assert.AreEqual("LOST TO A LEVIATHAN TORPEDO", RunSummary.LostTo(RunStats.KilledBy));

            RunStats.Clear();
            Assert.IsNull(RunStats.KilledBy, "a new run starts with nothing having ended it");
        }

        [Test]
        public void AWonEndingIsDecidedAndStaysAWinUnderTheCard()
        {
            RunOutcome.ReportEnding(0.25f, won: true);

            Assert.IsTrue(RunOutcome.Decided);
            Assert.IsTrue(RunOutcome.Won);
            Assert.AreEqual(0.25f, Time.timeScale, 1e-4f);

            // The victory card ends the beat; the win is still what it was.
            RunOutcome.ReportRunEnded();
            Assert.IsTrue(RunOutcome.Won);

            RunOutcome.Clear();
            Assert.IsFalse(RunOutcome.Won);
        }

        [Test]
        public void ALostEndingIsNotAWin()
        {
            RunOutcome.ReportEnding(0.25f);
            Assert.IsTrue(RunOutcome.Ending);
            Assert.IsFalse(RunOutcome.Won);
        }
    }
}
