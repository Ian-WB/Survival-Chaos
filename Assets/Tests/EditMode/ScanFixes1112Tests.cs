using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// What the scan of 12 November 2026 changed: the intro plays once and
    /// hides under the pause menu, the run's clock restarts with it, the end
    /// card names skills plainly, the overlays use the HUD's font, the title
    /// film dims behind panels, and a Heavy pays for its hull.
    /// </summary>
    public class ScanFixes1112Tests
    {
        [TearDown]
        public void TearDown()
        {
            RunStats.Clock = () => Time.time;
            RunStats.Frame = () => Time.frameCount;
            RunStats.Clear();
        }

        [Test]
        public void TheIntro_PlaysOnTheFirstRunOfASessionOnly()
        {
            Assert.IsTrue(ArenaIntroDirector.ShouldPlay("Game", true, false));
            Assert.IsFalse(ArenaIntroDirector.ShouldPlay("Game", true, true), "Try Again replayed the intro.");
            Assert.IsFalse(ArenaIntroDirector.ShouldPlay("Game", false, false));
            Assert.IsFalse(ArenaIntroDirector.ShouldPlay("Menu", true, false));
        }

        [Test]
        public void TheIntrosOverlay_IsNotDrawnOverThePauseMenuOrTheLoadingScreen()
        {
            Assert.IsTrue(ArenaIntroDirector.OverlayShown(true, false, false, false));
            Assert.IsFalse(ArenaIntroDirector.OverlayShown(true, false, true, false), "Drawn over the pause menu.");
            Assert.IsFalse(ArenaIntroDirector.OverlayShown(true, false, false, true), "Drawn over the loading screen.");
            Assert.IsFalse(ArenaIntroDirector.OverlayShown(true, true, false, false));
            Assert.IsFalse(ArenaIntroDirector.OverlayShown(false, false, false, false));
        }

        [Test]
        public void TheRunsClock_StartsAgainWhenTheIntroHandsOver()
        {
            float now = 10f;
            RunStats.Clock = () => now;
            RunStats.Clear();

            now = 13.5f;
            Assert.AreEqual(3.5f, RunStats.Seconds, 1e-4f, "The intro's 3.5 s is on the clock before the hand-over.");

            RunStats.RestartClock();
            Assert.AreEqual(0f, RunStats.Seconds, 1e-4f);

            now = 20f;
            Assert.AreEqual(6.5f, RunStats.Seconds, 1e-4f);
        }

        [Test]
        public void TheRunsClock_IsNotRestartedOnceTheRunHasEnded()
        {
            float now = 0f;
            RunStats.Clock = () => now;
            RunStats.Clear();

            now = 40f;
            RunStats.Stop();
            now = 45f;
            RunStats.RestartClock();

            Assert.AreEqual(40f, RunStats.Seconds, 1e-4f);
        }

        [TestCase("Attack Speed Increased!", "ATTACK SPEED")]
        [TestCase("Max HP Increased!", "MAX HP")]
        [TestCase("Deflector Online!", "DEFLECTOR")]
        [TestCase("Slow Mo Online!", "SLOW MO")]
        [TestCase("Dash Recovers Faster!", "DASH")]
        [TestCase("More Shots!", "MORE SHOTS")]
        [TestCase("Magnet!", "MAGNET")]
        [TestCase("Online!", "ONLINE")]
        [TestCase("", "")]
        public void TheEndCard_NamesASkillWithoutItsAnnouncement(string announced, string expected)
        {
            Assert.AreEqual(expected, RunSummary.ShortName(announced));
        }

        [Test]
        public void EverySkillsCardName_HasNoExclamationMark()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/Data/Skills" }))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                string name = RunSummary.ShortName(skill.DisplayName);
                Assert.IsNotEmpty(name, skill.name);
                StringAssert.DoesNotContain("!", name, skill.name);
            }
        }

        [Test]
        public void TheShotUpgrade_IsNamedInEnglish()
        {
            var shots = AssetDatabase.LoadAssetAtPath<ShotUpgradeSkill>("Assets/Data/Skills/ShotUpgrade.asset");
            Assert.IsNotNull(shots);

            for (int picks = 0; picks <= 3; picks++)
            {
                StringAssert.DoesNotContain("TUPLO", shots.GetDisplayName(picks));
                StringAssert.DoesNotContain("TUPLO", shots.GetPickupName(picks));
            }

            Assert.AreEqual("Sextuple Shot!", shots.GetDisplayName(99));
        }

        [Test]
        public void TheOverlayFont_IsTheHudsAndIsInResources()
        {
            Font font = Resources.Load<Font>(OverlayFont.ResourcePath);
            Assert.IsNotNull(font, "The intro and the splash fall back to the default font without it.");
            StringAssert.Contains("Chakra", font.name);
        }

        [Test]
        public void OverlayTextSize_FollowsTheScreenBetweenItsLimits()
        {
            Assert.AreEqual(18, OverlayFont.Size(360f, 0.034f, 18, 48));
            Assert.AreEqual(49, OverlayFont.Size(1440f, 0.034f, 18, 64));
            Assert.AreEqual(48, OverlayFont.Size(2160f, 0.034f, 18, 48));
        }

        [Test]
        public void TheTitleFilm_DimsBehindTheOptionsAndTheCredits()
        {
            Assert.AreEqual(1f, TitleSplashDirector.BackdropTarget(true));
            Assert.Less(TitleSplashDirector.BackdropTarget(false), 0.5f);
        }

        [Test]
        public void WhileAVideoIsRecorded_OneFrameIsOneFrameOfFilm()
        {
            Assert.AreEqual(1f / 30f, CaptureClock.Delta(30, 0.17f), 1e-6f, "The trailer ran five times too fast in its own recording.");
            Assert.AreEqual(0.017f, CaptureClock.Delta(0, 0.017f), 1e-6f);
        }

        [Test]
        public void TheHeavy_PaysMoreThanTheFighterItOutlasts()
        {
            var heavy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Heavy.asset");
            var fighter = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Fighter.asset");

            Assert.AreEqual(30, heavy.ExperienceReward);
            Assert.Greater(heavy.ExperienceReward, fighter.ExperienceReward);
        }
    }
}
