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

            // Written here since 2 October 2026. It used to be lifted out of
            // the screen being replaced, which was the 2023 jam's own text.
            string credits = CreditsText;

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
            GameObject creditsScreen = BuildScreen(root.transform, "Credits Screen", panelMaterial,
                new Vector2(900f, 620f), "Credits");

            // Options is four tabs, each its own screen, and every one of them
            // backs out to the title. The strip, the rows and the Back buttons
            // come from the factory the pause screen shares.
            GameObject[] tabs = new GameObject[HoloUiFactory.OptionTabNames.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i] = BuildScreen(root.transform, HoloUiFactory.OptionTabNames[i] + " Screen",
                    panelMaterial, HoloUiFactory.OptionsPanelSize, null);
            }

            GameObject title = BuildTitleScreen(root.transform, mainMenu, creditsScreen);
            OptionsTabs options = HoloUiFactory.BuildOptionsTabs(tabs, panelMaterial, barMaterial, title);

            // Wired now rather than in BuildTitleScreen: the tabs need the title
            // screen to exist first, for their Back buttons.
            Button optionsEntry = HoloUiFactory.Find<Button>(title.transform, "Options");
            if (optionsEntry != null)
            {
                UnityEventTools.AddVoidPersistentListener(optionsEntry.onClick,
                    new UnityAction(options.ShowRemembered));
            }

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
        /// The credits. The team that made the game in 2023 comes first, as it
        /// stood on their own screen, without the personal addresses that
        /// screen carried. Then everyone whose work the rebuild uses, following
        /// ThirdPartyNotices.txt, which ships beside the exe and is the place
        /// the licences themselves are. A new sound pack, font or library
        /// wants a line here as well as there.
        ///
        /// The NVIDIA line is the attribution its DLSS licence asks a game's
        /// credits to carry. The NASA line is for the map the storm sky's moon
        /// is painted from (StormSkyBuilder): public domain, and NASA asks
        /// for credit where it can be given. Plain ASCII apart from the two names: the font
        /// atlas is Extended ASCII, and a glyph outside it draws as a box.
        /// </summary>
        public const string CreditsText =
            "DEVELOPMENT TEAM\n\n" +
            "Samuel Silva | Game Design\n" +
            "Lucca To\u00E9 | Tech Art\n" +
            "Maria Fernanda | Game Artist\n" +
            "Ian Barbosa | Main Programmer\n" +
            "Fabr\u00EDcio Frade | UI Programmer\n" +
            "Luis Rocha | Programmer\n\n\n" +
            "MUSIC\n\n" +
            "Darbuka Delight, Chase and Neon Hyperdrive\n" +
            "by Adiutorium, from OpenGameArt.org (CC0)\n\n\n" +
            "SOUND\n\n" +
            "Kenney | Interface Sounds and Sci-fi Sounds (CC0)\n" +
            "Bluezone Corporation | weapon sounds, from the Sonniss GDC bundle\n\n\n" +
            "VISUAL EFFECTS\n\n" +
            "Big Rook Games | effects pack\n" +
            "Unity Technologies | Particle Pack\n\n\n" +
            "SKY\n\n" +
            "The Moon's surface | NASA's Scientific Visualization Studio,\n" +
            "CGI Moon Kit, from Lunar Reconnaissance Orbiter data\n\n\n" +
            "FONTS\n\n" +
            "Chakra Petch | The Chakra Petch Project Authors\n" +
            "Liberation Sans | Google and Red Hat\n" +
            "Both under the SIL Open Font License 1.1\n\n\n" +
            "TECHNOLOGY\n\n" +
            "AMD FidelityFX Super Resolution\n" +
            "Copyright (C) Advanced Micro Devices, Inc.\n\n" +
            "NVIDIA DLSS\n" +
            "Copyright 2018 - 2024 NVIDIA Corporation. NVIDIA and DLSS are\n" +
            "trademarks and/or registered trademarks of NVIDIA Corporation\n" +
            "in the U.S. and other countries.\n\n" +
            "Made with Unity\n\n\n" +
            "The licences are in ThirdPartyNotices.txt, beside the game.";

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

            // A null title is an options tab, whose strip of tabs is its title.
            if (title != null)
            {
                TextMeshProUGUI heading = HoloUiFactory.CreateText(panel.transform, "Title",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                    new Vector2(panelSize.x - 60f, 70f), 46f, TextAlignmentOptions.Center);
                heading.text = title;
            }

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
            GameObject credits)
        {
            GameObject screen = new GameObject("Title Screen", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(screen, "Build title screen");
            RectTransform rect = (RectTransform)screen.transform;
            rect.SetParent(parent, false);
            HoloUiFactory.Stretch(rect);
            Undo.AddComponent<MenuScreen>(screen);

            Button play = AddEntry(rect, "Play", "Play", 0);
            UnityEventTools.AddVoidPersistentListener(play.onClick, new UnityAction(mainMenu.Jogar));

            // Wired by the caller once the option tabs exist.
            AddEntry(rect, "Options", "Options", 1);

            Button creditsEntry = AddEntry(rect, "Credits", "Credits", 2);
            UnityEventTools.AddVoidPersistentListener(creditsEntry.onClick,
                new UnityAction(credits.GetComponent<MenuScreen>().Show));

            Button quit = AddEntry(rect, "Quit", "Quit", 3);
            UnityEventTools.AddVoidPersistentListener(quit.onClick, new UnityAction(mainMenu.Sair));

            // Under the entries, in their column: the player's bests, hidden
            // until there are any. Then which build this is, in the corner.
            HoloUiFactory.CreateFootnote(rect, "Best Runs", MenuFootnote.Content.Bests, Vector2.zero,
                new Vector2(150f, 70f), new Vector2(1100f, 30f), 18f, TextAlignmentOptions.BottomLeft, 0.7f);
            HoloUiFactory.AddBuildStamp(rect);

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

        private static void BuildCredits(GameObject screen, Material panelMaterial,
            GameObject title, string credits)
        {
            Transform panel = PanelOf(screen);

            // A window the text scrolls inside: the roll is several times the
            // panel's height since it began naming everyone the rebuild uses.
            RectTransform window = HoloUiFactory.CreateRect(panel, "Credits Window",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f),
                new Vector2(800f, 400f));
            Undo.AddComponent<RectMask2D>(window.gameObject);

            TextMeshProUGUI text = HoloUiFactory.CreateText(window, "Credits Text",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(800f, 400f), 20f, TextAlignmentOptions.TopLeft);
            text.text = credits;

            // Left as authored: names are not decoration, and the uppercasing
            // and wide tracking used elsewhere would shout a list of them.
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 0f;
            text.textWrappingMode = TextWrappingModes.Normal;

            // Its height is set by CreditsRoll when the screen opens. The
            // screen is inactive while it is built, and an inactive text
            // cannot be measured: it reports the height of five lines for
            // forty-nine.
            RectTransform textRect = text.rectTransform;

            CreditsRoll roll = Undo.AddComponent<CreditsRoll>(window.gameObject);
            HoloUiFactory.Assign(roll, "content", textRect);

            TextMeshProUGUI hint = HoloUiFactory.CreateText(panel, "Credits Hint",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -538f),
                new Vector2(800f, 24f), 15f, TextAlignmentOptions.Center);
            hint.text = "Up and down, or the wheel, to scroll";
            Color quiet = HoloUiFactory.Edge;
            quiet.a = 0.6f;
            hint.color = quiet;

            AddBack(panel, panelMaterial, title, -580f);
        }

        private static void AddBack(Transform panel, Material panelMaterial, GameObject target, float y)
        {
            Button back = HoloUiFactory.CreateButton(panel, "Back", new Vector2(0.5f, 1f), Centre,
                new Vector2(0f, y), ButtonSize, panelMaterial, "Back", 24f);

            // Opens the screen behind this one rather than just closing, which
            // would leave the player looking at the video with no way back in.
            UnityEventTools.AddVoidPersistentListener(back.onClick,
                new UnityAction(target.GetComponent<MenuScreen>().Show));

            // The panel passed in is the screen's child, so the component this
            // belongs on is one level up.
            HoloUiFactory.SetPrevious(panel.parent == null ? null : panel.parent.gameObject, target);
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
