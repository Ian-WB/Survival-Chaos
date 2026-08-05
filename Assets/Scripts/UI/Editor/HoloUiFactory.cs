using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// The parts every holographic element is made from, shared by the HUD and
    /// the menu builders so the two cannot drift apart.
    ///
    /// A menu button and a health bar have to look like the same machine. Keeping
    /// the palette and the construction in one place is what guarantees that -
    /// the old interface looked assembled from three different projects because
    /// each screen was built by hand at a different time.
    /// </summary>
    public static class HoloUiFactory
    {
        public const string MaterialFolder = "Assets/UI/Materials";

        /// <summary>Authoring resolution. Scale With Screen Size covers the rest.</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // Cyan-white line work over a cold dark fill: maximum separation from the
        // orange lava that dominates the scene, so the interface never competes
        // with the thing being looked at.
        public static readonly Color Edge = new Color(0.55f, 0.95f, 1.00f, 1.00f);
        public static readonly Color Accent = new Color(0.30f, 0.85f, 1.00f, 1.00f);
        public static readonly Color PanelFill = new Color(0.03f, 0.14f, 0.20f, 0.42f);
        public static readonly Color TrackDark = new Color(0.03f, 0.12f, 0.17f, 0.55f);
        public static readonly Color Loss = new Color(1.00f, 0.32f, 0.34f, 1.00f);
        public static readonly Color Health = new Color(0.40f, 1.00f, 0.80f, 1.00f);
        public static readonly Color Boss = new Color(1.00f, 0.55f, 0.25f, 1.00f);
        public static readonly Color Scrim = new Color(0.01f, 0.03f, 0.05f, 0.82f);

        public static Material EnsureBaseMaterial(string name, string shaderName)
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

            EnsureFolder();
            Material material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/UI"))
            {
                AssetDatabase.CreateFolder("Assets", "UI");
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/UI", "Materials");
            }
        }

        /// <summary>
        /// Writes a material to a fixed path, replacing what is there. A unique
        /// path would leave a trail of orphans every time a layout is re-run.
        /// </summary>
        public static Material SaveMaterial(Material material, string fileName)
        {
            EnsureFolder();

            // Unity warns whenever a main asset's object name differs from its
            // filename, and the callers name these after the element they belong
            // to - "Health Bar" - while the file cannot carry the space.
            material.name = fileName;

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

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Removes a previous build of the same name so tools stay re-runnable.</summary>
        public static GameObject ReplaceRoot(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            GameObject root = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Build holo UI");
            RectTransform rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            return root;
        }

        public static RectTransform CreateRect(Transform parent, string name,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Build holo UI");

            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchor,
            Vector2 pivot, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions align)
        {
            RectTransform rect = CreateRect(parent, name, anchor, pivot, position, size);

            TextMeshProUGUI text = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Edge;
            text.raycastTarget = false;

            // Wide tracking and caps is most of what makes plain type read as a
            // technical readout. The bundled Liberation Sans is doing a lot of
            // work here; a squarer face would lift it further.
            text.characterSpacing = 8f;
            text.fontStyle = FontStyles.UpperCase;
            return text;
        }

        /// <summary>A framed panel drawn entirely by the panel shader.</summary>
        public static Image CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, Material panelMaterial, Color fill, string materialName)
        {
            RectTransform rect = CreateRect(parent, name, anchor, pivot, position, size);

            Image image = Undo.AddComponent<Image>(rect.gameObject);
            image.raycastTarget = false;
            image.color = Color.white;
            Undo.AddComponent<HoloRectData>(rect.gameObject);

            Material variant = new Material(panelMaterial) { name = materialName };
            variant.SetColor("_FillColor", fill);
            variant.SetColor("_EdgeColor", Edge);
            image.material = SaveMaterial(variant, materialName);
            return image;
        }

        /// <summary>
        /// A bar: one quad drawn by the shader, plus a Slider that exists only so
        /// the gameplay scripts have something to set. The slider draws nothing
        /// itself - no fill rect, no handle - which is why a bar is one object
        /// rather than the four nested ones it used to be.
        /// </summary>
        public static Slider CreateBar(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, Material barMaterial, Color fill, float segments,
            float lowThreshold)
        {
            Image image = CreateBarImage(parent, name, anchor, pivot, position, size,
                barMaterial, fill, segments);

            Slider slider = Undo.AddComponent<Slider>(image.gameObject);
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            HoloBar bar = Undo.AddComponent<HoloBar>(image.gameObject);
            ConfigureBar(bar, slider, lowThreshold);
            return slider;
        }

        public static Image CreateBarImage(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, Material barMaterial, Color fill, float segments)
        {
            RectTransform rect = CreateRect(parent, name, anchor, pivot, position, size);

            Image image = Undo.AddComponent<Image>(rect.gameObject);
            image.raycastTarget = false;
            image.color = Color.white;
            Undo.AddComponent<HoloRectData>(rect.gameObject);

            Material variant = new Material(barMaterial) { name = name };
            variant.SetColor("_FillColor", fill);
            variant.SetColor("_EdgeColor", Edge);
            variant.SetColor("_TrackColor", TrackDark);
            variant.SetColor("_GhostColor", Loss);
            variant.SetFloat("_Segments", segments);
            image.material = SaveMaterial(variant, name.Replace(" ", string.Empty));
            return image;
        }

        public static void ConfigureBar(HoloBar bar, Slider source, float lowThreshold)
        {
            SerializedObject so = new SerializedObject(bar);
            so.FindProperty("source").objectReferenceValue = source;
            so.FindProperty("lowThreshold").floatValue = lowThreshold;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A button: a holo panel that takes raycasts, with a label.
        ///
        /// Highlighting is done by HoloButtonHighlight driving the shader, not by
        /// Unity's colour tint. A tint fades the whole image at once - fill
        /// included - which flattens the frame instead of sharpening it, and it
        /// cannot reach the glow, the brackets or the sweep at all.
        /// </summary>
        public static Button CreateButton(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, Material panelMaterial, string label, float fontSize)
        {
            Image image = CreatePanel(parent, name, anchor, pivot, position, size,
                panelMaterial, PanelFill, "HoloButton");
            image.raycastTarget = true;

            Button button = Undo.AddComponent<Button>(image.gameObject);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            RectTransform rect = (RectTransform)image.transform;
            TextMeshProUGUI text = CreateText(rect, "Label", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, size, fontSize, TextAlignmentOptions.Center);
            text.text = label;

            HoloButtonHighlight highlight = Undo.AddComponent<HoloButtonHighlight>(image.gameObject);
            SerializedObject so = new SerializedObject(highlight);
            so.FindProperty("panel").objectReferenceValue = image;
            so.FindProperty("label").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            return button;
        }

        /// <summary>
        /// A title-screen menu line: an accent bar and a left-aligned label, with
        /// no frame around them.
        ///
        /// Deliberately not CreateButton. A boxed button is right for a dialog
        /// the player is being held in, and wrong for a title screen, where the
        /// artwork is the point and a row of panels sits on top of it like a
        /// sticker. Highlighting is handled by HoloMenuEntry rather than a colour
        /// tint, because there is no panel left to tint.
        /// </summary>
        public static Button CreateMenuEntry(Transform parent, string name, Vector2 anchor,
            Vector2 pivot, Vector2 position, Vector2 size, string label, float fontSize)
        {
            RectTransform rect = CreateRect(parent, name, anchor, pivot, position, size);

            // Invisible, but still the thing that catches the pointer - the row
            // should respond anywhere along it, not only on the glyphs.
            Image hit = Undo.AddComponent<Image>(rect.gameObject);
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            Button button = Undo.AddComponent<Button>(rect.gameObject);
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            RectTransform accent = CreateRect(rect, "Accent", new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, size.y * 0.6f));
            Image accentImage = Undo.AddComponent<Image>(accent.gameObject);
            accentImage.raycastTarget = false;
            accentImage.color = Edge;

            TextMeshProUGUI text = CreateText(rect, "Label", new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(size.x - 30f, size.y),
                fontSize, TextAlignmentOptions.Left);
            text.text = label;

            HoloMenuEntry entry = Undo.AddComponent<HoloMenuEntry>(rect.gameObject);
            SerializedObject so = new SerializedObject(entry);
            so.FindProperty("label").objectReferenceValue = text.transform;
            so.FindProperty("accent").objectReferenceValue = accent;
            so.ApplyModifiedPropertiesWithoutUndo();

            return button;
        }

        /// <summary>
        /// Searches the whole subtree. Transform.Find only looks at direct
        /// children and would quietly return null for anything nested.
        /// </summary>
        public static T Find<T>(Transform root, string name) where T : Component
        {
            foreach (T candidate in root.GetComponentsInChildren<T>(includeInactive: true))
            {
                if (candidate.gameObject.name == name)
                {
                    return candidate;
                }
            }

            Debug.LogWarning("Holo UI is missing '" + name + "', so something will be left unwired.");
            return null;
        }
    }
}
