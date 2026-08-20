using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Builds the holographic HUD, repoints the gameplay scripts at it, and
    /// retires the old one.
    ///
    /// The interface is generated rather than assembled by hand for the same
    /// reason the sky is: it is defined by numbers, and numbers belong in a file
    /// that can be re-run. Nudging thirty rect transforms by hand and hoping they
    /// still line up at another aspect ratio is how the old one ended up as it was.
    ///
    /// Every step is registered with Undo, so one Ctrl+Z reverts the whole build.
    ///
    /// Deliberately in its own assembly: the other editor tools reference HDRP and
    /// cannot go back to the URP branch, whereas nothing here is pipeline specific.
    /// </summary>
    public static class HoloUiBuilder
    {
        private const string BossBarTag = "bossHpBar";
        private const string RootName = "HUD (Holo)";

        /// <summary>
        /// The old HUD objects. These are stripped of their visuals rather than
        /// deleted, because several of them carry gameplay components - HealthBar
        /// sits on the object called "HealthBar", ExpBar on "XpBar", BossHpBar on
        /// "bossHpBar". Deleting those would take the components with them and
        /// leave Player's references pointing at nothing.
        /// </summary>
        private static readonly string[] OldHudObjects =
        {
            "HealthBar", "XpBar", "timeBar", "bossHpBar", "LevelUpText"
        };

        [MenuItem("Survival Chaos/UI/Rebuild HUD", priority = 20)]
        public static void RebuildHud()
        {
            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("No canvas",
                    "Open the Game scene first - this needs a Canvas to build into.", "OK");
                return;
            }

            ConfigureCanvas(canvas);

            Material bar = HoloUiFactory.EnsureBaseMaterial("HoloBar", "Survival Chaos/Holo Bar");
            Material panel = HoloUiFactory.EnsureBaseMaterial("HoloPanel", "Survival Chaos/Holo Panel");
            if (bar == null || panel == null)
            {
                return;
            }

            GameObject root = HoloUiFactory.ReplaceRoot(canvas.transform, RootName);

            BuildHealth(root.transform, bar);
            BuildExperience(root.transform, bar);
            BuildTimer(root.transform, bar);
            BuildBossBar(root.transform, bar);
            BuildLevelUpBanner(root.transform, panel);

            // Order matters: the components have to exist on the new objects
            // before anything is wired to them, and everything has to be wired
            // before the old objects are destroyed.
            RelocateComponents(root.transform);
            ConsolidateTimer(root.transform, canvas.transform);
            int wired = Rewire(root.transform);
            RepointReferences(root.transform);
            int removed = RetireOldHud(canvas.transform);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = root;

            Debug.Log(
                "Holo HUD built. " + wired + " references repointed, " + removed +
                " old HUD objects removed. Ctrl+Z reverts everything.", root);
        }

        private static Canvas FindCanvas()
        {
            foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (candidate.isRootCanvas)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>The two settings that made the old interface look low resolution.</summary>
        private static void ConfigureCanvas(Canvas canvas)
        {
            Undo.RecordObject(canvas, "Configure canvas");

            // The shaders read each element's pixel size from this channel. It is
            // off by default, and without it every holo element falls back to a
            // fixed size and draws wrong.
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            EditorUtility.SetDirty(canvas);

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            }

            Undo.RecordObject(scaler, "Configure canvas scaler");

            // Was Constant Pixel Size, which ignores the reference resolution
            // entirely and pins the interface to physical pixels - so it shrank on
            // every display better than the one it was authored on.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HoloUiFactory.ReferenceResolution;
            // Match both axes equally: an ultrawide loses nothing off the sides,
            // a 4:3 loses nothing off the top.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(scaler);
        }

        private static void BuildHealth(Transform parent, Material bar)
        {
            HoloUiFactory.CreateText(parent, "Health Label", Vector2.zero, Vector2.zero,
                new Vector2(52f, 92f), new Vector2(300f, 24f), 18f, TextAlignmentOptions.Left)
                .text = "Hull";

            HoloUiFactory.CreateBar(parent, "Health Bar", Vector2.zero, Vector2.zero,
                new Vector2(48f, 48f), new Vector2(440f, 38f), bar,
                HoloUiFactory.Health, 10f, 0.3f);
        }

        private static void BuildExperience(Transform parent, Material bar)
        {
            // ExpBar reads an Image's fillAmount rather than a Slider, so this one
            // has no Slider and HoloBar falls back to reading fillAmount.
            Image image = HoloUiFactory.CreateBarImage(parent, "XP Bar", Vector2.zero, Vector2.zero,
                new Vector2(48f, 128f), new Vector2(440f, 14f), bar, HoloUiFactory.Accent, 0f);

            // Left as Simple deliberately. Filled would shorten the quad itself,
            // and the shader would then draw a whole bar inside that short piece.
            // ExpBar can still set fillAmount - it is stored, and read from here.
            image.type = Image.Type.Simple;
            image.fillAmount = 0f;

            HoloBar holo = Undo.AddComponent<HoloBar>(image.gameObject);
            // No source, and no low pulse: running out of experience is not an
            // emergency, so it should not throb.
            HoloUiFactory.ConfigureBar(holo, null, 0f);

            HoloUiFactory.CreateText(parent, "Level Text", Vector2.zero, Vector2.zero,
                new Vector2(52f, 146f), new Vector2(300f, 24f), 16f, TextAlignmentOptions.Left)
                .text = "Level 1";
        }

        private static void BuildTimer(Transform parent, Material bar)
        {
            HoloUiFactory.CreateBar(parent, "Timer Bar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -44f), new Vector2(860f, 22f), bar, HoloUiFactory.Accent, 20f, 0f);

            HoloUiFactory.CreateText(parent, "Timer Label", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(600f, 22f), 16f, TextAlignmentOptions.Center)
                .text = "Incoming";
        }

        private static void BuildBossBar(Transform parent, Material bar)
        {
            Slider slider = HoloUiFactory.CreateBar(parent, "Boss Bar", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(1200f, 44f), bar,
                HoloUiFactory.Boss, 24f, 0.2f);

            // BossEmitter finds this by tag at runtime, so the tag matters more
            // than the name.
            slider.gameObject.tag = BossBarTag;

            HoloUiFactory.CreateText(slider.transform, "Boss Label", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(600f, 24f), 20f,
                TextAlignmentOptions.Center).text = "Leviathan";

            // Hidden until the timer runs out; BossHpBar switches it on.
            slider.gameObject.SetActive(false);
        }

        private static void BuildLevelUpBanner(Transform parent, Material panel)
        {
            Image image = HoloUiFactory.CreatePanel(parent, "Level Up Banner",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f),
                new Vector2(620f, 130f), panel, HoloUiFactory.PanelFill, "LevelUpPanel");

            HoloUiFactory.CreateText(image.transform, "Level Up Text", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 100f), 30f,
                TextAlignmentOptions.Center).text = "Level Up";

            image.gameObject.SetActive(false);
        }

        /// <summary>
        /// Points the gameplay scripts at the new elements. They are not modified,
        /// only reconnected - HealthBar still sets the same slider value it always
        /// did. Inactive objects are included, because the boss bar and the menus
        /// start switched off.
        /// </summary>
        private static int Rewire(Transform root)
        {
            int wired = 0;

            Slider health = HoloUiFactory.Find<Slider>(root, "Health Bar");
            Slider boss = HoloUiFactory.Find<Slider>(root, "Boss Bar");
            Image xp = HoloUiFactory.Find<Image>(root, "XP Bar");
            TextMeshProUGUI levelText = HoloUiFactory.Find<TextMeshProUGUI>(root, "Level Text");
            TextMeshProUGUI levelUpText = HoloUiFactory.Find<TextMeshProUGUI>(root, "Level Up Text");

            foreach (HealthBar target in Object.FindObjectsByType<HealthBar>(FindObjectsInactive.Include))
            {
                HoloUiFactory.Assign(target, "slider", health);
                wired++;
            }

            foreach (ExpBar target in Object.FindObjectsByType<ExpBar>(FindObjectsInactive.Include))
            {
                HoloUiFactory.Assign(target, "xpBar", xp);
                HoloUiFactory.Assign(target, "expText", levelText);
                wired++;
            }

            // Timer is handled by ConsolidateTimer, which has to pick between two
            // differently configured copies rather than just wiring both.

            foreach (BossHpBar target in Object.FindObjectsByType<BossHpBar>(FindObjectsInactive.Include))
            {
                // Only the panel. BossHpBar's job is to reveal it; the slider
                // itself is found by tag from BossEmitter, which is what actually
                // drives the value - the second reference here was written and
                // never read.
                HoloUiFactory.Assign(target, "HpBar", boss != null ? boss.gameObject : null);
                wired++;
            }

            foreach (SkillSelect target in Object.FindObjectsByType<SkillSelect>(FindObjectsInactive.Include))
            {
                HoloUiFactory.Assign(target, "skillText", levelUpText);
                HoloUiFactory.Assign(target, "skillTextObject", levelUpText != null
                    ? levelUpText.transform.parent.gameObject
                    : null);
                wired++;
            }

            return wired;
        }

        /// <summary>
        /// Puts a copy of each HUD script on the new element it now describes.
        ///
        /// These components sat on the old bars - HealthBar on the object called
        /// "HealthBar", ExpBar on "XpBar", BossHpBar on "bossHpBar" - so the old
        /// objects could not be deleted without taking them along. Recreating them
        /// here is what makes the old HUD genuinely disposable.
        /// </summary>
        private static void RelocateComponents(Transform root)
        {
            Player player = Object.FindAnyObjectByType<Player>(FindObjectsInactive.Include);

            Slider health = HoloUiFactory.Find<Slider>(root, "Health Bar");
            if (health != null && health.GetComponent<HealthBar>() == null)
            {
                Undo.AddComponent<HealthBar>(health.gameObject);
            }

            Image xp = HoloUiFactory.Find<Image>(root, "XP Bar");
            if (xp != null && xp.GetComponent<ExpBar>() == null)
            {
                ExpBar bar = Undo.AddComponent<ExpBar>(xp.gameObject);
                HoloUiFactory.Assign(bar, "player", player);
            }

            Slider boss = HoloUiFactory.Find<Slider>(root, "Boss Bar");
            if (boss != null && boss.GetComponent<BossHpBar>() == null)
            {
                Undo.AddComponent<BossHpBar>(boss.gameObject);
            }
        }

        /// <summary>
        /// Reduces the scene to a single Timer, counting down to the moment the
        /// boss actually spawns.
        ///
        /// There were two, both active and both driving the same slider: one with
        /// gameTime 300 and a wired boss bar, one with 60 and a null one, so from
        /// sixty seconds in it called showHpBar() on nothing. They also disagreed
        /// about the slider's maxValue, making the bar's scale depend on start
        /// order.
        ///
        /// The length is taken from the wave data rather than from either of them.
        /// The countdown and the boss's spawn time were two independent numbers
        /// that had to agree and had already drifted apart; deriving one from the
        /// other means they cannot drift again.
        /// </summary>
        private static void ConsolidateTimer(Transform root, Transform canvas)
        {
            Timer[] existing = Object.FindObjectsByType<Timer>(FindObjectsInactive.Include);

            float gameTime = ResolveBossSpawnTime();
            string source = "the boss stream in the wave asset";

            if (gameTime <= 0f)
            {
                // No wave data to read. Fall back to the longest existing
                // countdown, which at least errs toward the boss arriving late
                // rather than immediately.
                foreach (Timer candidate in existing)
                {
                    gameTime = Mathf.Max(gameTime, ReadGameTime(candidate));
                }

                gameTime = gameTime > 0f ? gameTime : 300f;
                source = "the longest existing Timer (no boss stream found)";
            }

            GameObject host = PickTimerHost(existing, canvas);

            foreach (Timer old in existing)
            {
                Undo.DestroyObjectImmediate(old);
            }

            Timer timer = Undo.AddComponent<Timer>(host);
            Slider bar = HoloUiFactory.Find<Slider>(root, "Timer Bar");
            HoloUiFactory.Assign(timer, "timerSlider", bar);
            HoloUiFactory.Assign(timer, "timerBar", bar != null ? bar.gameObject : null);
            // Specifically the new one. A scene-wide search could return the old
            // BossHpBar, which is about to be destroyed - leaving the countdown
            // pointing at nothing exactly as the boss arrives.
            HoloUiFactory.Assign(timer, "bossHpBar", HoloUiFactory.Find<BossHpBar>(root, "Boss Bar"));

            SerializedObject so = new SerializedObject(timer);
            so.FindProperty("gameTime").floatValue = gameTime;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("Timer consolidated onto '" + host.name + "' with gameTime " + gameTime +
                      " (from " + source + "), replacing " + existing.Length + ".");
        }

        /// <summary>
        /// When the boss is scheduled to spawn, read from the wave asset.
        ///
        /// The boss stream is found by its prefab carrying a BossEmitter, not by
        /// its label or its position in the list - a renamed or reordered stream
        /// should not silently change how long the run lasts.
        /// </summary>
        private static float ResolveBossSpawnTime()
        {
            foreach (WaveDirector director in Object.FindObjectsByType<WaveDirector>(FindObjectsInactive.Include))
            {
                SerializedObject so = new SerializedObject(director);
                WaveDefinition wave = so.FindProperty("wave").objectReferenceValue as WaveDefinition;

                if (wave == null || wave.Streams == null)
                {
                    continue;
                }

                foreach (SpawnStream stream in wave.Streams)
                {
                    if (stream == null || stream.Prefab == null)
                    {
                        continue;
                    }

                    if (stream.Prefab.GetComponentInChildren<BossEmitter>(includeInactive: true) != null)
                    {
                        return stream.StartDelay;
                    }
                }
            }

            return -1f;
        }

        private static float ReadGameTime(Timer timer)
        {
            return new SerializedObject(timer).FindProperty("gameTime").floatValue;
        }

        /// <summary>
        /// Somewhere that is not part of the HUD, so the countdown keeps running
        /// when the timer bar hides itself.
        /// </summary>
        private static GameObject PickTimerHost(Timer[] existing, Transform canvas)
        {
            foreach (Timer candidate in existing)
            {
                if (!IsOldHud(candidate.gameObject))
                {
                    return candidate.gameObject;
                }
            }

            return canvas.gameObject;
        }

        private static bool IsOldHud(GameObject candidate)
        {
            foreach (string name in OldHudObjects)
            {
                if (candidate.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Repoints what referenced the relocated components. Player holds direct
        /// references to HealthBar and ExpBar, and those would be left pointing at
        /// objects about to be destroyed.
        /// </summary>
        private static void RepointReferences(Transform root)
        {
            HealthBar health = HoloUiFactory.Find<HealthBar>(root, "Health Bar");
            ExpBar experience = HoloUiFactory.Find<ExpBar>(root, "XP Bar");

            foreach (Player player in Object.FindObjectsByType<Player>(FindObjectsInactive.Include))
            {
                if (health != null)
                {
                    HoloUiFactory.Assign(player, "healthBar", health);
                }

                if (experience != null)
                {
                    HoloUiFactory.Assign(player, "expBar", experience);
                }
            }
        }

        /// <summary>
        /// Deletes the old HUD outright. Safe only because everything that lived
        /// on those objects has been recreated and everything pointing at them has
        /// been repointed - both of which happen before this runs.
        ///
        /// Removing the old boss bar also clears a duplicate "bossHpBar" tag,
        /// which BossEmitter resolves at runtime and would otherwise pick from at
        /// random.
        /// </summary>
        private static int RetireOldHud(Transform canvas)
        {
            int removed = 0;
            Transform newRoot = canvas.Find(RootName);

            foreach (string name in OldHudObjects)
            {
                Transform old = FindByName(canvas, name);

                // Never delete what was just built. The new names differ from the
                // old ones, so this should not trigger - but a rename later would
                // otherwise make this tool quietly delete its own output.
                if (old == null || (newRoot != null && old.IsChildOf(newRoot)))
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(old.gameObject);
                removed++;
            }

            return removed;
        }

        private static Transform FindByName(Transform root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (candidate.gameObject.name == name && candidate != root)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
