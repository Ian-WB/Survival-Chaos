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

            // Options is four tabs, each its own screen, and every one of them
            // backs out to the pause screen. The strip, the rows and the Back
            // buttons come from the factory the title screen shares.
            GameObject[] tabs = new GameObject[HoloUiFactory.OptionTabNames.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i] = BuildScreen(root.transform, HoloUiFactory.OptionTabNames[i] + " Screen",
                    panelMaterial, HoloUiFactory.OptionsPanelSize, null, HoloUiFactory.Edge);
            }

            GameObject abandon = BuildAbandon(root.transform, panelMaterial, mainMenu);
            GameObject paused = BuildPause(root.transform, panelMaterial, pause, abandon.GetComponent<AbandonRun>());
            // No and B come back here.
            HoloUiFactory.Assign(abandon.GetComponent<MenuScreen>(), "previous", paused.GetComponent<MenuScreen>());
            OptionsTabs options = HoloUiFactory.BuildOptionsTabs(tabs, panelMaterial, barMaterial, paused);
            WireOptions(paused, options);
            GameObject death = BuildOutcome(root.transform, panelMaterial, mainMenu,
                "Death Screen", "Ship Lost", HoloUiFactory.Loss);
            GameObject victory = BuildOutcome(root.transform, panelMaterial, mainMenu,
                "Victory Screen", "Leviathan Down", HoloUiFactory.Health);
            GameObject prompt = BuildTutorialPrompt(root.transform, panelMaterial);

            int wired = Rewire(canvas, paused, tabs[0], death, victory, prompt);
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
                Add(found, HoloUiFactory.Read(menu, "pauseMenuUI") as GameObject);
                Add(found, HoloUiFactory.Read(menu, "optionsUI") as GameObject);
            }

            foreach (DeathMenu menu in Object.FindObjectsByType<DeathMenu>(FindObjectsInactive.Include))
            {
                Add(found, HoloUiFactory.Read(menu, "deathMenuUI") as GameObject);
            }

            foreach (VictoryMenu menu in Object.FindObjectsByType<VictoryMenu>(FindObjectsInactive.Include))
            {
                Add(found, HoloUiFactory.Read(menu, "victoryMenuUI") as GameObject);
            }

            foreach (Tutorial tutorial in Object.FindObjectsByType<Tutorial>(FindObjectsInactive.Include))
            {
                Add(found, HoloUiFactory.Read(tutorial, "shiftTutorial") as GameObject);
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
            // A null title is an options tab, whose strip of tabs is its title.
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

            if (title != null)
            {
                TextMeshProUGUI heading = HoloUiFactory.CreateText(panel.transform, "Title", new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(panelSize.x - 80f, 60f),
                    42f, TextAlignmentOptions.Center);
                heading.text = title;
                heading.color = titleColor;
            }

            screen.SetActive(false);
            return screen;
        }

        private static Transform PanelOf(GameObject screen)
        {
            return screen.transform.Find("Panel");
        }

        private static Button AddButton(Transform panel, Material panelMaterial, string name,
            string label, int row, float firstRow = -150f)
        {
            return HoloUiFactory.CreateButton(panel, name, new Vector2(0.5f, 1f), Centre,
                new Vector2(0f, firstRow - row * ButtonStep), ButtonSize, panelMaterial, label, 24f);
        }

        /// <summary>
        /// Where the buttons start on a screen with a line under its title: far
        /// enough down that the line has its own room between the two.
        /// </summary>
        private const float FirstRowUnderALine = -186f;

        /// <summary>The line under a title, clear of the title's capitals and of the first button.</summary>
        private const float LineUnderTitle = -112f;

        /// <summary>
        /// The pause screen. Its Options button is wired afterwards by
        /// <see cref="WireOptions"/>, because the tabs it opens need this screen
        /// to exist first, for their Back buttons.
        ///
        /// Since 30 September 2026 it has Restart, and Restart, Main Menu and
        /// Quit ask first (<see cref="AbandonRun"/>). Under the buttons is the
        /// run so far, written by the end card's own <see cref="RunSummary"/>,
        /// and under the title a line that shows only when a pad going away is
        /// what paused the game.
        /// </summary>
        private static GameObject BuildPause(Transform parent, Material panelMaterial,
            PauseMenu pause, AbandonRun abandon)
        {
            GameObject screen = BuildScreen(parent, "Pause Screen", panelMaterial,
                new Vector2(620f, 780f), "Paused", HoloUiFactory.Edge);
            Transform panel = PanelOf(screen);

            TextMeshProUGUI notice = HoloUiFactory.CreateText(panel, "Pause Notice", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, LineUnderTitle), new Vector2(560f, 28f), 20f,
                TextAlignmentOptions.Center);
            notice.text = "Controller disconnected";
            notice.color = HoloUiFactory.Loss;
            notice.raycastTarget = false;
            notice.gameObject.SetActive(false);
            if (pause != null)
            {
                HoloUiFactory.Assign(pause, "pauseNotice", notice.gameObject);
            }

            Button resume = AddButton(panel, panelMaterial, "Resume", "Resume", 0, FirstRowUnderALine);
            if (pause != null)
            {
                UnityEventTools.AddVoidPersistentListener(resume.onClick, new UnityAction(pause.Resume));
            }

            AddButton(panel, panelMaterial, "Options", "Options", 1, FirstRowUnderALine);

            Button restart = AddButton(panel, panelMaterial, "Restart", "Restart", 2, FirstRowUnderALine);
            UnityEventTools.AddVoidPersistentListener(restart.onClick, new UnityAction(abandon.AskRestart));

            Button toMenu = AddButton(panel, panelMaterial, "Main Menu", "Main Menu", 3, FirstRowUnderALine);
            UnityEventTools.AddVoidPersistentListener(toMenu.onClick, new UnityAction(abandon.AskMainMenu));

            Button quit = AddButton(panel, panelMaterial, "Quit", "Quit", 4, FirstRowUnderALine);
            UnityEventTools.AddVoidPersistentListener(quit.onClick, new UnityAction(abandon.AskQuit));

            // The run so far. Filled each time the screen opens.
            TextMeshProUGUI summary = HoloUiFactory.CreateText(panel, "Run Summary", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -602f), new Vector2(560f, 150f), 18f,
                TextAlignmentOptions.Top);
            summary.enableAutoSizing = true;
            summary.fontSizeMin = 14f;
            summary.fontSizeMax = 18f;
            summary.textWrappingMode = TextWrappingModes.Normal;
            summary.raycastTarget = false;

            RunSummary runSummary = Undo.AddComponent<RunSummary>(screen);
            HoloUiFactory.Assign(runSummary, "target", summary);

            // Which build this is, so a screenshot of a paused run says so.
            HoloUiFactory.AddBuildStamp(screen.transform);

            return screen;
        }

        /// <summary>
        /// The question before a run is thrown away. No comes first, so it is
        /// what is selected, and what a second press of A lands on.
        /// </summary>
        private static GameObject BuildAbandon(Transform parent, Material panelMaterial, MainMenu mainMenu)
        {
            GameObject screen = BuildScreen(parent, "Abandon Screen", panelMaterial,
                new Vector2(620f, 360f), "Abandon this run?", HoloUiFactory.Loss);
            Transform panel = PanelOf(screen);

            TextMeshProUGUI consequence = HoloUiFactory.CreateText(panel, "Consequence", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, LineUnderTitle), new Vector2(560f, 28f), 20f,
                TextAlignmentOptions.Center);
            consequence.text = "This run is lost.";
            consequence.raycastTarget = false;

            AbandonRun abandon = Undo.AddComponent<AbandonRun>(screen);
            HoloUiFactory.Assign(abandon, "mainMenu", mainMenu);
            HoloUiFactory.Assign(abandon, "consequence", consequence);

            Button no = AddButton(panel, panelMaterial, "No", "No, keep playing", 0, FirstRowUnderALine);
            UnityEventTools.AddVoidPersistentListener(no.onClick, new UnityAction(abandon.Cancel));

            Button yes = AddButton(panel, panelMaterial, "Yes", "Yes", 1, FirstRowUnderALine);
            UnityEventTools.AddVoidPersistentListener(yes.onClick, new UnityAction(abandon.Confirm));

            return screen;
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

        /// <summary>
        /// The prompt that teaches the controls, one line at a time (see
        /// Tutorial). Wide enough for the longest line, the Deflector's, at
        /// full size; the text shrinks rather than wraps if a line outgrows it.
        /// </summary>
        private static GameObject BuildTutorialPrompt(Transform parent, Material panelMaterial)
        {
            Image panel = HoloUiFactory.CreatePanel(parent, "Hint Prompt", new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(820f, 76f),
                panelMaterial, HoloUiFactory.PanelFill, "HoloPrompt");

            TextMeshProUGUI text = HoloUiFactory.CreateText(panel.transform, "Prompt Text", Centre, Centre,
                Vector2.zero, new Vector2(770f, 60f), 22f, TextAlignmentOptions.Center);
            text.text = "Shift  -  Reverse";
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = 22f;
            text.textWrappingMode = TextWrappingModes.NoWrap;

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
                HoloUiFactory.Assign(menu, "pauseMenuUI", paused);
                HoloUiFactory.Assign(menu, "optionsUI", options);
                wired++;
            }

            DeathMenu deathScript = EnsureComponent<DeathMenu>(canvas.gameObject);
            HoloUiFactory.Assign(deathScript, "deathMenuUI", death);
            wired++;

            // Player holds a direct reference to the DeathMenu component, and the
            // old one is about to be destroyed with the screen it sat on.
            foreach (Player player in Object.FindObjectsByType<Player>(FindObjectsInactive.Include))
            {
                HoloUiFactory.Assign(player, "deathMenu", deathScript);
                wired++;
            }

            foreach (VictoryMenu menu in Object.FindObjectsByType<VictoryMenu>(FindObjectsInactive.Include))
            {
                HoloUiFactory.Assign(menu, "victoryMenuUI", victory);
                wired++;
            }

            foreach (Tutorial tutorial in Object.FindObjectsByType<Tutorial>(FindObjectsInactive.Include))
            {
                HoloUiFactory.Assign(tutorial, "shiftTutorial", prompt);
                wired++;
            }

            return wired;
        }

        /// <summary>
        /// Points the pause screen's Options button at the tabs. It opens the
        /// one the player was last on rather than always the first.
        /// </summary>
        private static void WireOptions(GameObject paused, OptionsTabs options)
        {
            Button button = HoloUiFactory.Find<Button>(paused.transform, "Options");
            if (button == null || options == null)
            {
                return;
            }

            // ShowRemembered calls Show, not SetActive: opening options has to
            // close the pause screen too.
            UnityEventTools.AddVoidPersistentListener(button.onClick, new UnityAction(options.ShowRemembered));
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
