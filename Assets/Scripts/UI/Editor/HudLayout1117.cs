using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Lays the run HUD out the 17 November 2026 way (Artifacts/hud-redesign,
    /// block 3), on whatever HUD Rebuild HUD made, without rebuilding it:
    ///
    /// - everything inside a frame clamped to 16:9 with 5.5% margins
    ///   (HudSafeFrame), so 21:9 corners stay over the middle;
    /// - the timer as a small framed box at top centre, the boss bar narrower
    ///   in the same place, the level-up banner under them;
    /// - the hull at bottom centre on its own plate with "18 / 20", Dash docked
    ///   to its left, Deflector and Slow Mo stacked to its right;
    /// - level and experience as a slim chip bottom left.
    ///
    /// Canvas units: 1920x1080 reference, Match 0.5. Safe to run again.
    /// </summary>
    public static class HudLayout1117
    {
        private const string HudName = "HUD (Holo)";
        private const string FrameName = "HUD Frame";
        private const string PlateMaterial = "HudPlate";

        /// <summary>Hull length per hit point: the starting 20 at 270.</summary>
        public const float HullPerPoint = 13.5f;

        /// <summary>The longest the hull grows: 375, reached at 28 hit points.</summary>
        public const float HullMaxWidth = 375f;

        private const float HullHeight = 16f;
        private const float HullPlateHeight = 50f;
        private const float PlatePadding = 12f;
        private const float DockGap = 10f;
        private const float DockWidth = 107f;

        /// <summary>The bars that sit on a plate, by material.</summary>
        public static readonly string[] InPlateBars =
            { "HealthBar", "DashBar", "DeflectorBar", "SlowMoBar", "XPBar", "TimerBar" };

        /// <summary>Fill inset of a bar on a plate, in pixels: thin bars need nearly all of it.</summary>
        private const float InPlateInset = 1.5f;

        private const string VignetteName = "Low Hull Vignette";

        [MenuItem("Survival Chaos/UI/Lay Out HUD (17 Nov)", priority = 144)]
        public static void LayOut()
        {
            string report = Apply();
            Debug.Log(report);
        }

        /// <summary>Lays the HUD out and returns what it did, for evals.</summary>
        public static string Apply()
        {
            Canvas canvas = null;
            foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (candidate.isRootCanvas && candidate.transform.Find(HudName) != null)
                {
                    canvas = candidate;
                    break;
                }
            }

            if (canvas == null)
            {
                return "No canvas with a " + HudName + ": open the Game scene.";
            }

            Transform hud = canvas.transform.Find(HudName);
            Material panelBase = HoloUiFactory.EnsureBaseMaterial("HoloPanel", "Survival Chaos/Holo Panel");
            if (panelBase == null)
            {
                return "No HoloPanel material.";
            }

            RectTransform frame = Child(hud, FrameName);
            if (frame.GetComponent<HudSafeFrame>() == null)
            {
                Undo.AddComponent<HudSafeFrame>(frame.gameObject);
            }

            Set(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                HudSafeFrame.SizeFor(HoloUiFactory.ReferenceResolution, 16f / 9f, 0.055f));
            frame.SetAsFirstSibling();

            int moved = 0;
            moved += Timer(hud, frame, panelBase);
            moved += Boss(hud, frame);
            moved += Banner(hud, frame);
            moved += Hull(hud, frame, panelBase);
            moved += Experience(hud, frame, panelBase);
            moved += Hint(canvas.transform);

            // A bar on a plate is a track and a fill: the plate is its frame,
            // so the bar's own frame line and corner ticks go, and its fill
            // comes out nearly to its edge so a thin bar still shows one.
            foreach (string bar in InPlateBars)
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>(HoloUiFactory.MaterialFolder + "/" + bar + ".mat");
                if (m != null && m.HasProperty("_FrameStrength"))
                {
                    Undo.RecordObject(m, "In-plate bar");
                    m.SetFloat("_FrameStrength", 0f);
                    m.SetFloat("_BracketArm", 0f);
                    m.SetFloat("_Inset", InPlateInset);
                    EditorUtility.SetDirty(m);
                }
            }

            moved += Vignette(hud);
            moved += Flare(hud);
            moved += Fades(hud, frame);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            return "HUD laid out (17 Nov): " + moved + " elements placed.";
        }

        private static int Timer(Transform hud, RectTransform frame, Material panelBase)
        {
            RectTransform bar = Find(hud, "Timer Bar");
            if (bar == null)
            {
                return 0;
            }

            RectTransform group = Child(frame, "Timer");
            Set(group, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(280f, 56f));

            Plate(group, "Timer Plate", panelBase, stretch: true);

            Reparent(bar, group);
            Set(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(252f, 8f));

            RectTransform label = Find(bar, "Timer Label");
            if (label != null)
            {
                Set(label, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(252f, 22f));
                Font(label, 15f);
            }

            // Timer hides this object at the handover, so it has to be the
            // whole box, not just the bar inside it.
            SurvivalChaos.Timer timer = Object.FindAnyObjectByType<SurvivalChaos.Timer>(FindObjectsInactive.Include);
            HoloUiFactory.Assign(timer, "timerBar", group.gameObject);
            return 1;
        }

        private static int Boss(Transform hud, RectTransform frame)
        {
            RectTransform bar = Find(hud, "Boss Bar");
            if (bar == null)
            {
                return 0;
            }

            Reparent(bar, frame);

            // 36% of the 16:9 width, under its name.
            Set(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(690f, 22f));

            RectTransform label = Find(bar, "Boss Label");
            if (label != null)
            {
                Set(label, label.anchorMin, label.pivot, new Vector2(0f, 6f), new Vector2(600f, 22f));
                Font(label, 16f);
            }

            return 1;
        }

        private static int Banner(Transform hud, RectTransform frame)
        {
            RectTransform banner = Find(hud, "Level Up Banner");
            if (banner == null)
            {
                return 0;
            }

            Reparent(banner, frame);

            // Under the top bar, out of the ship's lane.
            Set(banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(420f, 60f));

            RectTransform text = Find(banner, "Level Up Text");
            if (text != null)
            {
                Set(text, text.anchorMin, text.pivot, Vector2.zero, new Vector2(400f, 52f));
                Font(text, 22f);
            }

            return 1;
        }

        private static int Hull(Transform hud, RectTransform frame, Material panelBase)
        {
            RectTransform bar = Find(hud, "Health Bar");
            if (bar == null)
            {
                return 0;
            }

            int start = Mathf.RoundToInt(20f);
            float width = Mathf.Min(start * HullPerPoint, HullMaxWidth);

            RectTransform plate = Plate(frame, "Hull Plate", panelBase, stretch: false);
            Set(plate, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(width + 2f * PlatePadding, HullPlateHeight));

            Reparent(bar, frame);
            bar.SetSiblingIndex(plate.GetSiblingIndex() + 1);
            Set(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(width, HullHeight));

            RectTransform label = Find(hud, "Health Label");
            if (label != null)
            {
                Reparent(label, plate);
                Set(label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(PlatePadding, -5f), new Vector2(140f, 20f));
                Font(label, 15f);
                Align(label, TextAlignmentOptions.Left);
            }

            RectTransform count = Find(plate, "Hull Count");
            if (count == null)
            {
                TextMeshProUGUI made = HoloUiFactory.CreateText(plate, "Hull Count", new Vector2(1f, 1f),
                    new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 15f, TextAlignmentOptions.Right);
                made.text = "<color=#2EE57D>20</color> / 20";
                made.richText = true;
                count = made.rectTransform;
            }

            Set(count, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-PlatePadding, -5f), new Vector2(140f, 20f));

            SurvivalChaos.HealthBar health = bar.GetComponent<SurvivalChaos.HealthBar>();
            if (health != null)
            {
                SerializedObject so = new SerializedObject(health);
                so.FindProperty("pixelsPerHitPoint").floatValue = HullPerPoint;
                so.FindProperty("maxWidth").floatValue = HullMaxWidth;
                so.FindProperty("platePadding").floatValue = PlatePadding;
                so.FindProperty("plate").objectReferenceValue = plate;
                so.FindProperty("count").objectReferenceValue = count.GetComponent<TMP_Text>();
                so.ApplyModifiedProperties();
            }

            Dash(hud, plate, panelBase);
            Gauge(hud, plate, panelBase, "Deflector", 27f);
            Gauge(hud, plate, panelBase, "Slow Mo", 0f);
            return 1;
        }

        /// <summary>Dash, docked to the hull plate's left edge, as tall as the plate.</summary>
        private static void Dash(Transform hud, RectTransform hullPlate, Material panelBase)
        {
            RectTransform plate = Plate(hullPlate, "Dash Plate", panelBase, stretch: false);
            Set(plate, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-DockGap, 0f),
                new Vector2(DockWidth, HullPlateHeight));

            RectTransform label = Find(hud, "Dash Label");
            if (label != null)
            {
                Reparent(label, plate);
                Set(label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -5f), new Vector2(90f, 18f));
                Font(label, 12f);
                Align(label, TextAlignmentOptions.Left);
            }

            RectTransform bar = Find(hud, "Dash Bar");
            if (bar != null)
            {
                Reparent(bar, plate);
                Set(bar, Vector2.zero, Vector2.zero, new Vector2(8f, 9f), new Vector2(DockWidth - 16f, 10f));
            }
        }

        /// <summary>
        /// Deflector or Slow Mo, stacked right of the hull plate. The plate
        /// becomes what its meter shows and hides with the bar, so nothing is
        /// left on screen before the ability is taken.
        /// </summary>
        private static void Gauge(Transform hud, RectTransform hullPlate, Material panelBase, string name, float y)
        {
            RectTransform group = Find(hud, name);
            if (group == null || group.GetComponent<MonoBehaviour>() == null)
            {
                return;
            }

            Reparent(group, hullPlate);
            Set(group, new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(DockGap, y), new Vector2(DockWidth, 23f));

            RectTransform plate = Plate(group, name + " Plate", panelBase, stretch: true);
            plate.SetAsFirstSibling();

            RectTransform label = Find(group, name + " Label");
            if (label != null)
            {
                Reparent(label, plate);
                Set(label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(7f, -2f), new Vector2(95f, 13f));
                Font(label, 10f);
                Align(label, TextAlignmentOptions.Left);
            }

            RectTransform bar = Find(group, name + " Bar");
            if (bar != null)
            {
                bar.SetAsLastSibling();
                Set(bar, Vector2.zero, Vector2.zero, new Vector2(7f, 3f), new Vector2(DockWidth - 14f, 5f));
            }

            // The meter hides its "label" with its bar: point that at the plate.
            foreach (MonoBehaviour meter in group.GetComponents<MonoBehaviour>())
            {
                SerializedObject so = new SerializedObject(meter);
                SerializedProperty property = so.FindProperty("label");
                if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    property.objectReferenceValue = plate.gameObject;
                    so.ApplyModifiedProperties();
                }
            }
        }

        private static int Experience(Transform hud, RectTransform frame, Material panelBase)
        {
            RectTransform bar = Find(hud, "XP Bar");
            if (bar == null)
            {
                return 0;
            }

            RectTransform plate = Plate(frame, "XP Plate", panelBase, stretch: false);
            Set(plate, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(230f, 34f));

            RectTransform level = Find(hud, "Level Text");
            if (level != null)
            {
                Reparent(level, plate);
                Set(level, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(92f, 20f));
                Font(level, 13f);
                Align(level, TextAlignmentOptions.Left);
            }

            Reparent(bar, plate);
            Set(bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(116f, 8f));
            return 1;
        }

        /// <summary>
        /// The low-hull edge vignette: a full-screen Edge Glow under the HUD
        /// root, outside the 16:9 frame so it reaches the real screen edges,
        /// drawn first so the HUD sits on top. HealthBar drives its alpha.
        /// </summary>
        private static int Vignette(Transform hud)
        {
            Shader shader = Shader.Find("Survival Chaos/Edge Glow");
            if (shader == null)
            {
                return 0;
            }

            string path = HoloUiFactory.MaterialFolder + "/LowHullVignette.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "LowHullVignette" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetVector("_Sides", Vector4.one);
            material.SetFloat("_Reach", 0.11f);
            material.SetFloat("_Falloff", 2.6f);
            material.SetFloat("_Spread", 0f);
            EditorUtility.SetDirty(material);

            RectTransform rect = Child(hud, VignetteName);
            Undo.RecordObject(rect, "Vignette");
            HoloUiFactory.Stretch(rect);
            rect.SetAsFirstSibling();

            Image image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = Undo.AddComponent<Image>(rect.gameObject);
            }

            Undo.RecordObject(image, "Vignette");
            image.material = material;
            image.raycastTarget = false;
            // A dark crimson (#5A1825 on screen; the UI writes linear values), so
            // the edges deepen to red rather than wash pink.
            image.color = new Color(0.16f, 0.006f, 0.02f, 0f);
            image.enabled = false;

            SurvivalChaos.HealthBar health = Object.FindAnyObjectByType<SurvivalChaos.HealthBar>(FindObjectsInactive.Include);
            HoloUiFactory.Assign(health, "vignette", image);

            // Amber does not pulse the bar; crimson does, through PulseFloor.
            SurvivalChaos.HoloBar holo = health != null ? health.GetComponent<SurvivalChaos.HoloBar>() : null;
            if (holo != null)
            {
                SerializedObject so = new SerializedObject(holo);
                so.FindProperty("lowThreshold").floatValue = 0f;
                so.ApplyModifiedProperties();
            }

            return 1;
        }

        /// <summary>
        /// The hit flare: a full-screen Edge Glow lit on one edge at a time,
        /// over the vignette and under the HUD, with a small chevron.
        /// </summary>
        private static int Flare(Transform hud)
        {
            Shader shader = Shader.Find("Survival Chaos/Edge Glow");
            if (shader == null)
            {
                return 0;
            }

            string path = HoloUiFactory.MaterialFolder + "/HitFlare.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "HitFlare" };
                AssetDatabase.CreateAsset(material, path);
            }

            // About 150 px at 2560 wide: a narrow band hugging the edge.
            material.SetVector("_Sides", new Vector4(0f, 1f, 0f, 0f));
            material.SetFloat("_Reach", 0.07f);
            material.SetFloat("_Falloff", 2.6f);
            material.SetFloat("_Centre", 0.5f);
            material.SetFloat("_Spread", 0.32f);
            EditorUtility.SetDirty(material);

            RectTransform rect = Child(hud, "Hit Flare");
            Undo.RecordObject(rect, "Hit flare");
            HoloUiFactory.Stretch(rect);

            Transform vignette = hud.Find(VignetteName);
            rect.SetSiblingIndex(vignette != null ? vignette.GetSiblingIndex() + 1 : 0);

            Image image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = Undo.AddComponent<Image>(rect.gameObject);
            }

            Undo.RecordObject(image, "Hit flare");
            image.material = material;
            image.raycastTarget = false;

            // Brighter than the vignette: a hit is a flash, not a state.
            image.color = new Color(0.5f, 0.012f, 0.03f, 0f);
            image.enabled = false;

            RectTransform chevron = (RectTransform)rect.Find("Hit Chevron");
            if (chevron == null)
            {
                TextMeshProUGUI made = HoloUiFactory.CreateText(rect, "Hit Chevron", new Vector2(1f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(-26f, 0f), new Vector2(40f, 60f), 44f,
                    TextAlignmentOptions.Center);
                made.text = ">";
                made.characterSpacing = 0f;
                chevron = made.rectTransform;
            }

            TMP_Text glyph = chevron.GetComponent<TMP_Text>();
            Undo.RecordObject(glyph, "Hit chevron");
            glyph.color = new Color(1f, 0.62f, 0.66f, 0f);
            glyph.enabled = false;

            SurvivalChaos.HitFlare flare = rect.GetComponent<SurvivalChaos.HitFlare>();
            if (flare == null)
            {
                flare = Undo.AddComponent<SurvivalChaos.HitFlare>(rect.gameObject);
            }

            HoloUiFactory.Assign(flare, "chevron", chevron);
            HoloUiFactory.Assign(flare, "player",
                Object.FindAnyObjectByType<SurvivalChaos.Player>(FindObjectsInactive.Include));
            return 1;
        }

        /// <summary>
        /// Block 6, context fades, plus the frame's CanvasGroup the HUD
        /// options' opacity drives.
        /// </summary>
        private static int Fades(Transform hud, RectTransform frame)
        {
            CanvasGroup frameGroup = Group(frame);
            HoloUiFactory.Assign(frame.GetComponent<SurvivalChaos.HudSafeFrame>(), "group", frameGroup);

            int made = 0;

            // Experience: 30% idle, up on a gain or near a level, 2.5 s hold.
            RectTransform xpPlate = Find(hud, "XP Plate");
            Image xp = Find(hud, "XP Bar")?.GetComponent<Image>();
            if (xpPlate != null && xp != null)
            {
                Fade(xpPlate, Group(xpPlate), null, xp, null, 0.3f, 2.5f, 0.15f, 0.8f, false, 0.8f, null);
                made++;
            }

            // Hull: 65% only after 4 s untouched at full; up while damaged and
            // through the boss fight. Graphic by graphic, so the docked
            // abilities (children of the plate) keep their own fades.
            RectTransform hullPlate = Find(hud, "Hull Plate");
            RectTransform hullBar = Find(hud, "Health Bar");
            if (hullPlate != null && hullBar != null)
            {
                var graphics = new System.Collections.Generic.List<Graphic>();
                foreach (string name in new[] { "Hull Plate", "Health Label", "Hull Count", "Health Bar" })
                {
                    Graphic g = Find(hud, name)?.GetComponent<Graphic>();
                    if (g != null)
                    {
                        graphics.Add(g);
                    }
                }

                RectTransform boss = Find(hud, "Boss Bar");
                Fade(hullPlate, null, graphics, null, hullBar.GetComponent<Slider>(), 0.65f, 4f, 0.08f, 0.8f,
                    true, 2f, boss != null ? boss.gameObject : null);
                made++;
            }

            // Abilities: 30% while full and ready, up while draining or
            // recharging and for 0.6 s after.
            foreach (var (holder, bar) in new[] { ("Dash Plate", "Dash Bar"), ("Deflector", "Deflector Bar"),
                         ("Slow Mo", "Slow Mo Bar") })
            {
                RectTransform rect = Find(hud, holder);
                Image image = Find(hud, bar)?.GetComponent<Image>();
                if (rect != null && image != null)
                {
                    Fade(rect, Group(rect), null, image, null, 0.3f, 0.6f, 0.05f, 0.4f, true, 2f, null);
                    made++;
                }
            }

            return made;
        }

        private static CanvasGroup Group(RectTransform rect)
        {
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = Undo.AddComponent<CanvasGroup>(rect.gameObject);
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            return group;
        }

        private static void Fade(RectTransform owner, CanvasGroup group,
            System.Collections.Generic.List<Graphic> graphics, Image fill, Slider slider, float rest, float hold,
            float fadeIn, float fadeOut, bool awakeBelowFull, float awakeAtOrAbove, GameObject awakeWhileActive)
        {
            SurvivalChaos.HudContextFade fade = owner.GetComponent<SurvivalChaos.HudContextFade>();
            if (fade == null)
            {
                fade = Undo.AddComponent<SurvivalChaos.HudContextFade>(owner.gameObject);
            }

            SerializedObject so = new SerializedObject(fade);
            so.FindProperty("slider").objectReferenceValue = slider;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("group").objectReferenceValue = group;

            SerializedProperty list = so.FindProperty("graphics");
            list.arraySize = graphics != null ? graphics.Count : 0;
            for (int i = 0; i < list.arraySize; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = graphics[i];
            }

            so.FindProperty("rest").floatValue = rest;
            so.FindProperty("hold").floatValue = hold;
            so.FindProperty("fadeIn").floatValue = fadeIn;
            so.FindProperty("fadeOut").floatValue = fadeOut;
            so.FindProperty("wakeOnChange").boolValue = true;
            so.FindProperty("awakeBelowFull").boolValue = awakeBelowFull;
            so.FindProperty("awakeAtOrAbove").floatValue = awakeAtOrAbove;
            so.FindProperty("awakeWhileActive").objectReferenceValue = awakeWhileActive;
            so.ApplyModifiedProperties();
        }

        /// <summary>The tutorial line, raised clear of the hull plate.</summary>
        private static int Hint(Transform canvas)
        {
            RectTransform hint = Find(canvas, "Hint Prompt");
            if (hint == null)
            {
                return 0;
            }

            Set(hint, hint.anchorMin, hint.pivot, new Vector2(0f, 135f), hint.sizeDelta);
            return 1;
        }

        private static RectTransform Plate(Transform parent, string name, Material panelBase, bool stretch)
        {
            Transform existing = parent.Find(name);
            RectTransform plate;
            if (existing != null)
            {
                plate = (RectTransform)existing;
            }
            else
            {
                plate = HoloUiFactory.CreatePanel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(100f, 40f), panelBase, HoloUiFactory.PanelFill, PlateMaterial).rectTransform;
                plate.SetAsFirstSibling();
            }

            if (stretch)
            {
                Undo.RecordObject(plate, "Plate");
                HoloUiFactory.Stretch(plate);
            }

            return plate;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return (RectTransform)existing;
            }

            return HoloUiFactory.CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
        }

        private static RectTransform Find(Transform root, string name)
        {
            foreach (RectTransform candidate in root.GetComponentsInChildren<RectTransform>(includeInactive: true))
            {
                if (candidate.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void Reparent(RectTransform rect, Transform parent)
        {
            if (rect.parent != parent)
            {
                Undo.SetTransformParent(rect, parent, "Lay out HUD");
            }
        }

        private static void Set(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            Undo.RecordObject(rect, "Lay out HUD");
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Font(RectTransform rect, float size)
        {
            TMP_Text text = rect.GetComponent<TMP_Text>();
            if (text != null)
            {
                Undo.RecordObject(text, "HUD type size");
                text.fontSize = size;
                text.enableAutoSizing = false;
            }
        }

        private static void Align(RectTransform rect, TextAlignmentOptions align)
        {
            TMP_Text text = rect.GetComponent<TMP_Text>();
            if (text != null)
            {
                Undo.RecordObject(text, "HUD alignment");
                text.alignment = align;
            }
        }
    }
}
