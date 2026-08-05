using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Builds the holographic HUD and repoints the existing gameplay scripts at it.
    ///
    /// The interface is generated rather than assembled by hand for the same
    /// reason the sky is: it is defined by numbers, and numbers belong in a file
    /// that can be re-run. Nudging thirty rect transforms by hand and hoping they
    /// still line up at another aspect ratio is how the old one ended up as it
    /// was.
    ///
    /// This adds a new HUD next to the old one rather than deleting anything. The
    /// old canvas is left alone so the two can be compared, and so a mistake here
    /// costs nothing. Every step is registered with Undo, so one Ctrl+Z reverts
    /// the whole build.
    ///
    /// Deliberately in its own assembly: the other editor tools reference HDRP and
    /// cannot go back to the URP branch, whereas nothing here is pipeline specific.
    /// </summary>
    public static class HoloUiBuilder
    {
        // The interface is authored against this. Scale With Screen Size then
        // handles every other resolution, which is exactly what the old canvas -
        // set to Constant Pixel Size - was not doing.
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private const string MaterialFolder = "Assets/UI/Materials";
        private const string BossBarTag = "bossHpBar";

        // Palette. Cyan-white line work over a cold dark fill: high contrast
        // against the orange lava that dominates the scene, so the HUD never
        // competes with the thing the player is looking at.
        private static readonly Color EdgeCyan = new Color(0.55f, 0.95f, 1.00f, 1.00f);
        private static readonly Color FillCyan = new Color(0.30f, 0.85f, 1.00f, 1.00f);
        private static readonly Color PanelFill = new Color(0.03f, 0.14f, 0.20f, 0.42f);
        private static readonly Color TrackDark = new Color(0.03f, 0.12f, 0.17f, 0.55f);
        private static readonly Color LossRed = new Color(1.00f, 0.32f, 0.34f, 1.00f);
        private static readonly Color HealthGreen = new Color(0.40f, 1.00f, 0.80f, 1.00f);
        private static readonly Color BossOrange = new Color(1.00f, 0.55f, 0.25f, 1.00f);

        [MenuItem("Survival Chaos/UI/Rebuild HUD", priority = 20)]
        public static void RebuildHud()
        {
            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog(
                    "No canvas",
                    "Open the Game scene first - this needs a Canvas to build into.",
                    "OK");
                return;
            }

            ConfigureCanvas(canvas);

            Material barMaterial = EnsureMaterial("HoloBar", "Survival Chaos/Holo Bar");
            Material panelMaterial = EnsureMaterial("HoloPanel", "Survival Chaos/Holo Panel");

            GameObject root = ReplaceRoot(canvas.transform, "HUD (Holo)");

            BuildHealth(root.transform, barMaterial);
            BuildExperience(root.transform, barMaterial);
            BuildTimer(root.transform, barMaterial);
            BuildBossBar(root.transform, barMaterial);
            BuildLevelUpBanner(root.transform, panelMaterial);

            int wired = Rewire(root.transform);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = root;

            Debug.Log(
                "Holo HUD built under '" + root.name + "'. " + wired + " script references repointed. " +
                "The old UI is untouched - compare them, then delete the old objects when you are happy. " +
                "Ctrl+Z reverts the whole build.", root);
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

        /// <summary>
        /// The two settings that caused the old interface to look low resolution.
        /// </summary>
        private static void ConfigureCanvas(Canvas canvas)
        {
            Undo.RecordObject(canvas, "Configure canvas");

            // The shaders read each element's pixel size out of this channel. It
            // is off by default, and without it every holo element draws at a
            // fallback size.
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            EditorUtility.SetDirty(canvas);

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            }

            Undo.RecordObject(scaler, "Configure canvas scaler");

            // Was Constant Pixel Size, which ignores the reference resolution
            // entirely and pins the interface to physical pixels - so it shrank
            // on every display better than the one it was authored on.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            // Match on both axes equally, so an ultrawide monitor loses nothing
            // off the sides and a 4:3 one loses nothing off the top.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(scaler);
        }

        private static Material EnsureMaterial(string name, string shaderName)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("Shader '" + shaderName + "' not found. Has it finished importing?");
                return null;
            }

            if (!AssetDatabase.IsValidFolder("Assets/UI"))
            {
                AssetDatabase.CreateFolder("Assets", "UI");
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/UI", "Materials");
            }

            Material material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Clears any previous build so the tool can be run repeatedly while the
        /// layout is being tuned, without stacking copies.
        /// </summary>
        private static GameObject ReplaceRoot(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            GameObject root = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Build holo HUD");
            RectTransform rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            return root;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform CreateRect(Transform parent, string name,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Build holo HUD");

            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>
        /// A bar: one image drawn entirely by the shader, plus a Slider that
        /// exists only so the gameplay scripts have something to set. The slider
        /// draws nothing itself - no fill rect, no handle - which is why the bar
        /// can be a single quad rather than the four nested objects it was.
        /// </summary>
        private static Slider CreateBar(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, Material material, Color fill, float segments,
            float lowThreshold)
        {
            RectTransform rect = CreateRect(parent, name, anchor, pivot, position, size);

            Image image = Undo.AddComponent<Image>(rect.gameObject);
            image.material = material;
            image.raycastTarget = false;
            image.color = Color.white;

            Undo.AddComponent<HoloRectData>(rect.gameObject);

            Slider slider = Undo.AddComponent<Slider>(rect.gameObject);
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            HoloBar bar = Undo.AddComponent<HoloBar>(rect.gameObject);
            ApplyBarSettings(bar, slider, lowThreshold);

            // Per-bar colour lives on the element, not the shared material, so
            // one material asset serves every bar.
            TintBar(image, material, fill, segments);
            return slider;
        }

        private static void ApplyBarSettings(HoloBar bar, Slider source, float lowThreshold)
        {
            SerializedObject so = new SerializedObject(bar);
            so.FindProperty("source").objectReferenceValue = source;
            so.FindProperty("lowThreshold").floatValue = lowThreshold;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Gives one element its own copy of the bar material so its colours and
        /// segment count can differ from the others.
        /// </summary>
        private static void TintBar(Image image, Material source, Color fill, float segments)
        {
            Material variant = new Material(source) { name = image.name + " Material" };
            variant.SetColor("_FillColor", fill);
            variant.SetColor("_EdgeColor", EdgeCyan);
            variant.SetColor("_TrackColor", TrackDark);
            variant.SetColor("_GhostColor", LossRed);
            variant.SetFloat("_Segments", segments);

            image.material = SaveMaterial(variant, image.name.Replace(" ", string.Empty));
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchor,
            Vector2 pivot, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions align)
        {
            RectTransform rect = CreateRect(parent, name, anchor, pivot, position, size);

            TextMeshProUGUI text = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = EdgeCyan;
            text.raycastTarget = false;

            // Wide tracking and small caps is most of what makes plain type read
            // as a technical readout. The bundled font is Liberation Sans, which
            // is doing a lot of work here - a squarer face would lift it further.
            text.characterSpacing = 8f;
            text.fontStyle = FontStyles.UpperCase;
            return text;
        }

        private static void BuildHealth(Transform parent, Material barMaterial)
        {
            CreateText(parent, "Health Label", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(52f, 92f), new Vector2(300f, 24f), 18f, TextAlignmentOptions.Left)
                .text = "Hull";

            CreateBar(parent, "Health Bar", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(48f, 48f), new Vector2(440f, 38f), barMaterial,
                HealthGreen, 10f, 0.3f);
        }

        private static void BuildExperience(Transform parent, Material barMaterial)
        {
            // Experience is read as an Image fill by ExpBar, not as a Slider, so
            // this one has no Slider at all and HoloBar reads fillAmount instead.
            RectTransform rect = CreateRect(parent, "XP Bar", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(48f, 128f), new Vector2(440f, 14f));

            Image image = Undo.AddComponent<Image>(rect.gameObject);
            image.material = barMaterial;
            image.raycastTarget = false;
            // Left as Simple on purpose. Filled would shorten the quad itself and
            // the shader would then draw a whole bar inside that shortened piece.
            // ExpBar can still set fillAmount; it is stored and read from here.
            image.type = Image.Type.Simple;
            image.fillAmount = 0f;

            Undo.AddComponent<HoloRectData>(rect.gameObject);

            HoloBar bar = Undo.AddComponent<HoloBar>(rect.gameObject);
            // No source: falls back to this element's own fillAmount. No low
            // pulse either - running out of experience is not an emergency.
            ApplyBarSettings(bar, null, 0f);
            TintBar(image, barMaterial, FillCyan, 0f);

            CreateText(parent, "Level Text", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(52f, 146f), new Vector2(300f, 24f), 16f, TextAlignmentOptions.Left)
                .text = "Level 1";
        }

        private static void BuildTimer(Transform parent, Material barMaterial)
        {
            CreateBar(parent, "Timer Bar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -44f), new Vector2(860f, 22f), barMaterial,
                FillCyan, 20f, 0f);

            CreateText(parent, "Timer Label", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(600f, 22f), 16f, TextAlignmentOptions.Center)
                .text = "Incoming";
        }

        private static void BuildBossBar(Transform parent, Material barMaterial)
        {
            Slider slider = CreateBar(parent, "Boss Bar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -44f), new Vector2(1200f, 44f), barMaterial,
                BossOrange, 24f, 0.2f);

            // BossEmitter finds this by tag at runtime, so the tag matters more
            // than the name does.
            slider.gameObject.tag = BossBarTag;

            CreateText(slider.transform, "Boss Label", new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(600f, 24f), 20f, TextAlignmentOptions.Center)
                .text = "Leviathan";

            // Hidden until the timer runs out; BossHpBar switches it on.
            slider.gameObject.SetActive(false);
        }

        private static void BuildLevelUpBanner(Transform parent, Material panelMaterial)
        {
            RectTransform rect = CreateRect(parent, "Level Up Banner", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(620f, 130f));

            Image panel = Undo.AddComponent<Image>(rect.gameObject);
            panel.material = panelMaterial;
            panel.raycastTarget = false;
            panel.color = Color.white;
            Undo.AddComponent<HoloRectData>(rect.gameObject);

            Material variant = new Material(panelMaterial) { name = "LevelUpPanel" };
            variant.SetColor("_FillColor", PanelFill);
            variant.SetColor("_EdgeColor", EdgeCyan);
            panel.material = SaveMaterial(variant, "LevelUpPanel");

            CreateText(rect, "Level Up Text", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(560f, 100f), 30f, TextAlignmentOptions.Center)
                .text = "Level Up";

            rect.gameObject.SetActive(false);
        }

        /// <summary>
        /// Points the existing gameplay scripts at the new elements. They are not
        /// modified, only reconnected - the health bar still sets the same slider
        /// value it always did.
        /// </summary>
        private static int Rewire(Transform root)
        {
            int wired = 0;

            Slider health = Find<Slider>(root, "Health Bar");
            Slider timer = Find<Slider>(root, "Timer Bar");
            Slider boss = Find<Slider>(root, "Boss Bar");
            Image xp = Find<Image>(root, "XP Bar");
            TextMeshProUGUI levelText = Find<TextMeshProUGUI>(root, "Level Text");
            TextMeshProUGUI levelUpText = Find<TextMeshProUGUI>(root, "Level Up Text");

            foreach (HealthBar target in Object.FindObjectsByType<HealthBar>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(target, "Rewire HUD");
                target.slider = health;
                EditorUtility.SetDirty(target);
                wired++;
            }

            foreach (ExpBar target in Object.FindObjectsByType<ExpBar>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(target, "Rewire HUD");
                target.xpBar = xp;
                target.expText = levelText;
                EditorUtility.SetDirty(target);
                wired++;
            }

            foreach (Timer target in Object.FindObjectsByType<Timer>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(target, "Rewire HUD");
                target.timerSlider = timer;
                target.timerBar = timer != null ? timer.gameObject : null;
                EditorUtility.SetDirty(target);
                wired++;
            }

            foreach (BossHpBar target in Object.FindObjectsByType<BossHpBar>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(target, "Rewire HUD");
                target.hpSlider = boss;
                target.HpBar = boss != null ? boss.gameObject : null;
                EditorUtility.SetDirty(target);
                wired++;
            }

            foreach (SkillSelect target in Object.FindObjectsByType<SkillSelect>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(target, "Rewire HUD");
                target.skillText = levelUpText;
                target.skillTextObject = levelUpText != null
                    ? levelUpText.transform.parent.gameObject
                    : null;
                EditorUtility.SetDirty(target);
                wired++;
            }

            return wired;
        }

        /// <summary>
        /// Searches the whole subtree, not just direct children - some elements
        /// are nested, and Transform.Find would quietly return null for those.
        /// </summary>
        private static T Find<T>(Transform root, string name) where T : Component
        {
            foreach (T candidate in root.GetComponentsInChildren<T>(includeInactive: true))
            {
                if (candidate.gameObject.name == name)
                {
                    return candidate;
                }
            }

            Debug.LogWarning("Holo HUD is missing '" + name + "', so something will be left unwired.");
            return null;
        }

        /// <summary>
        /// Writes a material to a fixed path, replacing what was there. Using a
        /// unique path instead would leave a trail of orphaned materials every
        /// time the layout is re-run.
        /// </summary>
        private static Material SaveMaterial(Material material, string fileName)
        {
            string path = MaterialFolder + "/" + fileName + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                existing.shader = material.shader;
                EditorUtility.CopySerialized(material, existing);
                Object.DestroyImmediate(material);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
