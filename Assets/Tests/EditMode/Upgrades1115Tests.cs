using NUnit.Framework;
using UnityEditor;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// What Ian asked for on 15 November 2026 after playing the scan build:
    /// the shot upgrade renamed Hydra with stages of two, four and eight
    /// rounds, its last only close to the end of the run; every upgrade named
    /// with its pick's Roman numeral; and the two shooting ships made harder
    /// on 10 November given twice that health again.
    /// </summary>
    public class Upgrades1115Tests
    {
        private const string ShotUpgrade = "Assets/Data/Skills/ShotUpgrade.asset";

        /// <summary>
        /// Level 14 is about where a run meets the Leviathan, which arrives at
        /// 400 s. The middle stage opens at 8, where the old six-round stage did.
        /// </summary>
        [Test]
        public void Hydra_OpensAtLevels3_8_And14()
        {
            var hydra = AssetDatabase.LoadAssetAtPath<ShotUpgradeSkill>(ShotUpgrade);
            Assert.IsNotNull(hydra);
            Assert.AreEqual(3, hydra.MaxPicks);

            Assert.IsFalse(hydra.IsAvailableAt(0, 2));
            Assert.IsTrue(hydra.IsAvailableAt(0, 3), "Hydra I");
            Assert.IsFalse(hydra.IsAvailableAt(1, 7));
            Assert.IsTrue(hydra.IsAvailableAt(1, 8), "Hydra II");
            Assert.IsFalse(hydra.IsAvailableAt(2, 13), "Hydra III came before the end of the run.");
            Assert.IsTrue(hydra.IsAvailableAt(2, 14), "Hydra III");
        }

        [Test]
        public void Hydra_FiresTwo_ThenFour_ThenEightRounds()
        {
            Assert.AreEqual(1, Player.RoundsAtStage(0));
            Assert.AreEqual(2, Player.RoundsAtStage(1));
            Assert.AreEqual(4, Player.RoundsAtStage(2));
            Assert.AreEqual(8, Player.RoundsAtStage(3));
            Assert.AreEqual(3, Player.MaxShotUpgrades);
        }

        /// <summary>The scene's list moves Hydra one stage a pick, so the middle stage is not skipped.</summary>
        [Test]
        public void TheScenesPlayer_TakesHydraOneStageAPick()
        {
            string scene = System.IO.File.ReadAllText("Assets/Scenes/Game.unity");
            StringAssert.Contains("shotStageByPick: 010000000200000003000000", scene);
        }

        [Test]
        public void Hydra_IsNamedAndAnnouncedPerStage()
        {
            var hydra = AssetDatabase.LoadAssetAtPath<ShotUpgradeSkill>(ShotUpgrade);

            Assert.AreEqual("Hydra I", hydra.GetPickupName(1));
            Assert.AreEqual("Hydra II", hydra.GetPickupName(2));
            Assert.AreEqual("Hydra III", hydra.GetPickupName(3));
            StringAssert.Contains("Eight", hydra.GetDisplayName(3));
            Assert.AreEqual(hydra.GetDisplayName(3), hydra.GetDisplayName(99), "A spent Hydra names its last stage.");
            Assert.AreEqual("HYDRA", RunSummary.ShortName(hydra.DisplayName));
        }

        [TestCase(1, "I")]
        [TestCase(2, "II")]
        [TestCase(4, "IV")]
        [TestCase(6, "VI")]
        [TestCase(9, "IX")]
        [TestCase(14, "XIV")]
        [TestCase(0, "0")]
        public void Roman_WritesPickCounts(int number, string expected)
        {
            Assert.AreEqual(expected, SkillDefinition.Roman(number));
        }

        /// <summary>
        /// Every upgrade's pickup and banner carry the pick's numeral, the
        /// first pick of anything being I. Heal repeats without limit and
        /// is not numbered.
        /// </summary>
        [Test]
        public void EveryUpgrade_IsNamedWithItsPicksNumeral()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/Data/Skills" }))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));

                if (skill.IsUnlimited)
                {
                    StringAssert.DoesNotEndWith(" I", skill.GetPickupName(1), skill.name);
                    continue;
                }

                for (int pick = 1; pick <= skill.MaxPicks; pick++)
                {
                    string numeral = " " + SkillDefinition.Roman(pick);
                    StringAssert.EndsWith(numeral, skill.GetPickupName(pick), skill.name + " pickup, pick " + pick);
                    StringAssert.Contains(numeral + "!", skill.GetDisplayName(pick), skill.name + " banner, pick " + pick);
                }
            }
        }

        /// <summary>Slow Mo's first banner still teaches its button.</summary>
        [Test]
        public void SlowMoI_StillSaysWhichButton()
        {
            var slowMo = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Data/Skills/SlowMo.asset");
            StringAssert.Contains("Press E or Y", slowMo.GetDisplayName(1));
        }

        // Doubled to 4 and 20 on 15 November, and put back the same night.
        [TestCase("Assets/Data/Enemies/Fighter.asset", 2)]
        [TestCase("Assets/Data/Enemies/Heavy.asset", 10)]
        public void TheShootingShips_KeepTheirTenthOfNovemberHealth(string path, int health)
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            Assert.IsNotNull(definition, path);
            Assert.AreEqual(health, definition.MaxHealth, path);
        }
    }
}
