using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The HUD changes Ian took on 16 November 2026 ("all as you picked"):
    /// a countdown on the timer bar that turns orange for the horn, marks on
    /// the boss bar where its acts end, the experience bar in gold with a
    /// flash at each level, a banner that names the upgrade and says what it
    /// did, kill numbers that merge and sit under the HUD, offers out of sight
    /// pointed at from the screen's edge, the dash coming back shown on the
    /// ship - and, his own addition, a hull bar that grows with each Max HP pick.
    /// </summary>
    public class Hud1116Tests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string Skills = "Assets/Data/Skills/";

        private static string scene;

        private static string Scene => scene ??= System.IO.File.ReadAllText(ScenePath);

        /// <summary>One serialized number from the saved scene, by a field name only one component has.</summary>
        private static float SceneFloat(string field)
        {
            MatchCollection found = Regex.Matches(Scene, "^  " + field + @": ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.AreEqual(1, found.Count, "Expected one '" + field + "' in the Game scene.");
            return float.Parse(found[0].Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>A serialized list of numbers from the saved scene.</summary>
        private static List<float> SceneList(string field)
        {
            Match found = Regex.Match(Scene, "^  " + field + @":\s*\r?\n((?:  - [-0-9.eE]+\r?\n)+)", RegexOptions.Multiline);
            Assert.IsTrue(found.Success, "Expected a '" + field + "' list in the Game scene.");

            var values = new List<float>();
            foreach (Match item in Regex.Matches(found.Groups[1].Value, @"- ([-0-9.eE]+)"))
            {
                values.Add(float.Parse(item.Groups[1].Value, CultureInfo.InvariantCulture));
            }

            return values;
        }

        /// <summary>A field of one component in the saved scene, found by the component's class.</summary>
        private static float ComponentFloat(string className, string field)
        {
            int at = Scene.IndexOf("SurvivalChaos::SurvivalChaos." + className + "\n", System.StringComparison.Ordinal);
            if (at < 0)
            {
                at = Scene.IndexOf("SurvivalChaos::SurvivalChaos." + className + "\r\n", System.StringComparison.Ordinal);
            }

            Assert.GreaterOrEqual(at, 0, className + " is not in the Game scene.");
            int end = Scene.IndexOf("--- !u!", at, System.StringComparison.Ordinal);
            string block = end > at ? Scene.Substring(at, end - at) : Scene.Substring(at);

            Match value = Regex.Match(block, "^  " + field + @": ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(value.Success, className + " has no '" + field + "' in the Game scene.");
            return float.Parse(value.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static T Skill<T>(string name) where T : SkillDefinition
        {
            var skill = AssetDatabase.LoadAssetAtPath<T>(Skills + name + ".asset");
            Assert.IsNotNull(skill, name);
            return skill;
        }

        private static string Seconds(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture) + " s";
        }

        // ---- Countdown -------------------------------------------------

        [TestCase(600f, "10:00")]
        [TestCase(252.3f, "4:13")]
        [TestCase(60f, "1:00")]
        [TestCase(59.5f, "1:00")]
        [TestCase(0.4f, "0:01")]
        [TestCase(0f, "0:00")]
        [TestCase(-5f, "0:00")]
        public void Countdown_RoundsUp_AndStopsAtZero(float left, string expected)
        {
            Assert.AreEqual(expected, Timer.Countdown(left));
        }

        // ---- Boss bar marks ---------------------------------------------

        [Test]
        public void BossMarks_SitWhereTheEmplacementsAndTheLastActEnd()
        {
            float[] marks = BossEmitter.StageMarks(1000, 240, 30);

            Assert.AreEqual(2, marks.Length);
            Assert.AreEqual(0.76f, marks[0], 0.0001f, "The hull is exposed once the emplacements spend 240.");
            Assert.AreEqual(0.03f, marks[1], 0.0001f, "The last act starts at 30.");
        }

        [Test]
        public void BossMarks_LeaveOutWhatTheBossDoesNotHave()
        {
            Assert.AreEqual(new[] { 0.03f }, BossEmitter.StageMarks(1000, 0, 30));
            Assert.AreEqual(0, BossEmitter.StageMarks(1000, 0, 0).Length);
            Assert.AreEqual(0, BossEmitter.StageMarks(0, 240, 30).Length);
            Assert.AreEqual(0, BossEmitter.StageMarks(200, 240, 0).Length,
                "Emplacements worth the whole pool would put a mark on the frame.");
        }

        [Test]
        public void BarMarks_FallInsideTheFill()
        {
            Assert.AreEqual(4f, BarMarks.AlongBar(1200f, 4f, 0f), 0.001f);
            Assert.AreEqual(600f, BarMarks.AlongBar(1200f, 4f, 0.5f), 0.001f);
            Assert.AreEqual(1196f, BarMarks.AlongBar(1200f, 4f, 1f), 0.001f);
        }

        [Test]
        public void TheScenesBossBar_CarriesMarks()
        {
            StringAssert.Contains("SurvivalChaos::SurvivalChaos.BarMarks", Scene);
        }

        // ---- Hull grows -------------------------------------------------

        [Test]
        public void Hull_GrowsByAPickWorthOfLength_AndKeepsItsSegmentsTheSameSize()
        {
            Assert.AreEqual(220f, HealthBar.WidthFor(20, 11f), 0.001f);
            Assert.AreEqual(330f, HealthBar.WidthFor(30, 11f), 0.001f);
            Assert.AreEqual(660f, HealthBar.WidthFor(60, 11f), 0.001f);

            Assert.AreEqual(10f, HealthBar.SegmentsFor(220f, 11f, 2), 0.001f);
            Assert.AreEqual(30f, HealthBar.SegmentsFor(660f, 11f, 2), 0.001f);
            Assert.AreEqual(220f / 10f, 660f / HealthBar.SegmentsFor(660f, 11f, 2), 0.001f,
                "A segment is as wide on a grown bar as on the first.");
        }

        /// <summary>
        /// The hull starts at 270 under the ship and every Max HP pick
        /// lengthens it, up to the 375 cap of the 17 November 2026 redesign
        /// (it was 220, growing without limit to sit level with a row above
        /// that has since gone). Held to the scene's starting hull and the
        /// pick's amount.
        /// </summary>
        [Test]
        public void Hull_GrowsWithPicksUpToTheCap()
        {
            int start = Mathf.RoundToInt(SceneFloat("healthPoints"));
            float perPoint = ComponentFloat("HealthBar", "pixelsPerHitPoint");
            float cap = ComponentFloat("HealthBar", "maxWidth");

            var maxHealth = Skill<MaxHealthSkill>("MaxHealth");
            int amount = new SerializedObject(maxHealth).FindProperty("amount").intValue;

            Assert.AreEqual(270f, HealthBar.WidthFor(start, perPoint, cap), 0.001f, "The starting length.");
            Assert.Greater(HealthBar.WidthFor(start + amount, perPoint, cap), 270f, "A pick still lengthens it.");
            Assert.AreEqual(375f, HealthBar.WidthFor(start + maxHealth.MaxPicks * amount, perPoint, cap), 0.001f,
                "Every pick taken, the hull stops at the cap.");
        }

        // ---- Banner -----------------------------------------------------

        [Test]
        public void Banner_NamesThePick_AndSaysWhatItDid_WithoutLevelUp()
        {
            var hydra = Skill<ShotUpgradeSkill>("ShotUpgrade");
            string banner = SkillSelect.BannerFor(hydra, 2);

            StringAssert.StartsWith("Hydra II\n", banner);
            StringAssert.Contains("Four rounds", banner);
            StringAssert.DoesNotContain("Level Up", banner);
        }

        [Test]
        public void Banner_WithNoEffect_IsOneLine()
        {
            Assert.AreEqual("Boss kit", SkillSelect.BannerText("Boss kit", ""));
            Assert.AreEqual("A\n<size=62%>B</size>", SkillSelect.BannerText("A", "B"));
        }

        /// <summary>Every upgrade the ring can offer says what each of its picks does.</summary>
        [Test]
        public void EveryOfferableUpgrade_HasAnEffectLineForEachPick()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/Data/Skills" }))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill == null || skill is HealSkill)
                {
                    continue;
                }

                for (int pick = 1; pick <= Mathf.Max(1, skill.MaxPicks); pick++)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(skill.GetEffectLine(pick)),
                        skill.name + " pick " + pick + " has no effect line.");
                }
            }
        }

        /// <summary>
        /// The numbers in the effect lines are the Player's tuning. Like the
        /// loading tips, a line has to stay true, so a retune that leaves one
        /// behind fails here.
        /// </summary>
        [Test]
        public void EffectLines_QuoteTheTuning()
        {
            int fireRate = Mathf.RoundToInt(SceneFloat("attackSpeedStep") * 100f);
            StringAssert.Contains("+" + fireRate + "%", Skill<AttackSpeedSkill>("AttackSpeed").GetEffectLine(1));

            int speed = Mathf.RoundToInt(SceneFloat("moveSpeedStep") * 100f);
            StringAssert.Contains("+" + speed + "%", Skill<MoveSpeedSkill>("MoveSpeed").GetEffectLine(1));

            var maxHealth = Skill<MaxHealthSkill>("MaxHealth");
            int amount = new SerializedObject(maxHealth).FindProperty("amount").intValue;
            StringAssert.Contains("+" + amount, maxHealth.GetEffectLine(1));

            List<float> deflector = SceneList("deflectorRecharge");
            var deflectorSkill = Skill<DeflectorSkill>("Deflector");
            StringAssert.Contains(Seconds(deflector[0]), deflectorSkill.GetEffectLine(1));
            StringAssert.Contains(Seconds(deflector[1]), deflectorSkill.GetEffectLine(2));

            List<float> slowMo = SceneList("slowMoCooldown");
            var slowMoSkill = Skill<SlowMoSkill>("SlowMo");
            StringAssert.Contains(Seconds(slowMo[1]), slowMoSkill.GetEffectLine(2));

            float dash = ComponentFloat("PlayerDash", "cooldown");
            float quickened = Mathf.Max(SceneFloat("dashCooldownFloor"), dash - SceneFloat("dashCooldownStep"));
            StringAssert.Contains(Seconds(quickened), Skill<DashRecoverySkill>("DashRecovery").GetEffectLine(1));
        }

        // ---- Kill numbers -----------------------------------------------

        [Test]
        public void KillNumbers_Merge_OnlyWhenYoungAndNear()
        {
            Vector3 here = new Vector3(10f, 7f, 0f);

            Assert.IsTrue(PickupLabelBoard.ShouldMerge(here, 0.1f, here + Vector3.right * 2f, 0.35f, 3.5f));
            Assert.IsFalse(PickupLabelBoard.ShouldMerge(here, 0.5f, here, 0.35f, 3.5f), "Too old.");
            Assert.IsFalse(PickupLabelBoard.ShouldMerge(here, 0.1f, here + Vector3.right * 5f, 0.35f, 3.5f), "Too far.");
            Assert.IsFalse(PickupLabelBoard.ShouldMerge(here, 0f, here, 0f, 3.5f), "A window of 0 switches it off.");
        }

        /// <summary>Kill numbers fade out before they reach the top bars, whose empty track is see-through.</summary>
        [Test]
        public void KillNumbers_FadeBeforeTheTopBars()
        {
            Assert.AreEqual(1f, PickupLabelBoard.BandFade(900f, 980f, 24f), 0.001f, "Well below the band.");
            Assert.AreEqual(0.5f, PickupLabelBoard.BandFade(968f, 980f, 24f), 0.001f, "Halfway through the fade.");
            Assert.AreEqual(0f, PickupLabelBoard.BandFade(990f, 980f, 24f), 0.001f, "Inside the band.");
        }

        // ---- Dash ready -------------------------------------------------

        [Test]
        public void TheScenesShip_ShowsTheDashComingBack()
        {
            StringAssert.Contains("SurvivalChaos::SurvivalChaos.DashReadyGlint", Scene);
        }

        // ---- Font -------------------------------------------------------

        /// <summary>
        /// The HUD font came in (f9bb4ee, "Font update") with a kerning table
        /// that did not match its glyphs: 24,195 pairs, "3" pulled into "0"
        /// by 0.29 em, "H" into "m", while "AV" had nothing. Ian saw "+30%"
        /// with the digits overlapping on 16 November 2026 and the table was
        /// cleared. Real kerning never moves a pair by more than a few percent
        /// of the em, so anything past 10% means a bad table is back.
        /// </summary>
        [Test]
        public void HudFont_HasNoKerningThatPullsCharactersTogether()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/UI/Fonts/ChakraPetch-Medium SDF.asset");
            Assert.IsNotNull(font, "The HUD font asset is missing.");

            float limit = font.faceInfo.pointSize * 0.1f;
            int bad = 0;
            foreach (var pair in font.fontFeatureTable.glyphPairAdjustmentRecords)
            {
                if (pair.firstAdjustmentRecord.glyphValueRecord.xAdvance < -limit)
                {
                    bad++;
                }
            }

            Assert.AreEqual(0, bad, "Kerning pairs that pull two characters together by more than 10% of the em.");
        }
    }
}
