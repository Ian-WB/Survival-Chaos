using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// The menus of the 17 November 2026 redesign (build brief block 8), laid
    /// onto whichever scene is open, without rebuilding its menus:
    ///
    /// - the title screen: a dark rail behind PLAY / OPTIONS / CREDITS / QUIT,
    ///   the bests in a framed strip with a gold BEST tag, a control legend at
    ///   the bottom right;
    /// - options, in both scenes: a control legend under BACK, and on the title
    ///   screen a darker scrim so the 3D title sits dimmed behind the panel;
    /// - pause: the run HUD hidden, a hairline over the summary, the summary as
    ///   chips, the offers on the ring in a gold-edged card at the right;
    /// - the end cards: larger, with a hairline and the summary as chips, the
    ///   HUD hidden; Victory cyan, Death crimson with its own panel edge.
    ///
    /// Idempotent: each piece is found by name and reset, or made. The menu
    /// builders run it at the end, so a rebuild keeps it.
    /// </summary>
    public static class Menus1117
    {
        private const string Materials = "Assets/UI/Materials/";

        /// <summary>Menu card fill strength (gemini-menu-spec: 91%).</summary>
        public const float CardFillAlpha = 0.91f;

        /// <summary>Victory's title: the menus' own cyan, not hull green.</summary>
        public static readonly Color VictoryTitle = HoloUiFactory.Edge;

        /// <summary>Death's title: the boss and danger crimson.</summary>
        public static readonly Color DeathTitle = HoloUiFactory.Danger;

        /// <summary>
        /// The holo shaders write their colours as linear values, so an edge
        /// that should read as #FF3B4E is given its linear equivalent.
        /// </summary>
        private static readonly Color DeathEdge = new Color(1f, 0.0437f, 0.0762f, 1f);
        private static readonly Color DeathBracket = new Color(1f, 0.342f, 0.392f, 1f);
        private static readonly Color GoldEdge = new Color(0.584f, 0.381f, 0.040f, 1f);
        private static readonly Color GoldBracket = new Color(1f, 0.64f, 0.05f, 1f);

        private static readonly Color Hairline = new Color(0.36f, 0.72f, 0.80f, 0.35f);

        /// <summary>A key in a legend: the key's name on a faint plate.</summary>
        public static string Key(string name) =>
            "<mark=#5CB8CC40 padding=\"8,8,3,3\"><color=#DDF8FF>" + name + "</color></mark>";

        private const string LegendDot = "   <alpha=#66>·<alpha=#FF>   ";

        public static readonly string OptionsLegendKeys =
            Key("Q") + " / " + Key("E") + "  SWITCH TAB" + LegendDot
            + Key("LEFT") + " / " + Key("RIGHT") + "  CHANGE" + LegendDot + Key("ESC") + "  BACK";

        public static readonly string OptionsLegendPad =
            Key("LB") + " / " + Key("RB") + "  SWITCH TAB" + LegendDot
            + Key("D-PAD") + "  CHANGE" + LegendDot + Key("B") + "  BACK";

        /// <summary>
        /// The title screen's legend. Not the mockup's ESC / B QUIT: at the
        /// title's root Esc does nothing (MenuScreen), on purpose.
        /// </summary>
        public static readonly string TitleLegendKeys =
            Key("UP") + " / " + Key("DOWN") + "  MOVE" + LegendDot + Key("ENTER") + "  SELECT";

        public static readonly string TitleLegendPad =
            Key("D-PAD") + "  MOVE" + LegendDot + Key("A") + "  SELECT";

        [MenuItem("Survival Chaos/UI/Lay Out Menus (17 Nov)")]
        private static void ApplyFromMenu()
        {
            Debug.Log(Apply());
        }

        /// <summary>Lays the redesign onto the open scene. Returns what it did.</summary>
        public static string Apply()
        {
            Undo.SetCurrentGroupName("Lay out menus (17 Nov)");
            int group = Undo.GetCurrentGroup();
            string done = string.Empty;

            CardMaterials();

            foreach (OptionsTabs tabs in Object.FindObjectsByType<OptionsTabs>(FindObjectsInactive.Include))
            {
                OptionsLegend(tabs.transform);
            }

            GameObject hud = GameObject.Find("Canvas/HUD (Holo)");
            if (hud != null)
            {
                Hide(hud);
                GameObject labels = GameObject.Find("Canvas/Pickup Labels (Holo)");
                if (labels != null)
                {
                    Hide(labels);
                }

                done += "HUD hidden behind menus. ";
            }

            Transform menus = FindRoot("Pause Screen");
            if (menus != null)
            {
                Pause(menus.Find("Pause Screen"));
                EndCard(menus.Find("Victory Screen"), VictoryTitle, null);
                EndCard(menus.Find("Death Screen"), DeathTitle, Load("DeathPanel"));
                done += "Pause and end cards. ";
            }

            Transform title = FindRoot("Title Screen");
            if (title != null)
            {
                Title(title.Find("Title Screen"));
                foreach (OptionsTabs tabs in title.GetComponentsInChildren<OptionsTabs>(true))
                {
                    Image scrim = tabs.GetComponent<Image>();
                    Undo.RecordObject(scrim, "Lay out menus");
                    scrim.color = new Color(0.008f, 0.03f, 0.05f, 0.9f);
                }

                done += "Title screen and its options scrim. ";
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return done.Length > 0 ? done : "Nothing to lay out in this scene.";
        }

        /// <summary>The menu card, the offers card and Death's card.</summary>
        private static void CardMaterials()
        {
            Material card = Load("HoloMenuPanel");
            Color fill = card.GetColor("_FillColor");
            if (!Mathf.Approximately(fill.a, CardFillAlpha))
            {
                Undo.RecordObject(card, "Lay out menus");
                fill.a = CardFillAlpha;
                card.SetColor("_FillColor", fill);
                EditorUtility.SetDirty(card);
            }

            Variant(card, "DeathPanel", DeathEdge, DeathBracket, CardFillAlpha);
            Variant(card, "OffersCard", GoldEdge, GoldBracket, 0.84f);
            AssetDatabase.SaveAssets();
        }

        private static void Variant(Material source, string name, Color edge, Color bracket, float alpha)
        {
            string path = Materials + name + ".mat";
            Material variant = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (variant == null)
            {
                variant = new Material(source) { name = name };
                AssetDatabase.CreateAsset(variant, path);
            }
            else
            {
                variant.CopyPropertiesFromMaterial(source);
            }

            Color fill = source.GetColor("_FillColor");
            fill.a = alpha;
            variant.SetColor("_FillColor", fill);
            variant.SetColor("_EdgeColor", edge);
            variant.SetColor("_BracketColor", bracket);
            EditorUtility.SetDirty(variant);
        }

        private static Material Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");

        private static void Hide(GameObject target)
        {
            if (target.GetComponent<CanvasGroup>() == null)
            {
                CanvasGroup group = Undo.AddComponent<CanvasGroup>(target);
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            if (target.GetComponent<HideBehindMenus>() == null)
            {
                Undo.AddComponent<HideBehindMenus>(target);
            }
        }

        // ---------- Pause ----------

        private static void Pause(Transform screen)
        {
            if (screen == null)
            {
                return;
            }

            Transform panel = screen.Find("Panel");
            Line(panel, "Summary Rule", -594f, 500f);

            TextMeshProUGUI summary = panel.Find("Run Summary").GetComponent<TextMeshProUGUI>();
            Undo.RecordObject(summary.rectTransform, "Lay out menus");
            summary.rectTransform.anchoredPosition = new Vector2(0f, -606f);
            summary.rectTransform.sizeDelta = new Vector2(580f, 166f);
            Summary(summary);

            OffersCard(screen);
        }

        /// <summary>The summary text's look, shared by pause and the end cards.</summary>
        private static void Summary(TextMeshProUGUI text)
        {
            Undo.RecordObject(text, "Lay out menus");
            text.alignment = TextAlignmentOptions.Top;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = 11f;
            text.fontSizeMax = 20f;
            text.fontSize = 20f;
            text.lineSpacing = 12f;
            text.color = new Color(0.72f, 0.95f, 1f, 1f);
            text.raycastTarget = false;
        }

        private static void OffersCard(Transform screen)
        {
            RectTransform holder = Child(screen, "Offers", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            OffersCard offers = Ensure<OffersCard>(holder.gameObject);

            // 5.5% in from the right, centred on the screen's height.
            RectTransform card = Child(holder, "Card", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-106f, 0f), new Vector2(300f, 144f));
            Image plate = Ensure<Image>(card.gameObject);
            plate.material = Load("OffersCard");
            plate.color = Color.white;
            plate.raycastTarget = false;
            if (card.GetComponent<HoloRectData>() == null)
            {
                Undo.AddComponent<HoloRectData>(card.gameObject);
            }

            TMP_FontAsset font = screen.Find("Panel/Title").GetComponent<TMP_Text>().font;

            TextMeshProUGUI header = Text(card, "Header", new Vector2(18f, -16f), new Vector2(264f, 20f), 14f, font);
            header.text = "Offers on ring";
            header.color = new Color(1f, 0.82f, 0.25f, 1f);
            header.characterSpacing = 6f;

            Line(card, "Header Rule", -42f, 264f);

            TextMeshProUGUI list = Text(card, "List", new Vector2(18f, -54f), new Vector2(264f, 200f), 18f, font);
            list.text = "Attack Speed III";
            list.color = new Color(0.86f, 0.97f, 1f, 1f);

            SerializedObject so = new SerializedObject(offers);
            so.FindProperty("card").objectReferenceValue = card.gameObject;
            so.FindProperty("list").objectReferenceValue = list;
            so.FindProperty("baseHeight").floatValue = 64f;
            so.FindProperty("rowHeight").floatValue = 40f;
            so.ApplyModifiedProperties();
        }

        // ---------- End cards ----------

        private static void EndCard(Transform screen, Color title, Material panelMaterial)
        {
            if (screen == null)
            {
                return;
            }

            Transform panel = screen.Find("Panel");
            RectTransform rect = (RectTransform)panel;
            Undo.RecordObject(rect, "Lay out menus");
            rect.sizeDelta = new Vector2(720f, 520f);

            if (panelMaterial != null)
            {
                Image image = panel.GetComponent<Image>();
                Undo.RecordObject(image, "Lay out menus");
                image.material = panelMaterial;
            }

            TextMeshProUGUI heading = panel.Find("Title").GetComponent<TextMeshProUGUI>();
            Undo.RecordObject(heading, "Lay out menus");
            heading.color = title;

            Line(panel, "Summary Rule", -302f, 600f);

            TMP_FontAsset font = heading.font;
            TextMeshProUGUI summary = Text(panel, "Run Summary", Vector2.zero, new Vector2(640f, 190f), 20f, font);
            RectTransform summaryRect = summary.rectTransform;
            Undo.RecordObject(summaryRect, "Lay out menus");
            summaryRect.anchorMin = summaryRect.anchorMax = new Vector2(0.5f, 1f);
            summaryRect.pivot = new Vector2(0.5f, 1f);
            summaryRect.anchoredPosition = new Vector2(0f, -316f);
            Summary(summary);
            summary.text = string.Empty;

            RunSummary component = Ensure<RunSummary>(screen.gameObject);
            SerializedObject so = new SerializedObject(component);
            so.FindProperty("target").objectReferenceValue = summary;
            so.FindProperty("font").objectReferenceValue = font;
            so.ApplyModifiedProperties();
        }

        // ---------- Options ----------

        private static void OptionsLegend(Transform screen)
        {
            Transform panel = screen.Find("Panel");
            TMP_FontAsset font = panel.Find("Back/Label").GetComponent<TMP_Text>().font;

            TextMeshProUGUI legend = Text(panel, "Control Legend", Vector2.zero, new Vector2(1000f, 26f), 15f, font);
            RectTransform rect = legend.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -678f);
            legend.alignment = TextAlignmentOptions.Center;
            legend.color = new Color(0.61f, 0.85f, 0.90f, 0.88f);
            Hint(legend, OptionsLegendKeys, OptionsLegendPad);
        }

        // ---------- Title screen ----------

        private static void Title(Transform screen)
        {
            if (screen == null)
            {
                return;
            }

            Material plate = Load("HudPlate");

            // Behind the four entries (centres 152 to 380, 60 tall, labels from x 180).
            RectTransform rail = Child(screen, "Menu Rail", Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(124f, 112f), new Vector2(300f, 308f));
            rail.SetSiblingIndex(0);
            Image railImage = Ensure<Image>(rail.gameObject);
            railImage.material = plate;
            railImage.color = Color.white;
            railImage.raycastTarget = false;
            if (rail.GetComponent<HoloRectData>() == null)
            {
                Undo.AddComponent<HoloRectData>(rail.gameObject);
            }

            // The bests, framed, sized to the line.
            RectTransform strip = Child(screen, "Best Strip", Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(124f, 60f), new Vector2(600f, 44f));
            Image stripImage = Ensure<Image>(strip.gameObject);
            stripImage.material = plate;
            stripImage.color = Color.white;
            stripImage.raycastTarget = false;
            if (strip.GetComponent<HoloRectData>() == null)
            {
                Undo.AddComponent<HoloRectData>(strip.gameObject);
            }

            HorizontalLayoutGroup layout = Ensure<HorizontalLayoutGroup>(strip.gameObject);
            layout.padding = new RectOffset(18, 18, 9, 9);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = Ensure<ContentSizeFitter>(strip.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Transform best = screen.Find("Best Runs");
            if (best == null)
            {
                best = strip.Find("Best Runs");
            }
            if (best != null)
            {
                Undo.SetTransformParent(best, strip, "Lay out menus");
                TextMeshProUGUI bestText = best.GetComponent<TextMeshProUGUI>();
                Undo.RecordObject(bestText, "Lay out menus");
                bestText.alignment = TextAlignmentOptions.Left;
                bestText.textWrappingMode = TextWrappingModes.NoWrap;
                bestText.enableAutoSizing = false;
                bestText.fontSize = 17f;
                bestText.color = new Color(0.72f, 0.95f, 1f, 0.95f);

                SerializedObject so = new SerializedObject(best.GetComponent<MenuFootnote>());
                so.FindProperty("plate").objectReferenceValue = strip.gameObject;
                so.ApplyModifiedProperties();
            }

            // The legend, bottom right, level with the bests.
            TextMeshProUGUI legend = Text(screen, "Control Legend", Vector2.zero, new Vector2(800f, 26f), 15f,
                screen.Find("Play/Label").GetComponent<TMP_Text>().font);
            RectTransform legendRect = legend.rectTransform;
            legendRect.anchorMin = legendRect.anchorMax = new Vector2(1f, 0f);
            legendRect.pivot = new Vector2(1f, 0f);
            legendRect.anchoredPosition = new Vector2(-106f, 69f);
            legend.alignment = TextAlignmentOptions.Right;
            legend.color = new Color(0.61f, 0.85f, 0.90f, 0.88f);
            Hint(legend, TitleLegendKeys, TitleLegendPad);
        }

        private static T Ensure<T>(GameObject target) where T : Component
        {
            T found = target.GetComponent<T>();
            return found != null ? found : Undo.AddComponent<T>(target);
        }

        // ---------- Helpers ----------

        /// <summary>The screens' common parent, found by one screen's name.</summary>
        private static Transform FindRoot(string screen)
        {
            foreach (MenuScreen menu in Object.FindObjectsByType<MenuScreen>(FindObjectsInactive.Include))
            {
                if (menu.name == screen)
                {
                    return menu.transform.parent;
                }
            }

            return null;
        }

        private static RectTransform Child(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            Transform found = parent.Find(name);
            RectTransform rect;
            if (found == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Lay out menus");
                go.layer = parent.gameObject.layer;
                rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
            }
            else
            {
                rect = (RectTransform)found;
                Undo.RecordObject(rect, "Lay out menus");
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        /// <summary>A left-aligned line of text from the parent's top left.</summary>
        private static TextMeshProUGUI Text(Transform parent, string name, Vector2 position, Vector2 size,
            float fontSize, TMP_FontAsset font)
        {
            RectTransform rect = Child(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                position, size);
            TextMeshProUGUI text = Ensure<TextMeshProUGUI>(rect.gameObject);
            Undo.RecordObject(text, "Lay out menus");
            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = fontSize;
            text.fontStyle = FontStyles.UpperCase;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>A hairline across a card, centred, at a height from its top.</summary>
        private static void Line(Transform parent, string name, float y, float width)
        {
            RectTransform rect = Child(parent, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(width, 1.5f));
            Image image = Ensure<Image>(rect.gameObject);
            Undo.RecordObject(image, "Lay out menus");
            image.color = Hairline;
            image.raycastTarget = false;
        }

        private static void Hint(TextMeshProUGUI text, string keyboard, string pad)
        {
            text.text = keyboard;
            InputHint hint = Ensure<InputHint>(text.gameObject);
            SerializedObject so = new SerializedObject(hint);
            so.FindProperty("keyboard").stringValue = keyboard;
            so.FindProperty("pad").stringValue = pad;
            so.ApplyModifiedProperties();
        }
    }
}
