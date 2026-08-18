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
        /// One labelled volume slider with a percentage readout, bound to a
        /// channel. Built here rather than in each menu builder so the pause
        /// screen and the title screen cannot drift apart — they show the same
        /// channels and read from the same place.
        /// </summary>
        public static void CreateVolumeRow(Transform panel, AudioChannel channel, string label,
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
        public static void CreateSharpnessRow(Transform panel, string label,
            float columnX, float top, Material barMaterial)
        {
            Vector2 anchor = new Vector2(0.5f, 1f);
            Vector2 middle = new Vector2(0.5f, 0.5f);

            TextMeshProUGUI caption = CreateText(panel, label + " Label", anchor,
                new Vector2(0f, 0.5f), new Vector2(columnX - 300f, top), new Vector2(290f, 30f),
                21f, TextAlignmentOptions.Left);
            caption.text = label;

            TextMeshProUGUI readout = CreateText(panel, label + " Readout", anchor,
                new Vector2(1f, 0.5f), new Vector2(columnX + 340f, top), new Vector2(200f, 30f),
                18f, TextAlignmentOptions.Right);
            readout.text = "33%  (Low)";

            Image image = CreateBarImage(panel, label + " Bar", anchor, middle,
                new Vector2(columnX + 30f, top - 26f), new Vector2(560f, 20f), barMaterial, Accent, 8f);
            image.raycastTarget = true;

            Slider slider = Undo.AddComponent<Slider>(image.gameObject);
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.33f;
            slider.handleRect = CreateHandle((RectTransform)image.transform);
            slider.targetGraphic = image;

            HoloBar holo = Undo.AddComponent<HoloBar>(image.gameObject);
            ConfigureBar(holo, slider, 0f);

            TextMeshProUGUI note = CreateText(panel, label + " Note", anchor,
                new Vector2(0f, 0.5f), new Vector2(columnX - 300f, top - 44f), new Vector2(560f, 20f),
                14f, TextAlignmentOptions.Left);
            note.text = string.Empty;
            note.color = new Color(Edge.r, Edge.g, Edge.b, 0.55f);

            SharpnessControl control = Undo.AddComponent<SharpnessControl>(image.gameObject);
            SerializedObject so = new SerializedObject(control);
            so.FindProperty("slider").objectReferenceValue = slider;
            so.FindProperty("readout").objectReferenceValue = readout;
            so.FindProperty("note").objectReferenceValue = note;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Panel size the display rows need.
        ///
        /// Wide rather than tall: settings stacked in one column made a panel
        /// nearly as high as the screen, with a long dead gap between every label
        /// and its controls. Two columns use the shape a monitor actually is.
        /// </summary>
        public static readonly Vector2 DisplayPanelSize = new Vector2(1400f, 700f);

        /// <summary>
        /// The same as display, because it is now the same shape: two columns of
        /// five rows.
        ///
        /// It was 780 wide back when graphics was a single centred column. The
        /// columns sit at plus and minus 340 and a row spans from columnX - 300 to
        /// columnX + 313, so two-column content runs from -640 to +653 - close to
        /// 1300 wide, against a 780 panel. Every label and every stepper hung
        /// outside the frame.
        /// </summary>
        public static readonly Vector2 GraphicsPanelSize = DisplayPanelSize;

        /// <summary>
        /// Where the Back button goes on each panel. Far enough below the last row
        /// that the note line underneath it still has somewhere to go.
        ///
        /// Both panels carry five rows from -170 in steps of 84, putting the last
        /// note at -530, so both use the same value.
        /// </summary>
        public const float DisplayBackY = -620f;
        public const float GraphicsBackY = DisplayBackY;

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
        public static void PopulateDisplayPanel(Transform panel, Material panelMaterial,
            Material barMaterial)
        {
            const float top = -170f;
            const float step = 84f;

            (GraphicsOptionKind kind, string label)[] screen =
            {
                (GraphicsOptionKind.Resolution, "Resolution"),
                (GraphicsOptionKind.ScreenMode, "Window Mode"),
                (GraphicsOptionKind.VSync, "VSync"),
                (GraphicsOptionKind.FrameCap, "Frame Cap"),
                // Beside the frame cap rather than with the upscalers: both are
                // about holding a frame rate, and a player looking for one will
                // be looking for the other.
                (GraphicsOptionKind.DynamicResolution, "Dynamic Resolution")
            };

            (GraphicsOptionKind kind, string label)[] reconstruction =
            {
                (GraphicsOptionKind.UpscaleMethod, "Upscaling"),
                (GraphicsOptionKind.UpscaleQuality, "Upscale Quality"),
                (GraphicsOptionKind.RenderScale, "Render Scale"),
                (GraphicsOptionKind.AntiAliasing, "Anti-Aliasing")
            };

            for (int i = 0; i < screen.Length; i++)
            {
                CreateOptionRow(panel, screen[i].kind, screen[i].label,
                    LeftColumn, top - i * step, panelMaterial);
            }

            for (int i = 0; i < reconstruction.Length; i++)
            {
                CreateOptionRow(panel, reconstruction[i].kind, reconstruction[i].label,
                    RightColumn, top - i * step, panelMaterial);
            }

            // Directly under the row it depends on: sharpening is a property of
            // whatever resolved the edges, so the two belong together.
            CreateSharpnessRow(panel, "Sharpness",
                RightColumn, top - reconstruction.Length * step, barMaterial);
        }

        /// <summary>
        /// Fills a panel with what is actually in the image, as opposed to how it
        /// gets drawn — that half now lives on the display screen.
        ///
        /// Down to five rows from ten, and still two columns: the split is by what
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
        public static void PopulateGraphicsPanel(Transform panel, Material panelMaterial)
        {
            const float top = -170f;
            const float step = 84f;

            (GraphicsOptionKind kind, string label)[] lighting =
            {
                (GraphicsOptionKind.Quality, "Quality"),
                (GraphicsOptionKind.GlobalIllumination, "Global Illumination"),
                (GraphicsOptionKind.Reflections, "Reflections")
            };

            (GraphicsOptionKind kind, string label)[] image =
            {
                (GraphicsOptionKind.VolumetricFog, "Volumetric Fog"),
                (GraphicsOptionKind.MotionBlur, "Motion Blur")
            };

            for (int i = 0; i < lighting.Length; i++)
            {
                CreateOptionRow(panel, lighting[i].kind, lighting[i].label,
                    LeftColumn, top - i * step, panelMaterial);
            }

            for (int i = 0; i < image.Length; i++)
            {
                CreateOptionRow(panel, image[i].kind, image[i].label,
                    RightColumn, top - i * step, panelMaterial);
            }
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
        public static void CreateOptionRow(Transform panel, GraphicsOptionKind kind, string label,
            float columnX, float top, Material panelMaterial)
        {
            Vector2 anchor = new Vector2(0.5f, 1f);
            Vector2 middle = new Vector2(0.5f, 0.5f);

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

            TextMeshProUGUI note = CreateText(panel, label + " Note", anchor,
                new Vector2(0f, 0.5f), new Vector2(columnX - 300f, top - 24f), new Vector2(560f, 20f),
                14f, TextAlignmentOptions.Left);
            note.text = string.Empty;
            note.color = new Color(Edge.r, Edge.g, Edge.b, 0.55f);

            GraphicsOption option = Undo.AddComponent<GraphicsOption>(caption.gameObject);
            SerializedObject so = new SerializedObject(option);
            // intValue, not enumValueIndex: the latter is the position in the
            // enum's declared list rather than the value itself, so it silently
            // points at the wrong setting the moment the enum has a gap in it -
            // and GraphicsOptionKind has one, where the old combined upscaling
            // row was removed.
            so.FindProperty("kind").intValue = (int)kind;
            so.FindProperty("value").objectReferenceValue = current;
            so.FindProperty("note").objectReferenceValue = note;

            // So the row can grey itself out. Without these it can still refuse
            // the click, but it looks identical to a row that would accept one.
            so.FindProperty("previousButton").objectReferenceValue = previous;
            so.FindProperty("nextButton").objectReferenceValue = next;

            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddVoidPersistentListener(previous.onClick, new UnityAction(option.Previous));
            UnityEventTools.AddVoidPersistentListener(next.onClick, new UnityAction(option.Next));
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
