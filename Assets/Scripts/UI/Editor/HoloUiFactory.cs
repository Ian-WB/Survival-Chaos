using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
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
        public const string FontFolder = "Assets/UI/Fonts";

        /// <summary>
        /// Letter spacing on interface type.
        ///
        /// This was 8 while the interface ran on Liberation Sans, where wide
        /// tracking was doing the work of making a generic face read as a
        /// technical readout. Chakra Petch is already squared off, so the same
        /// value now just reads as loose. One number, tuned here for every screen.
        /// </summary>
        public const float Tracking = 4f;

        /// <summary>Authoring resolution. Scale With Screen Size covers the rest.</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // Cyan-white line work over a cold dark fill: maximum separation from the
        // orange lava that dominates the scene, so the interface never competes
        // with the thing being looked at.
        public static readonly Color Edge = new Color(0.55f, 0.95f, 1.00f, 1.00f);

        /// <summary>
        /// The frame line on panels and bars (#6EDDF0): a touch softer than the
        /// text cyan so the bright corner ticks the shaders draw read as the
        /// accent. Thin frame, small ticks: the 17 November 2026 redesign.
        /// </summary>
        public static readonly Color FrameLine = new Color(0.431f, 0.867f, 0.941f, 1.00f);
        public static readonly Color Accent = new Color(0.30f, 0.85f, 1.00f, 1.00f);
        /// <summary>
        /// The dark hologram backing plate (#04101A at 86%): opaque enough that
        /// lava behind a panel no longer shows through it. Was a 42% teal.
        /// </summary>
        public static readonly Color PanelFill = new Color(0.016f, 0.063f, 0.102f, 0.86f);
        public static readonly Color TrackDark = new Color(0.03f, 0.12f, 0.17f, 0.55f);
        public static readonly Color Loss = new Color(1.00f, 0.32f, 0.34f, 1.00f);

        // One meaning per colour (17 November 2026 redesign): green is hull,
        // crimson is danger and the boss, gold is experience, vivid cyan is an
        // ability, muted ice-cyan is the clock.
        public static readonly Color Health = new Color(0.180f, 0.898f, 0.490f, 1.00f);
        public static readonly Color HealthCaution = new Color(1.000f, 0.722f, 0.188f, 1.00f);
        public static readonly Color Danger = new Color(1.000f, 0.231f, 0.306f, 1.00f);
        public static readonly Color Boss = Danger;
        public static readonly Color Ability = new Color(0.161f, 0.851f, 1.000f, 1.00f);
        public static readonly Color Clock = new Color(0.471f, 0.761f, 0.831f, 1.00f);
        public static readonly Color BossTrack = new Color(0.078f, 0.031f, 0.051f, 0.88f);

        /// <summary>
        /// Experience: the gold of the numbers a kill raises, so the bar they
        /// fill reads as theirs. It was the dash bar's cyan until 16 November
        /// 2026, which tied it to the wrong thing.
        /// </summary>
        public static readonly Color Experience = new Color(1.00f, 0.820f, 0.251f, 1.00f);

        /// <summary>The XP bar's trail as it empties at a level-up: dim gold, not the damage red.</summary>
        public static readonly Color ExperienceTrail = new Color(0.75f, 0.55f, 0.18f, 1.00f);
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

            TMP_FontAsset font = InterfaceFont;
            if (font != null)
            {
                text.font = font;
            }

            text.characterSpacing = Tracking;
            text.fontStyle = FontStyles.UpperCase;
            return text;
        }

        /// <summary>
        /// The interface typeface, found by looking in the font folder rather than
        /// by naming a file.
        ///
        /// Deliberately not a hardcoded asset path: seven of those broke silently
        /// the last time the project was reorganised, and they only fail at the
        /// moment somebody runs a tool. Renaming or replacing the font asset here
        /// needs no code change.
        ///
        /// Returns null when nothing is found, and every caller falls back to
        /// TextMeshPro's default rather than producing text with no font at all.
        /// </summary>
        public static TMP_FontAsset InterfaceFont
        {
            get
            {
                if (!AssetDatabase.IsValidFolder(FontFolder))
                {
                    Debug.LogWarning("No '" + FontFolder + "' folder, so the interface will fall " +
                                     "back to TextMeshPro's default font.");
                    return null;
                }

                foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontFolder }))
                {
                    TMP_FontAsset font =
                        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));

                    if (font != null)
                    {
                        return font;
                    }
                }

                Debug.LogWarning("No TMP font asset in '" + FontFolder + "', so the interface will " +
                                 "fall back to TextMeshPro's default font.");
                return null;
            }
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
            variant.SetColor("_EdgeColor", FrameLine);
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
            variant.SetColor("_EdgeColor", FrameLine);
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
        /// Points one of a component's serialized reference fields at a value, by
        /// the field's name.
        ///
        /// The fields these builders wire are <c>[SerializeField] private</c>: the
        /// component owns them, and nothing at runtime has any business repointing
        /// the pause screen or the health bar. That leaves SerializedObject as the
        /// way in, which is the same route ConfigureBar above already takes and the
        /// one VictoryMenu's screen has always used.
        ///
        /// Applied with undo, so a builder run stays one Ctrl+Z. It also marks the
        /// object dirty on its own, which is why the callers no longer need a
        /// separate SetDirty for the references they set through here.
        /// </summary>
        /// <returns>False when the field does not exist, having said so.</returns>
        public static bool Assign(Object owner, string field, Object value)
        {
            if (owner == null)
            {
                return false;
            }

            SerializedObject so = new SerializedObject(owner);
            SerializedProperty property = so.FindProperty(field);

            if (property == null)
            {
                // A rename on the component side would otherwise show up as a
                // reference that silently never got wired, which is the same
                // symptom as the builder not having run at all.
                Debug.LogWarning(
                    $"{owner.GetType().Name} has no serialized field named '{field}', so it was " +
                    "left unwired.", owner);
                return false;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            return true;
        }

        /// <summary>Reads one of a component's serialized reference fields by name.</summary>
        public static Object Read(Object owner, string field)
        {
            if (owner == null)
            {
                return null;
            }

            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            return property != null ? property.objectReferenceValue : null;
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
        /// One labelled volume slider with a percentage readout, bound to a
        /// channel. Built here rather than in each menu builder so the pause
        /// screen and the title screen cannot drift apart — they show the same
        /// channels and read from the same place.
        /// </summary>
        public static Slider CreateVolumeRow(Transform panel, AudioChannel channel, string label,
            float top, Material barMaterial)
        {
            Vector2 anchor = new Vector2(0.5f, 1f);
            Vector2 middle = new Vector2(0.5f, 0.5f);

            TextMeshProUGUI caption = CreateText(panel, label + " Label", anchor,
                new Vector2(0f, 0.5f), new Vector2(-240f, top), new Vector2(300f, 26f),
                18f, TextAlignmentOptions.Left);
            caption.text = label;

            TextMeshProUGUI readout = CreateText(panel, label + " Readout", anchor,
                new Vector2(1f, 0.5f), new Vector2(240f, top), new Vector2(100f, 26f),
                18f, TextAlignmentOptions.Right);
            readout.text = "100%";

            Image image = CreateBarImage(panel, label + " Bar", anchor, middle,
                new Vector2(0f, top - 34f), new Vector2(480f, 26f), barMaterial, Accent, 10f);
            // Unlike the HUD bars, this one is dragged.
            image.raycastTarget = true;

            Slider slider = Undo.AddComponent<Slider>(image.gameObject);
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.handleRect = CreateHandle((RectTransform)image.transform);
            slider.targetGraphic = image;

            HoloBar holo = Undo.AddComponent<HoloBar>(image.gameObject);
            ConfigureBar(holo, slider, 0f);

            VolumeControl volume = Undo.AddComponent<VolumeControl>(image.gameObject);
            SerializedObject so = new SerializedObject(volume);
            // See CreateOptionRow: intValue is the enum's value, enumValueIndex is
            // its position in the declaration. AudioChannel has no gaps today, so
            // both work - which is exactly why the wrong one survives unnoticed.
            so.FindProperty("channel").intValue = (int)channel;
            so.FindProperty("slider").objectReferenceValue = slider;
            so.FindProperty("readout").objectReferenceValue = readout;
            so.ApplyModifiedPropertiesWithoutUndo();

            return slider;
        }

        /// <summary>
        /// Records which screen a Back button returns to, so Esc can retrace the
        /// same path.
        ///
        /// Always set next to the button it mirrors. Two places deciding what
        /// "back" means is how a key and a button end up disagreeing.
        /// </summary>
        public static void SetPrevious(GameObject screen, GameObject target)
        {
            MenuScreen menu = screen == null ? null : screen.GetComponent<MenuScreen>();
            if (menu == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(menu);
            SerializedProperty property = so.FindProperty("previous");

            // Named lookups into a serialised field fail silently if the field is
            // renamed, and a builder that quietly stops wiring Esc is worse than
            // one that stops. Say so loudly instead.
            if (property == null)
            {
                Debug.LogError("MenuScreen has no 'previous' field - Esc cannot step back. " +
                    "Rename the field back or update SetPrevious.", menu);
                return;
            }

            property.objectReferenceValue = target == null ? null : target.GetComponent<MenuScreen>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The sharpness slider: the one graphics control that is not a cycler.
        ///
        /// Built from the same bar the volume rows use, so it looks like a slider
        /// the player has already met rather than a new kind of widget. Laid out
        /// to occupy one cycler row's worth of height, with the bar under the
        /// label, so it drops into the column without moving anything below it.
        /// </summary>
        public static Slider CreateSharpnessRow(Transform panel, string label,
            float columnX, float top, Material barMaterial)
        {
            Slider slider = CreateSliderRow<Slider>(panel, label, columnX, top, barMaterial,
                "33%  (Low)", out _, out TextMeshProUGUI readout, out TextMeshProUGUI note);
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.33f;

            SharpnessControl control = Undo.AddComponent<SharpnessControl>(slider.gameObject);
            SerializedObject so = new SerializedObject(control);
            so.FindProperty("slider").objectReferenceValue = slider;
            so.FindProperty("readout").objectReferenceValue = readout;
            so.FindProperty("note").objectReferenceValue = note;
            so.ApplyModifiedPropertiesWithoutUndo();

            return slider;
        }

        /// <summary>
        /// A whole-number graphics slider - frame cap, dynamic resolution or
        /// render scale - bound to its control, which sets the range.
        ///
        /// The same bar as the sharpness row, so every slider on the screen reads
        /// as one kind of control. A HoldStepSlider rather than a plain Slider,
        /// so a held direction on the keys or the pad speeds up instead of
        /// crossing 270 values one at a time. The control is handed the caption
        /// too, so it can dim the row the way an inert cycler dims.
        /// </summary>
        public static Slider CreateGraphicsSliderRow<TControl>(Transform panel, string label,
            float columnX, float top, Material barMaterial)
            where TControl : GraphicsSliderControl
        {
            HoldStepSlider slider = CreateSliderRow<HoldStepSlider>(panel, label, columnX, top, barMaterial,
                "-", out TextMeshProUGUI caption, out TextMeshProUGUI readout, out TextMeshProUGUI note);
            slider.wholeNumbers = true;

            TControl control = Undo.AddComponent<TControl>(slider.gameObject);
            SerializedObject so = new SerializedObject(control);
            so.FindProperty("slider").objectReferenceValue = slider;
            so.FindProperty("readout").objectReferenceValue = readout;
            so.FindProperty("note").objectReferenceValue = note;
            so.FindProperty("label").objectReferenceValue = caption;
            so.ApplyModifiedPropertiesWithoutUndo();

            return slider;
        }

        /// <summary>
        /// A label, a readout, a draggable bar and a note line, in the same
        /// shape as a cycler row (<see cref="ApplySliderRowLayout"/>).
        /// </summary>
        private static T CreateSliderRow<T>(Transform panel, string label, float columnX, float top,
            Material barMaterial, string placeholder, out TextMeshProUGUI caption, out TextMeshProUGUI readout,
            out TextMeshProUGUI note)
            where T : Slider
        {
            Vector2 anchor = new Vector2(0.5f, 1f);
            Vector2 middle = new Vector2(0.5f, 0.5f);

            caption = CreateText(panel, label + " Label", anchor, middle, Vector2.zero, Vector2.one,
                21f, TextAlignmentOptions.Left);
            caption.text = label;

            readout = CreateText(panel, label + " Readout", anchor, middle, Vector2.zero, Vector2.one,
                21f, TextAlignmentOptions.Center);
            readout.text = placeholder;

            Image image = CreateBarImage(panel, label + " Bar", anchor, middle,
                Vector2.zero, Vector2.one, barMaterial, Accent, 8f);
            image.raycastTarget = true;

            T slider = Undo.AddComponent<T>(image.gameObject);
            slider.transition = Selectable.Transition.None;
            slider.handleRect = CreateHandle((RectTransform)image.transform);
            slider.targetGraphic = image;

            HoloBar holo = Undo.AddComponent<HoloBar>(image.gameObject);
            ConfigureBar(holo, slider, 0f);

            note = CreateText(panel, label + " Note", anchor, middle, Vector2.zero, Vector2.one,
                14f, TextAlignmentOptions.Left);
            note.text = string.Empty;
            note.color = new Color(Edge.r, Edge.g, Edge.b, 0.55f);

            ApplySliderRowLayout(panel, label, columnX, top);
            return slider;
        }

        /// <summary>
        /// Lays a slider row out exactly where a cycler row puts its parts, so
        /// the two kinds line up down a column: the label on the left, the value
        /// where a cycler shows its value, the bar beneath it across the span the
        /// two arrows occupy, and the note under the label at the cycler's note
        /// width.
        ///
        /// Until 17 November 2026 the bar ran under the label across the whole
        /// column, with the value pushed out past the arrows and the note a line
        /// lower. With three sliders among the cyclers instead of one, every
        /// column had rows of two different shapes and nothing lined up.
        ///
        /// Public so a scene can be brought into line without rebuilding its
        /// menus: it only moves the four named parts.
        /// </summary>
        public static void ApplySliderRowLayout(Transform panel, string label, float columnX, float top)
        {
            Vector2 left = new Vector2(0f, 0.5f);
            Vector2 middle = new Vector2(0.5f, 0.5f);

            // The arrows sit at columnX + 50 and + 290, 46 wide: this is their span.
            const float controlsCentre = 170f;
            const float controlsWidth = 286f;

            Place(panel, label + " Label", left, new Vector2(columnX - 300f, top), new Vector2(290f, 30f));
            Place(panel, label + " Readout", middle, new Vector2(columnX + controlsCentre, top - 2f),
                new Vector2(controlsWidth, 28f));
            Place(panel, label + " Bar", middle, new Vector2(columnX + controlsCentre, top - 26f),
                new Vector2(controlsWidth, 14f));
            Place(panel, label + " Note", left, new Vector2(columnX - 300f, top - 24f), new Vector2(NoteWidth, 20f));

            Transform readout = panel.Find(label + " Readout");
            if (readout != null && readout.TryGetComponent(out TextMeshProUGUI value))
            {
                value.alignment = TextAlignmentOptions.Center;
                value.enableAutoSizing = true;
                value.fontSizeMin = 14f;
                value.fontSizeMax = 19f;
                value.textWrappingMode = TextWrappingModes.NoWrap;
                EditorUtility.SetDirty(value);
            }

            // The cycler note's settings, for the same reason: it ends short of
            // the controls and shrinks rather than running under them.
            Transform noteLine = panel.Find(label + " Note");
            if (noteLine != null && noteLine.TryGetComponent(out TextMeshProUGUI note))
            {
                note.fontSize = 14f;
                note.textWrappingMode = TextWrappingModes.NoWrap;
                note.enableAutoSizing = true;
                note.fontSizeMin = NoteSmallest;
                note.fontSizeMax = 14f;
                EditorUtility.SetDirty(note);
            }
        }

        private static void Place(Transform panel, string name, Vector2 pivot, Vector2 position, Vector2 size)
        {
            Transform child = panel.Find(name);
            if (child == null)
            {
                Debug.LogError("Slider row part '" + name + "' is missing from " + panel.name, panel);
                return;
            }

            RectTransform rect = (RectTransform)child;
            Undo.RecordObject(rect, "Lay out slider row");
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>
        /// Panel size for every options tab.
        ///
        /// Wide rather than tall: settings stacked in one column made a panel
        /// nearly as high as the screen, with a long dead gap between every label
        /// and its controls. Two columns use the shape a monitor actually is.
        /// The columns sit at plus and minus 340 and a row's plate spans from
        /// columnX - 316 to columnX + 328, so two-column content runs from -656 to
        /// +668.
        ///
        /// Audio needs a fraction of the width, and gets it anyway. The tabs are
        /// one screen to the player, and a frame that changed size with every
        /// press of a shoulder would read as three windows again.
        /// </summary>
        public static readonly Vector2 OptionsPanelSize = new Vector2(1400f, 700f);

        /// <summary>
        /// Where the Back button goes on each tab. Far enough below the last row
        /// that the note line underneath it still has somewhere to go: the
        /// longest column is five rows from -170 in steps of 84, putting the last
        /// note at -530.
        /// </summary>
        public const float OptionsBackY = -620f;

        /// <summary>The tabs, in strip order. Content is built by position.</summary>
        public static readonly string[] OptionTabNames = { "Audio", "Display", "Graphics", "Controls", "HUD" };

        private static readonly Vector2 BackButtonSize = new Vector2(420f, 72f);

        /// <summary>
        /// The plate behind a settings row while it has focus. A little brighter
        /// than the panel it sits on, so the row reads as lifted rather than as a
        /// second panel.
        /// </summary>
        private static readonly Color RowFill = new Color(0.10f, 0.45f, 0.60f, 0.30f);

        /// <summary>Horizontal centre of each column, relative to the panel.</summary>
        private const float LeftColumn = -340f;
        private const float RightColumn = 340f;

        /// <summary>Single centred column, for panels that only need one.</summary>
        private const float OneColumn = 30f;

        /// <summary>
        /// Fills a panel with everything about how the image is produced and
        /// presented, in two columns.
        ///
        /// The left column is what a player sets once to fit their monitor. The
        /// right is reconstruction — which upscaler, how hard it pushes, and what
        /// resolves edges when none of them is doing it. Those three are adjacent
        /// because changing the first changes what the other two mean, and a row
        /// that has just gone inert should be visible from the one that did it.
        /// </summary>
        public static List<Selectable> PopulateDisplayPanel(Transform panel, Material panelMaterial,
            Material barMaterial)
        {
            List<Selectable> order = new List<Selectable>();

            const float top = -170f;
            const float step = 84f;

            // Left: what a player sets once to fit their monitor. Frame cap and
            // dynamic resolution sit together because both are about holding a
            // frame rate, and a player looking for one will be looking for the
            // other.
            float y = top;
            order.Add(CreateOptionRow(panel, GraphicsOptionKind.Resolution, "Resolution", LeftColumn, y, panelMaterial));
            y -= step;
            order.Add(CreateOptionRow(panel, GraphicsOptionKind.ScreenMode, "Window Mode", LeftColumn, y, panelMaterial));
            y -= step;
            order.Add(CreateOptionRow(panel, GraphicsOptionKind.VSync, "VSync", LeftColumn, y, panelMaterial));
            y -= step;
            order.Add(CreateGraphicsSliderRow<FrameCapControl>(panel, "Frame Cap", LeftColumn, y, barMaterial));
            y -= step;
            order.Add(CreateGraphicsSliderRow<DynamicResolutionControl>(panel, "Dynamic Resolution",
                LeftColumn, y, barMaterial));

            // Right: reconstruction.
            y = top;
            order.Add(CreateOptionRow(panel, GraphicsOptionKind.UpscaleMethod, "Upscaling", RightColumn, y, panelMaterial));
            y -= step;
            order.Add(CreateOptionRow(panel, GraphicsOptionKind.UpscaleQuality, "Upscale Quality", RightColumn, y, panelMaterial));
            y -= step;
            order.Add(CreateGraphicsSliderRow<RenderScaleControl>(panel, "Render Scale", RightColumn, y, barMaterial));
            y -= step;
            order.Add(CreateOptionRow(panel, GraphicsOptionKind.AntiAliasing, "Anti-Aliasing", RightColumn, y, panelMaterial));
            y -= step;

            // Directly under the row it depends on: sharpening is a property of
            // whatever resolved the edges, so the two belong together.
            order.Add(CreateSharpnessRow(panel, "Sharpness", RightColumn, y, barMaterial));

            return order;
        }

        /// <summary>
        /// Fills a panel with what is actually in the image, as opposed to how it
        /// gets drawn — that half now lives on the display screen.
        ///
        /// Seven rows, down from ten, and still two columns: the split is by what
        /// the setting spends money on, left for how light is computed and right
        /// for what gets layered over the image afterwards.
        ///
        /// What left, and why, because the pattern matters more than the list.
        /// Texture Quality and Anisotropic Filtering are per-level QualitySettings
        /// values the tier already carries, so they went with the code that used
        /// to override them. Shadow Quality narrowed to contact shadows, then went
        /// too. Ambient Occlusion followed it. Both of those last two are volume
        /// overrides that the tier assets stopped compiling - supportSSAO and
        /// supportContactShadows are off on all three now - and a row that can
        /// never take effect is worse than no row, which is the same principle
        /// that got the greyed-out state built in the first place.
        ///
        /// So the count moves with the tiers rather than being authored here. Turn
        /// either flag back on and its row has to come back with it.
        /// </summary>
        public static List<Selectable> PopulateGraphicsPanel(Transform panel, Material panelMaterial)
        {
            List<Selectable> order = new List<Selectable>();

            const float top = -170f;
            const float step = 84f;

            (GraphicsOptionKind kind, string label)[] lighting =
            {
                (GraphicsOptionKind.Quality, "Quality"),
                (GraphicsOptionKind.GlobalIllumination, "Global Illumination"),
                (GraphicsOptionKind.Reflections, "Reflections"),
                // The sun, the lava light that casts, and the clouds. It came back
                // as a row that writes nothing in the pipeline asset, so unlike
                // the Shadow Quality row described above no tier can gate it out.
                (GraphicsOptionKind.Shadows, "Shadows")
            };

            (GraphicsOptionKind kind, string label)[] image =
            {
                (GraphicsOptionKind.VolumetricFog, "Volumetric Fog"),
                (GraphicsOptionKind.VolumetricClouds, "Volumetric Clouds"),
                (GraphicsOptionKind.MotionBlur, "Motion Blur")
            };

            for (int i = 0; i < lighting.Length; i++)
            {
                order.Add(CreateOptionRow(panel, lighting[i].kind, lighting[i].label,
                    LeftColumn, top - i * step, panelMaterial));
            }

            for (int i = 0; i < image.Length; i++)
            {
                order.Add(CreateOptionRow(panel, image[i].kind, image[i].label,
                    RightColumn, top - i * step, panelMaterial));
            }

            return order;
        }

        /// <summary>
        /// The four channels, in the order they make sense to reach for: the one
        /// that governs everything, then the two most people actually want to
        /// balance, then menu sound.
        ///
        /// Labelled for players rather than for the mixer. "Effects" and
        /// "Interface" say what is being turned down; "SFX" and "UI" say how the
        /// code is organised.
        /// </summary>
        public static List<Selectable> PopulateAudioPanel(Transform panel, Material barMaterial)
        {
            return new List<Selectable>
            {
                CreateVolumeRow(panel, AudioChannel.Master, "Master", -170f, barMaterial),
                CreateVolumeRow(panel, AudioChannel.Music, "Music", -280f, barMaterial),
                CreateVolumeRow(panel, AudioChannel.Sfx, "Effects", -390f, barMaterial),
                CreateVolumeRow(panel, AudioChannel.Ui, "Interface", -500f, barMaterial)
            };
        }

        /// <summary>
        /// Every action with its key and its pad button, and the two settings
        /// about the controls themselves: rumble, and the hints that teach
        /// them.
        ///
        /// The table is read, not walked: the pad only stops on the two rows
        /// and Back. It sits in the left half, where the other tabs have their
        /// left column, and the rows sit where their right column is, so the
        /// strip turns between four screens of the same shape.
        ///
        /// The wording follows the README's controls table, which is the other
        /// place these are written down. A change to a binding in
        /// InputSystemGameInput wants both.
        /// </summary>
        public static List<Selectable> PopulateControlsPanel(Transform panel, Material panelMaterial)
        {
            const float top = -160f;
            const float step = 40f;
            const float actionX = -656f;
            const float keysX = -386f;
            const float padX = -196f;

            (string action, string keys, string pad)[] bindings =
            {
                ("Fly round the ring", "A / D, Left / Right", "Left stick, d-pad"),
                ("Climb and dive", "W / S, Up / Down", "Left stick, d-pad"),
                ("Dash", "Space", "A or RB"),
                ("Reverse", "Shift, on release", "LB, on release"),
                ("Slow Mo", "E", "Y or LT"),
                ("Pause", "Esc", "Start"),
                ("Move in a menu", "Arrows, Enter", "D-pad, A"),
                ("Back out of a menu", "Esc", "B"),
                ("Switch Options tab", "Q / E", "LB / RB"),
                ("Save a screenshot", "F12", "-")
            };

            Color heading = new Color(Accent.r, Accent.g, Accent.b, 0.8f);
            Color dimmer = new Color(Edge.r, Edge.g, Edge.b, 0.75f);
            AddTableCell(panel, "Action Heading", actionX, top, 260f, "Action", heading);
            AddTableCell(panel, "Keyboard Heading", keysX, top, 180f, "Keyboard", heading);
            AddTableCell(panel, "Controller Heading", padX, top, 200f, "Controller", heading);

            for (int i = 0; i < bindings.Length; i++)
            {
                float y = top - (i + 1) * step;
                string name = bindings[i].action;
                AddTableCell(panel, name + " Action", actionX, y, 260f, bindings[i].action, Edge);
                AddTableCell(panel, name + " Keys", keysX, y, 180f, bindings[i].keys, dimmer);
                AddTableCell(panel, name + " Pad", padX, y, 200f, bindings[i].pad, dimmer);
            }

            return new List<Selectable>
            {
                CreateToggleRow(panel, ToggleOptionKind.Rumble, "Rumble", RightColumn, -170f, panelMaterial),
                CreateToggleRow(panel, ToggleOptionKind.ControlHints, "Control Hints", RightColumn, -254f,
                    panelMaterial)
            };
        }

        private static void AddTableCell(Transform panel, string name, float left, float y, float width,
            string text, Color colour)
        {
            TextMeshProUGUI cell = CreateText(panel, name, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f),
                new Vector2(left, y), new Vector2(width, 30f), 17f, TextAlignmentOptions.Left);
            cell.text = text;
            cell.color = colour;
            cell.enableAutoSizing = true;
            cell.fontSizeMin = 12f;
            cell.fontSizeMax = 17f;
        }

        /// <summary>
        /// One graphics setting: label, a value between two arrows, and a note
        /// underneath when the setting needs explaining.
        ///
        /// Everything is a cycler, including the on/off settings — one control
        /// type means one visual language and one place to fix. The arrows are
        /// plain ASCII rather than typographic guillemets, because the font atlas
        /// is Extended ASCII and a missing glyph would render as a box.
        /// </summary>
        public static OptionRow CreateOptionRow(Transform panel, GraphicsOptionKind kind, string label,
            float columnX, float top, Material panelMaterial)
        {
            RowParts parts = CreateRowShell(panel, label, columnX, top, panelMaterial);

            GraphicsOption option = Undo.AddComponent<GraphicsOption>(parts.Caption.gameObject);
            SerializedObject so = new SerializedObject(option);
            // intValue, not enumValueIndex: the latter is the position in the
            // enum's declared list rather than the value itself, so it silently
            // points at the wrong setting the moment the enum has a gap in it -
            // and GraphicsOptionKind has one, where the old combined upscaling
            // row was removed.
            so.FindProperty("kind").intValue = (int)kind;
            so.FindProperty("value").objectReferenceValue = parts.Value;
            so.FindProperty("note").objectReferenceValue = parts.Note;

            // So the row can grey itself out. Without these it can still refuse
            // the click, but it looks identical to a row that would accept one.
            so.FindProperty("previousButton").objectReferenceValue = parts.Previous;
            so.FindProperty("nextButton").objectReferenceValue = parts.Next;

            so.ApplyModifiedPropertiesWithoutUndo();

            return FinishRow(parts, option);
        }

        /// <summary>
        /// An on/off row for the Controls tab, the same shape as a graphics row
        /// so the tabs read as one screen.
        /// </summary>
        public static OptionRow CreateToggleRow(Transform panel, ToggleOptionKind kind, string label,
            float columnX, float top, Material panelMaterial)
        {
            RowParts parts = CreateRowShell(panel, label, columnX, top, panelMaterial);

            ToggleOption option = Undo.AddComponent<ToggleOption>(parts.Caption.gameObject);
            SerializedObject so = new SerializedObject(option);
            so.FindProperty("kind").intValue = (int)kind;
            so.FindProperty("value").objectReferenceValue = parts.Value;
            so.FindProperty("note").objectReferenceValue = parts.Note;
            so.FindProperty("previousButton").objectReferenceValue = parts.Previous;
            so.FindProperty("nextButton").objectReferenceValue = parts.Next;
            so.ApplyModifiedPropertiesWithoutUndo();

            return FinishRow(parts, option);
        }

        /// <summary>What a settings row is made of, before a setting is attached to it.</summary>
        /// <summary>Width of the note under an option row: from the label to just short of the left arrow.</summary>
        public const float NoteWidth = 318f;

        /// <summary>The smallest a note shrinks to before it is cut.</summary>
        public const float NoteSmallest = 10f;

        private struct RowParts
        {
            public OptionRow Row;
            public Image Plate;
            public TextMeshProUGUI Caption;
            public TextMeshProUGUI Value;
            public TextMeshProUGUI Note;
            public Button Previous;
            public Button Next;
        }

        /// <summary>Wires a row's arrows and plate to its setting once the setting exists.</summary>
        private static OptionRow FinishRow(RowParts parts, SteppedSetting option)
        {
            UnityEventTools.AddVoidPersistentListener(parts.Previous.onClick, new UnityAction(option.Previous));
            UnityEventTools.AddVoidPersistentListener(parts.Next.onClick, new UnityAction(option.Next));

            SerializedObject rowSo = new SerializedObject(parts.Row);
            rowSo.FindProperty("option").objectReferenceValue = option;
            rowSo.FindProperty("plate").objectReferenceValue = parts.Plate;
            rowSo.ApplyModifiedPropertiesWithoutUndo();

            return parts.Row;
        }

        /// <summary>The plate, label, arrows, value and note of a row, with no setting yet.</summary>
        private static RowParts CreateRowShell(Transform panel, string label,
            float columnX, float top, Material panelMaterial)
        {
            Vector2 anchor = new Vector2(0.5f, 1f);
            Vector2 middle = new Vector2(0.5f, 0.5f);

            // First, so it draws behind everything else in the row. It reaches
            // down over the note line, so a row that has one is lit as a whole
            // rather than cut through the middle of its explanation.
            Image plate = CreatePanel(panel, label + " Row", anchor, middle,
                new Vector2(columnX + 6f, top - 8f), new Vector2(644f, 64f),
                panelMaterial, RowFill, "HoloRow");
            plate.raycastTarget = true;
            plate.color = Color.clear;

            OptionRow row = Undo.AddComponent<OptionRow>(plate.gameObject);
            row.transition = Selectable.Transition.None;
            row.targetGraphic = plate;

            // Laid out within a 560-wide column: the label owns the left half and
            // the controls sit together on the right, close enough to read as one
            // group rather than as a label stranded from its value.
            TextMeshProUGUI caption = CreateText(panel, label + " Label", anchor,
                new Vector2(0f, 0.5f), new Vector2(columnX - 300f, top), new Vector2(290f, 30f),
                21f, TextAlignmentOptions.Left);
            caption.text = label;

            Button previous = CreateButton(panel, label + " Prev", anchor, middle,
                new Vector2(columnX + 50f, top), new Vector2(46f, 40f), panelMaterial, "<", 20f);

            TextMeshProUGUI current = CreateText(panel, label + " Value", anchor, middle,
                new Vector2(columnX + 170f, top), new Vector2(180f, 30f), 21f, TextAlignmentOptions.Center);
            current.text = "-";

            Button next = CreateButton(panel, label + " Next", anchor, middle,
                new Vector2(columnX + 290f, top), new Vector2(46f, 40f), panelMaterial, ">", 20f);

            // Ends short of the left arrow, which starts at columnX + 27, and
            // shrinks to fit. It was 560 wide, and the longer notes ran on
            // under the arrow (12 November 2026).
            TextMeshProUGUI note = CreateText(panel, label + " Note", anchor,
                new Vector2(0f, 0.5f), new Vector2(columnX - 300f, top - 24f), new Vector2(NoteWidth, 20f),
                14f, TextAlignmentOptions.Left);
            note.text = string.Empty;
            note.color = new Color(Edge.r, Edge.g, Edge.b, 0.55f);
            note.textWrappingMode = TextWrappingModes.NoWrap;
            note.enableAutoSizing = true;
            note.fontSizeMin = NoteSmallest;
            note.fontSizeMax = 14f;

            // The arrows are for the mouse. The pad stands on the row and uses
            // left and right, so it has to pass over them.
            previous.navigation = new Navigation { mode = Navigation.Mode.None };
            next.navigation = new Navigation { mode = Navigation.Mode.None };

            return new RowParts
            {
                Row = row,
                Plate = plate,
                Caption = caption,
                Value = current,
                Note = note,
                Previous = previous,
                Next = next
            };
        }

        /// <summary>
        /// Fills the option tabs and ties them together: the strip across the
        /// top, each tab's settings, its Back button and the order the keys and
        /// the pad walk them in. Shared by the title screen and the pause screen,
        /// which differ only in where Back goes.
        /// </summary>
        /// <param name="screens">One empty screen per entry in <see cref="OptionTabNames"/>, in order.</param>
        /// <param name="back">Where Back, Esc and the pad's B go from every tab.</param>
        /// <returns>The first tab's <see cref="OptionsTabs"/>, for the Options buttons.</returns>
        public static OptionsTabs BuildOptionsTabs(GameObject[] screens, Material panelMaterial,
            Material barMaterial, GameObject back)
        {
            MenuScreen[] tabs = new MenuScreen[screens.Length];
            for (int i = 0; i < screens.Length; i++)
            {
                tabs[i] = screens[i].GetComponent<MenuScreen>();
            }

            MenuScreen backScreen = back.GetComponent<MenuScreen>();
            OptionsTabs first = null;

            for (int i = 0; i < screens.Length; i++)
            {
                Transform panel = screens[i].transform.Find("Panel");
                AddTabStrip(panel, panelMaterial, tabs, i);

                List<Selectable> order;
                switch (i)
                {
                    case 0: order = PopulateAudioPanel(panel, barMaterial); break;
                    case 1: order = PopulateDisplayPanel(panel, panelMaterial, barMaterial); break;
                    case 2: order = PopulateGraphicsPanel(panel, panelMaterial); break;
                    case 3: order = PopulateControlsPanel(panel, panelMaterial); break;
                    default: order = PopulateHudPanel(panel, panelMaterial, barMaterial); break;
                }

                Button backButton = CreateButton(panel, "Back", new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, OptionsBackY), BackButtonSize,
                    panelMaterial, "Back", 24f);
                UnityEventTools.AddVoidPersistentListener(backButton.onClick, new UnityAction(backScreen.Show));
                SetPrevious(screens[i], back);

                order.Add(backButton);
                ChainVertically(order);

                OptionsTabs component = Undo.AddComponent<OptionsTabs>(screens[i]);
                SerializedObject so = new SerializedObject(component);
                SerializedProperty list = so.FindProperty("tabs");
                list.arraySize = tabs.Length;
                for (int j = 0; j < tabs.Length; j++)
                {
                    list.GetArrayElementAtIndex(j).objectReferenceValue = tabs[j];
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                if (first == null)
                {
                    first = component;
                }
            }

            return first;
        }

        /// <summary>
        /// The tab strip: one framed button per tab, the open one held lit, and
        /// the two keys that turn them at either end.
        ///
        /// The tabs are out of navigation. The shoulders turn them, and a tab the
        /// pad could stand on would be one more stop above the top row for no
        /// gain.
        /// </summary>
        private static void AddTabStrip(Transform panel, Material panelMaterial, MenuScreen[] tabs, int active)
        {
            const float y = -62f;

            Vector2 anchor = new Vector2(0.5f, 1f);
            Vector2 middle = new Vector2(0.5f, 0.5f);
            float width = OptionTabWidth(tabs.Length);
            float span = OptionTabSpan(tabs.Length);

            for (int i = 0; i < tabs.Length; i++)
            {
                float x = OptionTabX(i, tabs.Length);
                Button tab = CreateButton(panel, OptionTabNames[i] + " Tab", anchor, middle,
                    new Vector2(x, y), new Vector2(width, 58f), panelMaterial, OptionTabNames[i], 24f);
                tab.navigation = new Navigation { mode = Navigation.Mode.None };
                UnityEventTools.AddVoidPersistentListener(tab.onClick, new UnityAction(tabs[i].Show));

                if (i == active)
                {
                    SerializedObject so = new SerializedObject(tab.GetComponent<HoloButtonHighlight>());
                    so.FindProperty("held").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            AddTabHint(panel, "Previous Tab Hint", new Vector2(-span / 2f - 70f, y), "Q", "LB");
            AddTabHint(panel, "Next Tab Hint", new Vector2(span / 2f + 70f, y), "E", "RB");
        }

        /// <summary>
        /// A tab's width in the strip: 250 for up to four tabs, 210 for five
        /// (the HUD tab, 17 November 2026), so the strip and its two key hints
        /// still sit inside the 1400-wide panel.
        /// </summary>
        public static float OptionTabWidth(int count) => count > 4 ? 210f : 250f;

        private static float OptionTabGap(int count) => count > 4 ? 14f : 30f;

        /// <summary>The strip's width, tab edge to tab edge.</summary>
        public static float OptionTabSpan(int count) =>
            count * OptionTabWidth(count) + (count - 1) * OptionTabGap(count);

        /// <summary>Centre of tab <paramref name="index"/> of <paramref name="count"/>.</summary>
        public static float OptionTabX(int index, int count) =>
            -OptionTabSpan(count) / 2f + OptionTabWidth(count) / 2f
            + index * (OptionTabWidth(count) + OptionTabGap(count));

        /// <summary>Centre of the Next Tab hint; the Previous one mirrors it.</summary>
        public static float OptionTabHintX(int count) => OptionTabSpan(count) / 2f + 70f;

        /// <summary>
        /// The HUD tab (17 November 2026 redesign): HUD SCALE, HUD OPACITY and
        /// CONTEXTUAL HUD, in one centred column, nothing else.
        /// </summary>
        public static List<Selectable> PopulateHudPanel(Transform panel, Material panelMaterial,
            Material barMaterial)
        {
            const float top = -170f;
            const float step = 84f;

            return new List<Selectable>
            {
                CreateHudSliderRow(panel, HudSliderKind.Scale, "HUD Scale", OneColumn, top, barMaterial),
                CreateHudSliderRow(panel, HudSliderKind.Opacity, "HUD Opacity", OneColumn, top - step,
                    barMaterial),
                CreateToggleRow(panel, ToggleOptionKind.HudContextual, "Contextual HUD", OneColumn,
                    top - 2f * step, panelMaterial)
            };
        }

        /// <summary>A HUD scale or opacity slider, in the graphics sliders' row shape.</summary>
        public static Slider CreateHudSliderRow(Transform panel, HudSliderKind kind, string label,
            float columnX, float top, Material barMaterial)
        {
            HoldStepSlider slider = CreateSliderRow<HoldStepSlider>(panel, label, columnX, top, barMaterial,
                "100%", out _, out TextMeshProUGUI readout, out TextMeshProUGUI note);
            slider.wholeNumbers = true;
            slider.minValue = 0f;
            slider.maxValue = HudOptionControl.LastPosition(kind);
            slider.value = HudOptionControl.PositionOf(kind, 100);

            HudOptionControl control = Undo.AddComponent<HudOptionControl>(slider.gameObject);
            SerializedObject so = new SerializedObject(control);
            so.FindProperty("kind").intValue = (int)kind;
            so.FindProperty("slider").objectReferenceValue = slider;
            so.FindProperty("readout").objectReferenceValue = readout;
            so.FindProperty("note").objectReferenceValue = note;
            so.ApplyModifiedPropertiesWithoutUndo();

            return slider;
        }

        private static void AddTabHint(Transform panel, string name, Vector2 position,
            string keyboard, string pad)
        {
            TextMeshProUGUI text = CreateText(panel, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                position, new Vector2(90f, 40f), 20f, TextAlignmentOptions.Center);
            text.text = keyboard;
            text.color = new Color(Edge.r, Edge.g, Edge.b, 0.55f);

            InputHint hint = Undo.AddComponent<InputHint>(text.gameObject);
            SerializedObject so = new SerializedObject(hint);
            so.FindProperty("keyboard").stringValue = keyboard;
            so.FindProperty("pad").stringValue = pad;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A small line in a corner of a menu that fills itself in when the
        /// menu opens: the build stamp, or the best runs. Dim, because it is
        /// there to be found rather than read.
        /// </summary>
        public static TextMeshProUGUI CreateFootnote(Transform parent, string name, MenuFootnote.Content content,
            Vector2 anchor, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions align, float alpha)
        {
            TextMeshProUGUI text = CreateText(parent, name, anchor, anchor, position, size, fontSize, align);
            text.text = content == MenuFootnote.Content.BuildStamp ? "Build" : "Best";
            text.color = new Color(Edge.r, Edge.g, Edge.b, alpha);

            MenuFootnote footnote = Undo.AddComponent<MenuFootnote>(text.gameObject);
            SerializedObject so = new SerializedObject(footnote);
            so.FindProperty("content").intValue = (int)content;
            so.ApplyModifiedPropertiesWithoutUndo();
            return text;
        }

        /// <summary>The build stamp, bottom right of a full-screen menu.</summary>
        public static void AddBuildStamp(Transform screen)
        {
            TextMeshProUGUI stamp = CreateFootnote(screen, "Build Stamp", MenuFootnote.Content.BuildStamp,
                new Vector2(1f, 0f), new Vector2(-28f, 22f), new Vector2(700f, 28f), 15f,
                TextAlignmentOptions.BottomRight, 0.45f);

            // As written: the folder is named in mixed case, and a commit is
            // looked up by exactly what is on screen.
            stamp.fontStyle = FontStyles.Normal;
        }

        /// <summary>
        /// Makes the controls one list for the keys and the pad: down goes to the
        /// next, up to the one before, and nothing goes sideways.
        ///
        /// Two columns are walked as one, so down from the foot of the left
        /// column goes to the head of the right. Sideways is taken, since it
        /// changes the value, so crossing between columns happens at the ends.
        /// </summary>
        public static void ChainVertically(IList<Selectable> order)
        {
            for (int i = 0; i < order.Count; i++)
            {
                order[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? order[i - 1] : null,
                    selectOnDown = i < order.Count - 1 ? order[i + 1] : null
                };
            }
        }

        /// <summary>
        /// The invisible handle a Slider needs in order to be draggable.
        ///
        /// Slider.UpdateDrag maps the pointer through <c>handleRect.parent</c>,
        /// falling back to <c>fillRect.parent</c>. With neither assigned it
        /// returns immediately and the control cannot be moved at all — which is
        /// not obvious, because the slider still looks and reports as if it works.
        ///
        /// Nothing is drawn on it: the bar shader already marks the value with its
        /// leading edge, so a second handle would be a duplicate.
        /// </summary>
        private static RectTransform CreateHandle(RectTransform bar)
        {
            // The area is the rect the pointer is measured against, so it must
            // span the bar; the handle inside it is what Slider actually moves.
            RectTransform area = CreateRect(bar, "Handle Area", Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;

            RectTransform handle = CreateRect(area, "Handle", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 0f));
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.offsetMin = new Vector2(-10f, 0f);
            handle.offsetMax = new Vector2(10f, 0f);
            return handle;
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
