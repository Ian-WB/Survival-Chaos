using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The HUD and menu redesign Ian approved on 17 November 2026
    /// (Artifacts/hud-redesign/build-brief.md), one group of tests per block.
    /// </summary>
    public class HudRedesign1117Tests
    {
        private const string Materials = "Assets/UI/Materials";

        /// <summary>The bars that sit on a HUD plate (HudLayout1117.InPlateBars).</summary>
        private static readonly string[] InPlateBars =
            { "HealthBar", "DashBar", "DeflectorBar", "SlowMoBar", "XPBar", "TimerBar" };

        private static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>
        /// Block 1, his words: the thin frame with small corner ticks of the
        /// mockups, not the old thick 26 px brackets, on every panel and bar.
        /// </summary>
        [Test]
        public void HoloFrames_AreThinWithSmallBrightCornerTicks()
        {
            int checkedCount = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Materials }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null ||
                    (m.shader.name != "Survival Chaos/Holo Panel" && m.shader.name != "Survival Chaos/Holo Bar"))
                {
                    continue;
                }

                checkedCount++;

                // Block 3 fix: a bar on a plate has no frame of its own, so the
                // plate reads as the one frame (lead, 17 November 2026).
                if (System.Array.IndexOf(InPlateBars, m.name) >= 0)
                {
                    Assert.AreEqual(0f, m.GetFloat("_FrameStrength"), path + ": a bar on a plate keeps no frame.");
                    Assert.AreEqual(0f, m.GetFloat("_BracketArm"), path + ": a bar on a plate keeps no corner ticks.");
                    continue;
                }

                Assert.LessOrEqual(m.GetFloat("_Border"), 1.5f, path + ": frame line too thick.");
                Assert.LessOrEqual(m.GetFloat("_Chamfer"), 3f, path + ": corners cut too deep.");

                float arm = m.GetFloat("_BracketArm");
                Assert.Greater(arm, 0f, path + ": no corner ticks.");
                Assert.LessOrEqual(arm, 10f, path + ": corner ticks too long (the old ones were 26).");
                Assert.LessOrEqual(m.GetFloat("_BracketWidth"), 2f, path + ": corner ticks too heavy (the old ones were 4).");

                Assert.Greater(Luma(m.GetColor("_BracketColor")), Luma(m.GetColor("_EdgeColor")),
                    path + ": the ticks should be brighter than the frame they mark.");
            }

            Assert.GreaterOrEqual(checkedCount, 20, "Expected the HUD and menu materials in " + Materials + ".");
        }

        private static Color Hex(string hex)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out Color c), hex);
            return c;
        }

        private static void AssertColour(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, 0.01f, what + " (red)");
            Assert.AreEqual(expected.g, actual.g, 0.01f, what + " (green)");
            Assert.AreEqual(expected.b, actual.b, 0.01f, what + " (blue)");
        }

        private static Material Mat(string name)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/" + name + ".mat");
            Assert.IsNotNull(m, name);
            return m;
        }

        /// <summary>
        /// Block 2: the hull turns amber at a quarter and crimson at 15%, in
        /// whole hit points: at the starting 20, amber at 5 and crimson at 3.
        /// </summary>
        [TestCase(20, 20, HealthBar.Stage.Healthy)]
        [TestCase(6, 20, HealthBar.Stage.Healthy)]
        [TestCase(5, 20, HealthBar.Stage.Caution)]
        [TestCase(4, 20, HealthBar.Stage.Caution)]
        [TestCase(3, 20, HealthBar.Stage.Critical)]
        [TestCase(1, 20, HealthBar.Stage.Critical)]
        [TestCase(8, 30, HealthBar.Stage.Healthy)]
        [TestCase(7, 30, HealthBar.Stage.Caution)]
        [TestCase(4, 30, HealthBar.Stage.Critical)]
        [TestCase(0, 0, HealthBar.Stage.Healthy)]
        public void HullStage_FollowsTheQuarterAndFifteenPercentLines(int health, int max, HealthBar.Stage expected)
        {
            Assert.AreEqual(expected, HealthBar.StageFor(health, max));
        }

        /// <summary>
        /// Block 2: one meaning per colour on the bars' own materials, which
        /// keep their colours apart from the palette in HoloUiFactory.
        /// </summary>
        [Test]
        public void HudBars_SpeakTheColourLanguage()
        {
            AssertColour(Hex("#2EE57D"), Mat("HealthBar").GetColor("_FillColor"), "Hull green");
            AssertColour(Hex("#FF3B4E"), Mat("BossBar").GetColor("_FillColor"), "Boss crimson");
            AssertColour(Hex("#78C2D4"), Mat("TimerBar").GetColor("_FillColor"), "Timer ice-cyan");
            AssertColour(Hex("#FFD140"), Mat("XPBar").GetColor("_FillColor"), "Experience gold");

            foreach (string ability in new[] { "DashBar", "DeflectorBar", "SlowMoBar" })
            {
                AssertColour(Hex("#29D9FF"), Mat(ability).GetColor("_FillColor"), ability + " cyan");
            }
        }

        /// <summary>
        /// Block 2: the panels sit on the dark #04101A plate at 84-91%, so
        /// lava behind the pause and level-up panels no longer shows through.
        /// </summary>
        [Test]
        public void Panels_SitOnTheDarkBackingPlate()
        {
            foreach (string panel in new[] { "HoloMenuPanel", "HoloPrompt", "LevelUpPanel", "LoadingPanel" })
            {
                Color fill = Mat(panel).GetColor("_FillColor");
                AssertColour(Hex("#04101A"), fill, panel);
                Assert.That(fill.a, Is.InRange(0.84f, 0.92f), panel + " plate opacity");
            }
        }

        /// <summary>
        /// Block 3: the HUD box is the screen clamped to 16:9 less 5.5% a side,
        /// so an ultrawide keeps its corners over the middle. Canvas units at
        /// Match 0.5: 1920x1080 at 16:9, about 2226x932 at 3440x1440.
        /// </summary>
        [Test]
        public void HudFrame_ClampsTo16By9WithSafeMargins()
        {
            Vector2 wide = HudSafeFrame.SizeFor(new Vector2(1920f, 1080f), 16f / 9f, 0.055f);
            Assert.AreEqual(1920f * 0.89f, wide.x, 0.5f);
            Assert.AreEqual(1080f * 0.89f, wide.y, 0.5f);

            Vector2 ultra = HudSafeFrame.SizeFor(new Vector2(2226f, 932f), 16f / 9f, 0.055f);
            Assert.AreEqual(932f * 16f / 9f * 0.89f, ultra.x, 0.5f, "21:9 is held to a 16:9 box.");
            Assert.AreEqual(932f * 0.89f, ultra.y, 0.5f);
        }

        /// <summary>
        /// Block 3: Max HP picks still lengthen the hull bar, up to a cap.
        /// </summary>
        [Test]
        public void HullWidth_GrowsWithMaxHpUpToTheCap()
        {
            Assert.AreEqual(270f, HealthBar.WidthFor(20, 13.5f, 375f), 0.01f);
            Assert.AreEqual(351f, HealthBar.WidthFor(26, 13.5f, 375f), 0.01f);
            Assert.AreEqual(375f, HealthBar.WidthFor(30, 13.5f, 375f), 0.01f);
            Assert.AreEqual(375f, HealthBar.WidthFor(40, 13.5f, 375f), 0.01f);
            Assert.AreEqual(540f, HealthBar.WidthFor(40, 13.5f, 0f), 0.01f, "0 means no cap.");
        }

        [Test]
        public void HullCount_ReadsCurrentOverMaximum()
        {
            string text = HealthBar.CountText(18, 20, Hex("#2EE57D"));
            Assert.AreEqual("<color=#2EE57D>18</color> / 20", text);
        }

        /// <summary>
        /// Block 3, in the saved scene: the HUD sits in its safe frame, the
        /// hull on its plate with the count, and the timer hides as a box.
        /// </summary>
        [Test]
        public void GameScene_HasTheRedesignedHudLayout()
        {
            string scene = System.IO.File.ReadAllText("Assets/Scenes/Game.unity");
            foreach (string name in new[] { "HUD Frame", "Timer Plate", "Hull Plate", "Hull Count", "Dash Plate",
                         "Deflector Plate", "Slow Mo Plate", "XP Plate" })
            {
                StringAssert.Contains("m_Name: " + name + "\n", scene.Replace("\r\n", "\n"), name + " missing.");
            }

            StringAssert.Contains("maxWidth: 375", scene, "The hull bar's growth cap.");
            StringAssert.Contains("pixelsPerHitPoint: 13.5", scene);
        }

        /// <summary>
        /// Block 4: no vignette above a quarter; a breath at amber that never
        /// goes dark; at crimson a base that each heartbeat kicks and that
        /// settles back before the next.
        /// </summary>
        [Test]
        public void LowHullVignette_BreathesAtAmberAndBeatsAtCrimson()
        {
            Assert.AreEqual(0f, HealthBar.VignetteFor(HealthBar.Stage.Healthy, 1.3f, 0f, 0.22f, 0.55f, 0.9f, 0.45f, 4f));

            float low = float.MaxValue, high = 0f;
            for (float t = 0f; t < 2.5f; t += 0.01f)
            {
                float v = HealthBar.VignetteFor(HealthBar.Stage.Caution, t, 9f, 0.22f, 0.55f, 0.9f, 0.45f, 4f);
                low = Mathf.Min(low, v);
                high = Mathf.Max(high, v);
            }

            Assert.AreEqual(0.22f, high, 0.01f, "Amber peaks at its strength.");
            Assert.Greater(low, 0.1f, "Amber breathes but never goes out.");

            float onBeat = HealthBar.VignetteFor(HealthBar.Stage.Critical, 0f, 0f, 0.22f, 0.55f, 0.9f, 0.45f, 4f);
            float later = HealthBar.VignetteFor(HealthBar.Stage.Critical, 0f, 0.9f, 0.22f, 0.55f, 0.9f, 0.45f, 4f);
            Assert.AreEqual(1f, onBeat, 0.001f);
            Assert.AreEqual(0.55f, later, 0.02f, "Settled by the next beat.");
            Assert.Greater(later, 2f * high, "Crimson well over twice amber.");
        }

        /// <summary>
        /// Block 4, in the scene: the vignette exists, the hull drives it, and
        /// the hull bar no longer pulses at amber.
        /// </summary>
        [Test]
        public void GameScene_HasTheLowHullVignette()
        {
            string scene = System.IO.File.ReadAllText("Assets/Scenes/Game.unity").Replace("\r\n", "\n");
            StringAssert.Contains("m_Name: Low Hull Vignette\n", scene);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(Materials + "/LowHullVignette.mat"));
            StringAssert.DoesNotContain("vignette: {fileID: 0}", scene, "The hull bar is not wired to the vignette.");
        }

        /// <summary>
        /// Block 5: a hit lights the screen edge it came from; one from behind
        /// the camera lights the bottom; one with no direction lights nothing.
        /// </summary>
        [Test]
        public void HitFlare_PicksTheEdgeTheHitCameFrom()
        {
            Vector2 screen = new Vector2(2560f, 1440f);
            Vector3 ship = new Vector3(1280f, 720f, 20f);

            var right = HitFlare.EdgeFor(ship, new Vector3(2000f, 760f, 20f), screen);
            Assert.AreEqual(HitFlare.Side.Right, right.Value.side);
            Assert.Greater(right.Value.along, 0.5f, "A little above the ship: above the middle of the edge.");

            Assert.AreEqual(HitFlare.Side.Left, HitFlare.EdgeFor(ship, new Vector3(100f, 700f, 20f), screen).Value.side);
            Assert.AreEqual(HitFlare.Side.Top, HitFlare.EdgeFor(ship, new Vector3(1300f, 1400f, 20f), screen).Value.side);
            Assert.AreEqual(HitFlare.Side.Bottom, HitFlare.EdgeFor(ship, new Vector3(1290f, 100f, 20f), screen).Value.side);

            var behind = HitFlare.EdgeFor(ship, new Vector3(2400f, 1400f, -5f), screen);
            Assert.AreEqual(HitFlare.Side.Bottom, behind.Value.side, "Behind the camera maps to the bottom edge.");

            Assert.IsNull(HitFlare.EdgeFor(ship, ship, screen));
        }

        /// <summary>
        /// Block 6: up while there is a reason and for the hold after, then
        /// down to rest; ALWAYS ON never rests below 85%.
        /// </summary>
        [Test]
        public void ContextFade_HoldsThenRests_AndAlwaysOnKeepsTheFloor()
        {
            Assert.AreEqual(1f, HudContextFade.TargetFor(true, 0f, 4f, 0.65f));
            Assert.AreEqual(1f, HudContextFade.TargetFor(false, 3.9f, 4f, 0.65f), "Hull holds 4 s.");
            Assert.AreEqual(0.65f, HudContextFade.TargetFor(false, 4.1f, 4f, 0.65f), "Then dims, never hides.");

            Assert.AreEqual(0.3f, HudSettings.Rest(0.3f, HudSettings.Contextual.Smart));
            Assert.AreEqual(0.85f, HudSettings.Rest(0.3f, HudSettings.Contextual.AlwaysOn));
            Assert.AreEqual(0.9f, HudSettings.Rest(0.9f, HudSettings.Contextual.AlwaysOn));
        }

        /// <summary>Block 6, in the scene: every faded element is wired.</summary>
        [Test]
        public void GameScene_FadesTheIdleHud()
        {
            string scene = System.IO.File.ReadAllText("Assets/Scenes/Game.unity");
            int fades = System.Text.RegularExpressions.Regex.Matches(scene, "awakeAtOrAbove:").Count;
            Assert.AreEqual(5, fades, "Expected fades on XP, hull, dash, deflector and Slow Mo.");
            StringAssert.Contains("rest: 0.65", scene, "The hull dims to 65%, not lower.");
        }

        /// <summary>
        /// Block 2: the timer's last ten seconds turn the boss bar's crimson in
        /// the scene, not only in the script's default.
        /// </summary>
        [Test]
        public void TimerAlert_IsTheBossCrimsonInTheScene()
        {
            string scene = System.IO.File.ReadAllText("Assets/Scenes/Game.unity");
            StringAssert.DoesNotContain("alertColour: {r: 1, g: 0.55, b: 0.25", scene,
                "A bar in Game.unity still alerts in the old orange.");
            StringAssert.Contains("alertColour: {r: 1, g: 0.231, b: 0.306", scene);
        }

        /// <summary>
        /// Block 7: HUD SCALE runs 70-130% and HUD OPACITY 40-100%, both in
        /// steps of 5, both defaulting to 100%.
        /// </summary>
        [Test]
        public void HudSliders_CoverTheirRangesInStepsOfFive()
        {
            Assert.AreEqual(12, HudOptionControl.LastPosition(HudSliderKind.Scale));
            Assert.AreEqual(70, HudOptionControl.PercentAt(HudSliderKind.Scale, 0));
            Assert.AreEqual(130, HudOptionControl.PercentAt(HudSliderKind.Scale, 12));
            Assert.AreEqual(6, HudOptionControl.PositionOf(HudSliderKind.Scale, 100));

            Assert.AreEqual(12, HudOptionControl.LastPosition(HudSliderKind.Opacity));
            Assert.AreEqual(40, HudOptionControl.PercentAt(HudSliderKind.Opacity, 0));
            Assert.AreEqual(100, HudOptionControl.PercentAt(HudSliderKind.Opacity, 12));
            Assert.AreEqual(12, HudOptionControl.PositionOf(HudSliderKind.Opacity, 100));
            Assert.AreEqual(0, HudOptionControl.PositionOf(HudSliderKind.Opacity, 10), "Clamped to the range.");
        }

        /// <summary>Block 7: CONTEXTUAL HUD reads SMART or ALWAYS ON; the others still read On and Off.</summary>
        [Test]
        public void ContextualHud_ReadsSmartOrAlwaysOn()
        {
            Assert.AreEqual("Smart", ToggleOption.ValueText(ToggleOptionKind.HudContextual, false));
            Assert.AreEqual("Always On", ToggleOption.ValueText(ToggleOptionKind.HudContextual, true));
            Assert.AreEqual("On", ToggleOption.ValueText(ToggleOptionKind.Rumble, true));
            Assert.AreEqual("Off", ToggleOption.ValueText(ToggleOptionKind.ControlHints, false));
        }

        /// <summary>
        /// Block 7: the five-tab strip and its two key hints fit the 1400-wide
        /// options panel; four tabs keep the old spacing.
        /// </summary>
        [Test]
        public void OptionsStrip_FitsFiveTabsInThePanel()
        {
            System.Type factory = System.Type.GetType(
                "SurvivalChaos.EditorTools.HoloUiFactory, SurvivalChaos.UI.Editor");
            Assert.IsNotNull(factory, "HoloUiFactory not found in SurvivalChaos.UI.Editor.");
            float Call(string method, params object[] args) =>
                (float)factory.GetMethod(method).Invoke(null, args);

            Assert.AreEqual(-420f, Call("OptionTabX", 0, 4), 0.01f);
            Assert.AreEqual(448f, Call("OptionTabX", 4, 5), 0.01f);
            Assert.LessOrEqual(Call("OptionTabHintX", 5) + 45f, 700f);
            CollectionAssert.AreEqual(new[] { "Audio", "Display", "Graphics", "Controls", "HUD" },
                (string[])factory.GetField("OptionTabNames").GetValue(null));
        }

        /// <summary>
        /// Block 7, in both scenes: the HUD tab, its button on all five strips,
        /// and its three rows.
        /// </summary>
        [TestCase("Assets/Scenes/Game.unity")]
        [TestCase("Assets/Scenes/Menu.unity")]
        public void Scenes_HaveTheHudOptionsTab(string path)
        {
            string scene = System.IO.File.ReadAllText(path);
            int Count(string name) =>
                System.Text.RegularExpressions.Regex.Matches(scene, "m_Name: " + name + "\r?\n").Count;

            Assert.AreEqual(1, Count("HUD Screen"));
            Assert.AreEqual(5, Count("HUD Tab"), "Every tab's strip needs the HUD button.");
            Assert.AreEqual(1, Count("HUD Scale Bar"));
            Assert.AreEqual(1, Count("HUD Opacity Bar"));
            Assert.AreEqual(1, Count("Contextual HUD Row"));
        }
    
        /// <summary>Block 8: the summary's chips and stats strip.</summary>
        [Test]
        public void RunSummary_WritesChipsAndAStatsStrip()
        {
            string chip = RunSummary.Chip("ATTACK SPEED", 2);
            StringAssert.StartsWith("<nobr>", chip, "A chip must not break across lines.");
            StringAssert.Contains("<color=#FFD140>X2</color>", chip, "Counts are gold.");
            StringAssert.Contains("<mark=", chip);

            string stats = RunSummary.StatsLine("1:22", 6, 30, 706);
            foreach (string part in new[] { "SURVIVED", "1:22", "LEVEL", "DESTROYED", "30", "EXPERIENCE", "706" })
            {
                StringAssert.Contains(part, stats);
            }

            StringAssert.Contains("#FFB830", RunSummary.DebugPill, "The debug notice is the amber pill.");
        }

        /// <summary>Block 8: offers split into a name and a gold tier.</summary>
        [Test]
        public void OffersCard_SplitsTheTierOffTheName()
        {
            OffersCard.Split("Attack Speed III", out string name, out string tier);
            Assert.AreEqual("Attack Speed", name);
            Assert.AreEqual("III", tier);

            OffersCard.Split("<b>Move Speed</b>\nII", out name, out tier);
            Assert.AreEqual("Move Speed", name);
            Assert.AreEqual("II", tier);

            OffersCard.Split("Magnet", out name, out tier);
            Assert.AreEqual("Magnet", name);
            Assert.AreEqual(string.Empty, tier);

            StringAssert.Contains("<color=#FFD140>III</color>", OffersCard.Compose(new[] { "Attack Speed III" }));
        }

        /// <summary>Block 8: BEST is a gold tag; an empty line stays empty.</summary>
        [Test]
        public void TitleBests_StartWithAGoldTag()
        {
            StringAssert.StartsWith("<color=#FFD140>BEST</color>", MenuFootnote.TagBest("BEST   SURVIVED 1:04"));
            StringAssert.EndsWith("SURVIVED 1:04", MenuFootnote.TagBest("BEST   SURVIVED 1:04"));
            Assert.AreEqual(string.Empty, MenuFootnote.TagBest(string.Empty));
        }

        /// <summary>Block 8: the run HUD shows with no menu open and hides behind any.</summary>
        [Test]
        public void RunHud_HidesBehindAnyMenu()
        {
            Assert.AreEqual(1f, HideBehindMenus.TargetFor(0));
            Assert.AreEqual(0f, HideBehindMenus.TargetFor(1));
            Assert.AreEqual(0f, HideBehindMenus.TargetFor(2));
        }

        /// <summary>Block 8, in the scenes: what the tool lays out is there.</summary>
        [Test]
        public void Scenes_HaveTheRedesignedMenus()
        {
            string game = System.IO.File.ReadAllText("Assets/Scenes/Game.unity");
            string menu = System.IO.File.ReadAllText("Assets/Scenes/Menu.unity");
            int Count(string scene, string name) =>
                System.Text.RegularExpressions.Regex.Matches(scene, "m_Name: " + name + "\r?\n").Count;

            Assert.AreEqual(5, Count(game, "Control Legend"), "A legend under Back on every pause options tab.");
            Assert.AreEqual(3, Count(game, "Summary Rule"), "Pause, Victory and Death each have the hairline.");
            Assert.AreEqual(1, Count(game, "Offers"), "Pause has the offers card.");
            Assert.AreEqual(6, Count(menu, "Control Legend"), "Five options tabs and the title screen.");
            Assert.AreEqual(1, Count(menu, "Menu Rail"));
            Assert.AreEqual(1, Count(menu, "Best Strip"));

            string death = AssetDatabase.AssetPathToGUID("Assets/UI/Materials/DeathPanel.mat");
            Assert.IsNotEmpty(death);
            StringAssert.Contains(death, game, "Death's card uses the crimson panel.");
        }
    }
}
