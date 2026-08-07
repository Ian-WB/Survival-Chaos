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
    /// Builds the pause, options, death and victory screens, plus the tutorial
    /// prompt, and retires the old ones.
    ///
    /// The old screens are identified by asking the scripts which objects they
    /// point at, rather than by matching names. Names drift and get duplicated;
    /// PauseMenu.pauseMenuUI is by definition the pause screen.
    /// </summary>
    public static class HoloMenuBuilder
    {
        private const string RootName = "Menus (Holo)";

        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 ButtonSize = new Vector2(420f, 72f);
        private const float ButtonStep = 88f;

        [MenuItem("Survival Chaos/UI/Rebuild Menus", priority = 21)]
        public static void RebuildMenus()
        {
            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("No canvas",
                    "Open the Game scene first - this needs a Canvas to build into.", "OK");
                return;
            }

            // These are the in-game screens, so they belong in the scene that has
            // a run to pause. Without this check the tool happily builds a second
            // options screen into the title scene, which is exactly what happened.
            if (Object.FindAnyObjectByType<WaveDirector>(FindObjectsInactive.Include) == null)
            {
                EditorUtility.DisplayDialog("Wrong scene",
                    "This scene has no WaveDirector, so it is not the game scene. The pause, death " +
                    "and victory screens belong in Game.unity.\n\nFor the title screen, use " +
                    "Rebuild Main Menu instead.", "OK");
                return;
            }

            Material panelMaterial = HoloUiFactory.EnsureBaseMaterial("HoloPanel", "Survival Chaos/Holo Panel");
            Material barMaterial = HoloUiFactory.EnsureBaseMaterial("HoloBar", "Survival Chaos/Holo Bar");
            if (panelMaterial == null || barMaterial == null)
            {
                return;
            }

            // Collected before anything is rebuilt, while the references still
            // point at the old screens.
            List<GameObject> retired = CollectOldScreens();

            GameObject root = HoloUiFactory.ReplaceRoot(canvas.transform, RootName);

            MainMenu mainMenu = EnsureComponent<MainMenu>(canvas.gameObject);
            PauseMenu pause = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);

            GameObject graphics = BuildGraphics(root.transform, panelMaterial);
            GameObject display = BuildDisplay(root.transform, panelMaterial);
            GameObject audio = BuildOptions(root.transform, panelMaterial, barMaterial);
            GameObject options = BuildOptionsHub(root.transform, panelMaterial, audio, display, graphics);
            WireSubScreenBacks(options, audio, display, graphics, panelMaterial);
            GameObject paused = BuildPause(root.transform, panelMaterial, mainMenu, pause, options);
            GameObject death = BuildOutcome(root.transform, panelMaterial, mainMenu,
                "Death Screen", "Ship Lost", HoloUiFactory.Loss);
            GameObject victory = BuildOutcome(root.transform, panelMaterial, mainMenu,
                "Victory Screen", "Leviathan Down", HoloUiFactory.Health);
            GameObject prompt = BuildTutorialPrompt(root.transform, panelMaterial);

            WireBack(options, paused);

            int wired = Rewire(canvas, paused, options, death, victory, prompt);
            int removed = Retire(retired, root) + SweepLeftovers(canvas.transform, root);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = root;

            Debug.Log("Holo menus built. " + wired + " references repointed, " + removed +
                      " old screens removed. Ctrl+Z reverts everything.", root);
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
        /// Asks each screen's own script which GameObject it shows. Anything still
        /// referenced when this runs is by definition part of the old interface.
        /// </summary>
        private static List<GameObject> CollectOldScreens()
        {
            List<GameObject> found = new List<GameObject>();

            foreach (PauseMenu menu in Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Include))
            {
                Add(found, menu.pauseMenuUI);
                Add(found, menu.optionsUI);
            }

            foreach (DeathMenu menu in Object.FindObjectsByType<DeathMenu>(FindObjectsInactive.Include))
            {
                Add(found, menu.deathMenuUI);
            }

            foreach (VictoryMenu menu in Object.FindObjectsByType<VictoryMenu>(FindObjectsInactive.Include))
            {
                SerializedObject so = new SerializedObject(menu);
                Add(found, so.FindProperty("victoryMenuUI").objectReferenceValue as GameObject);
            }

            foreach (Tutorial tutorial in Object.FindObjectsByType<Tutorial>(FindObjectsInactive.Include))
            {
                Add(found, tutorial.shiftTutorial);
            }

            return found;
        }

        private static void Add(List<GameObject> list, GameObject candidate)
        {
            if (candidate != null && !list.Contains(candidate))
            {
                list.Add(candidate);
            }
        }

        private static T EnsureComponent<T>(GameObject host) where T : Component
        {
            T existing = host.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(host);
        }

        /// <summary>
        /// A screen: a full-screen scrim that also swallows clicks, and a framed
        /// panel in the middle. The scrim matters as much as the panel - without
        /// it the game reads through the menu and stays visually busy while
        /// stopped.
        /// </summary>
        private static GameObject BuildScreen(Transform parent, string name, Material panelMaterial,
            Vector2 panelSize, string title, Color titleColor)
        {
            GameObject screen = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(screen, "Build holo menus");
            RectTransform rect = (RectTransform)screen.transform;
            rect.SetParent(parent, false);
            HoloUiFactory.Stretch(rect);

            Image scrim = Undo.AddComponent<Image>(screen);
            scrim.color = HoloUiFactory.Scrim;
            // Blocks clicks reaching whatever is behind the menu.
            scrim.raycastTarget = true;

            // Makes this screen close the others whenever it opens, however it
            // was opened. Two scrims and two panels at once is otherwise the
            // result, which is exactly what went wrong the first time.
            Undo.AddComponent<MenuScreen>(screen);

            Image panel = HoloUiFactory.CreatePanel(rect, "Panel", Centre, Centre,
                Vector2.zero, panelSize, panelMaterial, HoloUiFactory.PanelFill, "HoloMenuPanel");

            TextMeshProUGUI heading = HoloUiFactory.CreateText(panel.transform, "Title", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(panelSize.x - 80f, 60f),
                42f, TextAlignmentOptions.Center);
            heading.text = title;
            heading.color = titleColor;

            screen.SetActive(false);
            return screen;
        }

        private static Transform PanelOf(GameObject screen)
        {
            return screen.transform.Find("Panel");
        }

        private static Button AddButton(Transform panel, Material panelMaterial, string name,
            string label, int row)
        {
            return HoloUiFactory.CreateButton(panel, name, new Vector2(0.5f, 1f), Centre,
                new Vector2(0f, -150f - row * ButtonStep), ButtonSize, panelMaterial, label, 24f);
        }

        private static GameObject BuildPause(Transform parent, Material panelMaterial,
            MainMenu mainMenu, PauseMenu pause, GameObject options)
        {
            GameObject screen = BuildScreen(parent, "Pause Screen", panelMaterial,
                new Vector2(560f, 560f), "Paused", HoloUiFactory.Edge);
            Transform panel = PanelOf(screen);

            Button resume = AddButton(panel, panelMaterial, "Resume", "Resume", 0);
            if (pause != null)
            {
                UnityEventTools.AddVoidPersistentListener(resume.onClick, new UnityAction(pause.Resume));
            }

            Button optionsButton = AddButton(panel, panelMaterial, "Options", "Options", 1);
            // Show, not SetActive: opening options has to close this screen too.
            UnityEventTools.AddVoidPersistentListener(optionsButton.onClick,
                new UnityAction(options.GetComponent<MenuScreen>().Show));

            Button toMenu = AddButton(panel, panelMaterial, "Main Menu", "Main Menu", 2);
            UnityEventTools.AddVoidPersistentListener(toMenu.onClick, new UnityAction(mainMenu.MenuPrincipal));

            Button quit = AddButton(panel, panelMaterial, "Quit", "Quit", 3);
            UnityEventTools.AddVoidPersistentListener(quit.onClick, new UnityAction(mainMenu.Sair));

            return screen;
        }

        private static GameObject BuildOptions(Transform parent, Material panelMaterial, Material barMaterial)
        {
            GameObject screen = BuildScreen(parent, "Audio Screen", panelMaterial,
                new Vector2(780f, 660f), "Audio", HoloUiFactory.Edge);
            Transform panel = PanelOf(screen);

            AddVolumeRows(panel, barMaterial);
            return screen;
        }

        /// <summary>
        /// What is in the image: the quality preset and the per-effect overrides
        /// on top of it. How the image is produced lives on the display screen.
        /// </summary>
        private static GameObject BuildGraphics(Transform parent, Material panelMaterial)
        {
            GameObject screen = BuildScreen(parent, "Graphics Screen", panelMaterial,
                HoloUiFactory.GraphicsPanelSize, "Graphics", HoloUiFactory.Edge);

            HoloUiFactory.PopulateGraphicsPanel(PanelOf(screen), panelMaterial);
            return screen;
        }

        /// <summary>
        /// How the image is produced and presented: what the monitor is asked for,
        /// and how the frame is reconstructed to fill it.
        ///
        /// Split out from graphics because the two answer different questions. A
        /// player picking a resolution is fitting the game to their hardware; one
        /// turning off motion blur is expressing a preference. Mixed together they
        /// made a twelve-row panel with no order anyone could predict.
        /// </summary>
        private static GameObject BuildDisplay(Transform parent, Material panelMaterial)
        {
            GameObject screen = BuildScreen(parent, "Display Screen", panelMaterial,
                HoloUiFactory.DisplayPanelSize, "Display", HoloUiFactory.Edge);

            HoloUiFactory.PopulateDisplayPanel(PanelOf(screen), panelMaterial);
            return screen;
        }

        /// <summary>
        /// Options is a hub rather than a screen of its own settings.
        ///
        /// The three sections want genuinely different layouts - four sliders,
        /// two columns of cyclers, one column of switches. Sharing one panel meant
        /// at least one of them was always the wrong shape.
        /// </summary>
        private static GameObject BuildOptionsHub(Transform parent, Material panelMaterial,
            GameObject audio, GameObject display, GameObject graphics)
        {
            GameObject screen = BuildScreen(parent, "Options Screen", panelMaterial,
                new Vector2(640f, 540f), "Options", HoloUiFactory.Edge);
            Transform panel = PanelOf(screen);

            Button toAudio = HoloUiFactory.CreateButton(panel, "Audio", new Vector2(0.5f, 1f),
                Centre, new Vector2(0f, -160f), ButtonSize, panelMaterial, "Audio", 24f);
            UnityEventTools.AddVoidPersistentListener(toAudio.onClick,
                new UnityAction(audio.GetComponent<MenuScreen>().Show));

            Button toDisplay = HoloUiFactory.CreateButton(panel, "Display", new Vector2(0.5f, 1f),
                Centre, new Vector2(0f, -250f), ButtonSize, panelMaterial, "Display", 24f);
            UnityEventTools.AddVoidPersistentListener(toDisplay.onClick,
                new UnityAction(display.GetComponent<MenuScreen>().Show));

            Button toGraphics = HoloUiFactory.CreateButton(panel, "Graphics", new Vector2(0.5f, 1f),
                Centre, new Vector2(0f, -340f), ButtonSize, panelMaterial, "Graphics", 24f);
            UnityEventTools.AddVoidPersistentListener(toGraphics.onClick,
                new UnityAction(graphics.GetComponent<MenuScreen>().Show));

            // Named "Back" because WireBack finds it that way once the pause
            // screen exists and can be pointed at.
            HoloUiFactory.CreateButton(panel, "Back", new Vector2(0.5f, 1f), Centre,
                new Vector2(0f, -430f), ButtonSize, panelMaterial, "Back", 24f);

            return screen;
        }

        /// <summary>
        /// Sends every sub-screen back to the hub. Done after they all exist,
        /// because each needs to name another.
        /// </summary>
        private static void WireSubScreenBacks(GameObject hub, GameObject audio, GameObject display,
            GameObject graphics, Material panelMaterial)
        {
            MenuScreen target = hub.GetComponent<MenuScreen>();

            AddBackTo(audio, target, -580f, panelMaterial);
            AddBackTo(display, target, HoloUiFactory.DisplayBackY, panelMaterial);
            AddBackTo(graphics, target, HoloUiFactory.GraphicsBackY, panelMaterial);
        }

        private static void AddBackTo(GameObject screen, MenuScreen target, float y,
            Material panelMaterial)
        {
            Button back = HoloUiFactory.CreateButton(PanelOf(screen), "Back",
                new Vector2(0.5f, 1f), Centre, new Vector2(0f, y), ButtonSize,
                panelMaterial, "Back", 24f);
            UnityEventTools.AddVoidPersistentListener(back.onClick, new UnityAction(target.Show));
            HoloUiFactory.SetPrevious(screen, target.gameObject);
        }

        /// <summary>
        /// The four channels, in the order they make sense to reach for: the one
        /// that governs everything, then the two most people actually want to
        /// balance, then menu sound.
        ///
        /// Labelled for players rather than for the mixer — "Effects" and
        /// "Interface" say what is being turned down; "SFX" and "UI" say how the
        /// code is organised.
        /// </summary>
        private static void AddVolumeRows(Transform panel, Material barMaterial)
        {
            HoloUiFactory.CreateVolumeRow(panel, AudioChannel.Master, "Master", -170f, barMaterial);
            HoloUiFactory.CreateVolumeRow(panel, AudioChannel.Music, "Music", -280f, barMaterial);
            HoloUiFactory.CreateVolumeRow(panel, AudioChannel.Sfx, "Effects", -390f, barMaterial);
            HoloUiFactory.CreateVolumeRow(panel, AudioChannel.Ui, "Interface", -500f, barMaterial);
        }

        /// <summary>Death and victory differ only in wording and colour.</summary>
        private static GameObject BuildOutcome(Transform parent, Material panelMaterial,
            MainMenu mainMenu, string name, string title, Color titleColor)
        {
            GameObject screen = BuildScreen(parent, name, panelMaterial,
                new Vector2(620f, 420f), title, titleColor);
            Transform panel = PanelOf(screen);

            Button restart = AddButton(panel, panelMaterial, "Restart", "Try Again", 0);
            // Jogar reloads the Game scene and resets timeScale, which is exactly
            // what restarting from a stopped run needs.
            UnityEventTools.AddVoidPersistentListener(restart.onClick, new UnityAction(mainMenu.Jogar));

            Button toMenu = AddButton(panel, panelMaterial, "Main Menu", "Main Menu", 1);
            UnityEventTools.AddVoidPersistentListener(toMenu.onClick, new UnityAction(mainMenu.MenuPrincipal));

            return screen;
        }

        private static GameObject BuildTutorialPrompt(Transform parent, Material panelMaterial)
        {
            Image panel = HoloUiFactory.CreatePanel(parent, "Shift Prompt", new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(520f, 76f),
                panelMaterial, HoloUiFactory.PanelFill, "HoloPrompt");

            HoloUiFactory.CreateText(panel.transform, "Prompt Text", Centre, Centre,
                Vector2.zero, new Vector2(470f, 60f), 22f, TextAlignmentOptions.Center)
                .text = "Shift  -  Reverse";

            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        /// <summary>
        /// Points the screens' scripts at the new panels. DeathMenu is moved onto
        /// the Canvas on the way, because it currently lives on the very object it
        /// shows - which cannot survive that object being deleted.
        /// </summary>
        private static int Rewire(Canvas canvas, GameObject paused, GameObject options,
            GameObject death, GameObject victory, GameObject prompt)
        {
            int wired = 0;

            foreach (PauseMenu menu in Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(menu, "Rewire menus");
                menu.pauseMenuUI = paused;
                menu.optionsUI = options;
                EditorUtility.SetDirty(menu);
                wired++;
            }

            DeathMenu deathScript = EnsureComponent<DeathMenu>(canvas.gameObject);
            Undo.RecordObject(deathScript, "Rewire menus");
            deathScript.deathMenuUI = death;
            EditorUtility.SetDirty(deathScript);
            wired++;

            // Player holds a direct reference to the DeathMenu component, and the
            // old one is about to be destroyed with the screen it sat on.
            foreach (Player player in Object.FindObjectsByType<Player>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(player, "Rewire menus");
                player.deathMenu = deathScript;
                EditorUtility.SetDirty(player);
                wired++;
            }

            foreach (VictoryMenu menu in Object.FindObjectsByType<VictoryMenu>(FindObjectsInactive.Include))
            {
                SerializedObject so = new SerializedObject(menu);
                so.FindProperty("victoryMenuUI").objectReferenceValue = victory;
                so.ApplyModifiedProperties();
                wired++;
            }

            foreach (Tutorial tutorial in Object.FindObjectsByType<Tutorial>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(tutorial, "Rewire menus");
                tutorial.shiftTutorial = prompt;
                EditorUtility.SetDirty(tutorial);
                wired++;
            }

            return wired;
        }

        /// <summary>
        /// Sends Back to the pause screen rather than just closing options. Done
        /// here because options is built first and has no way to name the screen
        /// it should return to.
        /// </summary>
        private static void WireBack(GameObject options, GameObject paused)
        {
            Button back = HoloUiFactory.Find<Button>(options.transform, "Back");
            if (back == null)
            {
                return;
            }

            UnityEventTools.AddVoidPersistentListener(back.onClick,
                new UnityAction(paused.GetComponent<MenuScreen>().Show));
            HoloUiFactory.SetPrevious(options, paused);
        }

        /// <summary>
        /// Removes old screens left behind by an earlier run.
        ///
        /// Retire only catches what the scripts still point at, so once those
        /// references have been moved to the new panels the originals become
        /// unreachable and would sit in the scene forever. This is the fallback,
        /// and the one case where matching by name is the only option left.
        /// </summary>
        private static int SweepLeftovers(Transform canvas, GameObject root)
        {
            string[] names = { "EscMenu", "OptionsMenu", "DeathMenu", "VictoryMenuUI" };
            int removed = 0;

            foreach (string name in names)
            {
                foreach (Transform candidate in canvas.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (candidate.gameObject.name != name || candidate.IsChildOf(root.transform))
                    {
                        continue;
                    }

                    // Safe only because Rewire has already moved DeathMenu onto
                    // the Canvas and repointed Player at it, and MainMenu now
                    // lives there too - so nothing on these objects is still
                    // referenced by anything.
                    Undo.DestroyObjectImmediate(candidate.gameObject);
                    removed++;
                    break;
                }
            }

            return removed;
        }

        /// <summary>
        /// Destroys the old screens. Anything under the new root is skipped, so a
        /// second run cannot delete what it just built.
        /// </summary>
        private static int Retire(List<GameObject> retired, GameObject root)
        {
            int removed = 0;

            foreach (GameObject old in retired)
            {
                if (old == null || old == root || old.transform.IsChildOf(root.transform))
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(old);
                removed++;
            }

            return removed;
        }
    }
}
