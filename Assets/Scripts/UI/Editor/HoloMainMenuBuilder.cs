using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Rebuilds the title screen: main menu, options and credits.
    ///
    /// The video background, its player and the music are left alone - they are
    /// the scene's content, not its interface. Only the panels on top are
    /// replaced.
    ///
    /// The credits are copied out of the existing screen rather than written
    /// here. They list real people and their addresses, and retyping that into
    /// source is how a name ends up misspelled in a shipped build.
    /// </summary>
    public static class HoloMainMenuBuilder
    {
        private const string RootName = "Title (Holo)";

        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        /// <summary>Back buttons on the sub-screens; the title list sizes its own.</summary>
        private static readonly Vector2 ButtonSize = new Vector2(420f, 72f);

        /// <summary>Old panels. MainMenu sits on MenuPrincipal, so it moves first.</summary>
        private static readonly string[] OldScreens = { "MenuPrincipal", "MenuOpcoes", "Credits" };

        [MenuItem("Survival Chaos/UI/Rebuild Main Menu", priority = 22)]
        public static void RebuildMainMenu()
        {
            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("No canvas",
                    "Open the Menu scene first - this needs a Canvas to build into.", "OK");
                return;
            }

            // The mirror of the check in HoloMenuBuilder: a scene with a run in
            // it is the game, not the title screen.
            if (Object.FindAnyObjectByType<WaveDirector>(FindObjectsInactive.Include) != null)
            {
                EditorUtility.DisplayDialog("Wrong scene",
                    "This scene has a WaveDirector, so it is the game scene. The title screen " +
                    "belongs in Menu.unity.\n\nFor the in-game screens, use Rebuild Menus instead.",
                    "OK");
                return;
            }

            Material panelMaterial = HoloUiFactory.EnsureBaseMaterial("HoloPanel", "Survival Chaos/Holo Panel");
            Material barMaterial = HoloUiFactory.EnsureBaseMaterial("HoloBar", "Survival Chaos/Holo Bar");
            if (panelMaterial == null || barMaterial == null)
            {
                return;
            }

            // Read before anything is destroyed.
            string credits = CaptureCredits(canvas.transform);

            ConfigureCanvas(canvas);
            GameObject root = HoloUiFactory.ReplaceRoot(canvas.transform, RootName);

            // MainMenu lives on one of the panels about to go, so it is recreated
            // on the Canvas before any button is wired to it.
            MainMenu mainMenu = canvas.GetComponent<MainMenu>();
            if (mainMenu == null)
            {
                mainMenu = Undo.AddComponent<MainMenu>(canvas.gameObject);
            }

            // Sub-screens first, so the title screen can point its entries at them.
            GameObject audio = BuildScreen(root.transform, "Audio Screen", panelMaterial,
                new Vector2(780f, 660f), "Audio");
            GameObject creditsScreen = BuildScreen(root.transform, "Credits Screen", panelMaterial,
                new Vector2(900f, 620f), "Credits");

            GameObject graphics = BuildScreen(root.transform, "Graphics Screen", panelMaterial,
                HoloUiFactory.GraphicsPanelSize, "Graphics");
            HoloUiFactory.PopulateGraphicsPanel(PanelOf(graphics), panelMaterial);

            // Options is a hub here too. Audio and graphics want very different
            // layouts - four sliders against eleven cyclers in two columns - and
            // sharing one panel meant one of them was always the wrong shape.
            GameObject options = BuildScreen(root.transform, "Options Screen", panelMaterial,
                new Vector2(640f, 460f), "Options");
            GameObject title = BuildTitleScreen(root.transform, mainMenu, options, creditsScreen);

            Button toAudio = HoloUiFactory.CreateButton(PanelOf(options), "Audio",
                new Vector2(0.5f, 1f), Centre, new Vector2(0f, -160f), ButtonSize,
                panelMaterial, "Audio", 24f);
            UnityEventTools.AddVoidPersistentListener(toAudio.onClick,
                new UnityAction(audio.GetComponent<MenuScreen>().Show));

            Button toGraphics = HoloUiFactory.CreateButton(PanelOf(options), "Graphics",
                new Vector2(0.5f, 1f), Centre, new Vector2(0f, -250f), ButtonSize,
                panelMaterial, "Graphics", 24f);
            UnityEventTools.AddVoidPersistentListener(toGraphics.onClick,
                new UnityAction(graphics.GetComponent<MenuScreen>().Show));

            // The hub returns to the title; both sub-screens return to the hub.
            AddBack(PanelOf(options), panelMaterial, title, -340f);
            BuildOptions(audio, panelMaterial, barMaterial, options);

            Button graphicsBack = HoloUiFactory.CreateButton(PanelOf(graphics), "Back",
                new Vector2(0.5f, 1f), Centre, new Vector2(0f, HoloUiFactory.GraphicsBackY),
                ButtonSize, panelMaterial, "Back", 24f);
            UnityEventTools.AddVoidPersistentListener(graphicsBack.onClick,
                new UnityAction(options.GetComponent<MenuScreen>().Show));
            BuildCredits(creditsScreen, panelMaterial, title, credits);

            // The title screen is the one the player arrives at, so unlike the
            // in-game menus it starts visible.
            title.SetActive(true);

            int removed = RetireOldScreens(canvas.transform, root);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = root;

            Debug.Log("Holo title screen built, " + removed + " old panels removed. " +
                      "The video background, its player and the music were left untouched. " +
                      "Ctrl+Z reverts everything.", root);
        }

        private static Canvas FindCanvas()
        {
            foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (candidate.isRootCanvas && candidate.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void ConfigureCanvas(Canvas canvas)
        {
            Undo.RecordObject(canvas, "Configure canvas");
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            EditorUtility.SetDirty(canvas);

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            }

            Undo.RecordObject(scaler, "Configure canvas scaler");
            // This scene had the same Constant Pixel Size setting as the game
            // scene, so its reference resolution was being ignored too.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HoloUiFactory.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(scaler);
        }

        /// <summary>
        /// Lifts the credits out of whatever currently displays them. Picks the
        /// longest run of text on the canvas, which is the credit roll by a wide
        /// margin - every other label is a single word.
        /// </summary>
        private static string CaptureCredits(Transform canvas)
        {
            string longest = string.Empty;

            foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                if (text.text != null && text.text.Length > longest.Length)
                {
                    longest = text.text;
                }
            }

            foreach (Text text in canvas.GetComponentsInChildren<Text>(includeInactive: true))
            {
                if (text.text != null && text.text.Length > longest.Length)
                {
                    longest = text.text;
                }
            }

            if (longest.Length < 40)
            {
                Debug.LogWarning("No credits text found to carry over - the new credits screen will " +
                                 "need filling in by hand.");
                return "Credits";
            }

            return longest;
        }

        /// <summary>
        /// A sub-screen: framed panel over a dimmed background. Only options and
        /// credits are built this way - the title screen is a different shape of
        /// thing entirely, and is built by BuildTitleScreen.
        /// </summary>
        private static GameObject BuildScreen(Transform parent, string name, Material panelMaterial,
            Vector2 panelSize, string title)
        {
            GameObject screen = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(screen, "Build title screen");
            RectTransform rect = (RectTransform)screen.transform;
            rect.SetParent(parent, false);
            HoloUiFactory.Stretch(rect);

            // These two dim the video behind them so their text stays readable.
            Image scrim = Undo.AddComponent<Image>(screen);
            scrim.color = HoloUiFactory.Scrim;
            scrim.raycastTarget = true;

            Undo.AddComponent<MenuScreen>(screen);

            Image panel = HoloUiFactory.CreatePanel(rect, "Panel", Centre, Centre,
                Vector2.zero, panelSize, panelMaterial, HoloUiFactory.PanelFill, "HoloMenuPanel");

            TextMeshProUGUI heading = HoloUiFactory.CreateText(panel.transform, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                new Vector2(panelSize.x - 60f, 70f), 46f, TextAlignmentOptions.Center);
            heading.text = title;

            screen.SetActive(false);
            return screen;
        }

        private static Transform PanelOf(GameObject screen)
        {
            return screen.transform.Find("Panel");
        }

        /// <summary>
        /// The title screen: a column of entries in the lower left, and nothing
        /// else.
        ///
        /// No panel, no scrim and no title text. The video already renders the
        /// game's name across the middle of the screen, so a centred box carrying
        /// a second copy of it both covered the artwork and repeated it. Sitting
        /// the list down one side is the usual arrangement for exactly that
        /// reason - it leaves the key art as the thing the player looks at.
        /// </summary>
        private static GameObject BuildTitleScreen(Transform parent, MainMenu mainMenu,
            GameObject options, GameObject credits)
        {
            GameObject screen = new GameObject("Title Screen", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(screen, "Build title screen");
            RectTransform rect = (RectTransform)screen.transform;
            rect.SetParent(parent, false);
            HoloUiFactory.Stretch(rect);
            Undo.AddComponent<MenuScreen>(screen);

            Button play = AddEntry(rect, "Play", "Play", 0);
            UnityEventTools.AddVoidPersistentListener(play.onClick, new UnityAction(mainMenu.Jogar));

            Button optionsEntry = AddEntry(rect, "Options", "Options", 1);
            UnityEventTools.AddVoidPersistentListener(optionsEntry.onClick,
                new UnityAction(options.GetComponent<MenuScreen>().Show));

            Button creditsEntry = AddEntry(rect, "Credits", "Credits", 2);
            UnityEventTools.AddVoidPersistentListener(creditsEntry.onClick,
                new UnityAction(credits.GetComponent<MenuScreen>().Show));

            Button quit = AddEntry(rect, "Quit", "Quit", 3);
            UnityEventTools.AddVoidPersistentListener(quit.onClick, new UnityAction(mainMenu.Sair));

            screen.SetActive(false);
            return screen;
        }

        /// <summary>
        /// Places one entry in the lower-left column, counting downward from the
        /// top of the stack. Anchored to the bottom-left corner so the column
        /// stays put on any aspect ratio rather than drifting with the centre.
        /// </summary>
        private static Button AddEntry(Transform parent, string name, string label, int row)
        {
            const float ColumnLeft = 150f;
            const float ColumnTop = 380f;
            const float EntryStep = 76f;

            return HoloUiFactory.CreateMenuEntry(parent, name, Vector2.zero, new Vector2(0f, 0.5f),
                new Vector2(ColumnLeft, ColumnTop - row * EntryStep), new Vector2(420f, 60f),
                label, 30f);
        }

        private static void BuildOptions(GameObject screen, Material panelMaterial,
            Material barMaterial, GameObject title)
        {
            Transform panel = PanelOf(screen);

            // The same four rows the pause screen shows, from the same factory
            // and reading the same channels — a level set here is already in
            // force by the time the game scene loads.
            HoloUiFactory.CreateVolumeRow(panel, AudioChannel.Master, "Master", -170f, barMaterial);
            HoloUiFactory.CreateVolumeRow(panel, AudioChannel.Music, "Music", -280f, barMaterial);
            HoloUiFactory.CreateVolumeRow(panel, AudioChannel.Sfx, "Effects", -390f, barMaterial);
            HoloUiFactory.CreateVolumeRow(panel, AudioChannel.Ui, "Interface", -500f, barMaterial);

            AddBack(panel, panelMaterial, title, -580f);
        }

        private static void BuildCredits(GameObject screen, Material panelMaterial,
            GameObject title, string credits)
        {
            Transform panel = PanelOf(screen);

            TextMeshProUGUI text = HoloUiFactory.CreateText(panel, "Credits Text",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f),
                new Vector2(800f, 340f), 20f, TextAlignmentOptions.TopLeft);
            text.text = credits;

            // Left as authored: names and addresses are not decoration, and the
            // uppercasing and wide tracking used elsewhere would mangle an email.
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 0f;
            text.textWrappingMode = TextWrappingModes.Normal;

            AddBack(panel, panelMaterial, title, -580f);
        }

        private static void AddBack(Transform panel, Material panelMaterial, GameObject title, float y)
        {
            Button back = HoloUiFactory.CreateButton(panel, "Back", new Vector2(0.5f, 1f), Centre,
                new Vector2(0f, y), ButtonSize, panelMaterial, "Back", 24f);

            // Returns to the title screen rather than just closing, which would
            // leave the player looking at the video with no way back in.
            UnityEventTools.AddVoidPersistentListener(back.onClick,
                new UnityAction(title.GetComponent<MenuScreen>().Show));
        }

        /// <summary>
        /// Deletes the old panels. Safe because MainMenu - the only script that
        /// lived on one of them - has already been recreated on the Canvas, and
        /// the old buttons referencing it go with them.
        /// </summary>
        private static int RetireOldScreens(Transform canvas, GameObject root)
        {
            int removed = 0;
            List<Transform> doomed = new List<Transform>();

            foreach (Transform candidate in canvas.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (candidate.IsChildOf(root.transform))
                {
                    continue;
                }

                foreach (string name in OldScreens)
                {
                    if (candidate.gameObject.name == name && !doomed.Contains(candidate))
                    {
                        doomed.Add(candidate);
                    }
                }
            }

            foreach (Transform old in doomed)
            {
                if (old != null)
                {
                    Undo.DestroyObjectImmediate(old.gameObject);
                    removed++;
                }
            }

            return removed;
        }
    }
}
