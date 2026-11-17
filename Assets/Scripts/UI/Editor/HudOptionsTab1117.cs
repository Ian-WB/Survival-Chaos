using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Adds the HUD options tab (17 November 2026 redesign, block 7) to the
    /// open scene's options without rebuilding its menus: a fifth screen after
    /// Controls, a fifth button on every tab strip, the strips re-spaced to
    /// fit, and every tab's list of tabs extended. A rebuild of the menus
    /// makes the same tab through <see cref="HoloUiFactory.BuildOptionsTabs"/>.
    ///
    /// Runs once per scene: a scene that already has the tab is left alone.
    /// </summary>
    public static class HudOptionsTab1117
    {
        public const string ScreenName = "HUD Screen";

        /// <summary>The strip's height on the panel, as HoloUiFactory builds it.</summary>
        private const float StripY = -62f;

        [MenuItem("Survival Chaos/UI/Add HUD Options Tab (17 Nov)")]
        private static void AddFromMenu()
        {
            Debug.Log(Apply());
        }

        /// <summary>Adds the tab to the open scene. Returns what it did.</summary>
        public static string Apply()
        {
            string[] names = HoloUiFactory.OptionTabNames;
            int count = names.Length;

            Transform controls = FindScreen(names[3] + " Screen");
            if (controls == null)
            {
                return "No Controls Screen with options tabs in this scene.";
            }

            Transform root = controls.parent;
            if (root.Find(ScreenName) != null)
            {
                return "The HUD tab is already here.";
            }

            Undo.SetCurrentGroupName("Add HUD options tab");
            int group = Undo.GetCurrentGroup();

            Material panelMaterial = HoloUiFactory.EnsureBaseMaterial("HoloPanel", "Survival Chaos/Holo Panel");
            Material barMaterial = HoloUiFactory.EnsureBaseMaterial("HoloBar", "Survival Chaos/Holo Bar");

            // A copy of Controls keeps the screen's own settings, its strip,
            // its hints and its Back, with Back still going where this
            // scene's Back goes. Everything else on the panel goes.
            GameObject hud = Object.Instantiate(controls.gameObject, root);
            hud.name = ScreenName;
            hud.transform.SetSiblingIndex(controls.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(hud, "Add HUD options tab");

            Transform hudPanel = hud.transform.Find("Panel");
            var keep = new HashSet<string> { "Previous Tab Hint", "Next Tab Hint", "Back" };
            for (int i = 0; i < count - 1; i++)
            {
                keep.Add(names[i] + " Tab");
            }

            for (int i = hudPanel.childCount - 1; i >= 0; i--)
            {
                Transform child = hudPanel.GetChild(i);
                if (!keep.Contains(child.name))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            MenuScreen[] tabs = new MenuScreen[count];
            for (int i = 0; i < count; i++)
            {
                Transform screen = i == count - 1 ? hud.transform : root.Find(names[i] + " Screen");
                tabs[i] = screen != null ? screen.GetComponent<MenuScreen>() : null;
                if (tabs[i] == null)
                {
                    Undo.CollapseUndoOperations(group);
                    return "Missing " + names[i] + " Screen; stopped part way, undo it.";
                }
            }

            // Every strip: five tabs re-spaced, each click opening its tab
            // (the copy's Controls button had been pointed at the copy), the
            // open one held lit, the hints moved out to the strip's ends.
            for (int s = 0; s < count; s++)
            {
                Transform panel = tabs[s].transform.Find("Panel");
                for (int i = 0; i < count; i++)
                {
                    Transform tab = panel.Find(names[i] + " Tab");
                    Button button;
                    if (tab == null)
                    {
                        button = HoloUiFactory.CreateButton(panel, names[i] + " Tab", new Vector2(0.5f, 1f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, StripY), new Vector2(HoloUiFactory.OptionTabWidth(count),
                                58f), panelMaterial, names[i], 24f);
                        button.navigation = new Navigation { mode = Navigation.Mode.None };
                        tab = button.transform;
                        Transform before = panel.Find(names[i - 1] + " Tab");
                        tab.SetSiblingIndex(before.GetSiblingIndex() + 1);
                    }
                    else
                    {
                        button = tab.GetComponent<Button>();
                    }

                    Undo.RecordObject(tab, "Add HUD options tab");
                    RectTransform rect = (RectTransform)tab;
                    rect.anchoredPosition = new Vector2(HoloUiFactory.OptionTabX(i, count), rect.anchoredPosition.y);
                    rect.sizeDelta = new Vector2(HoloUiFactory.OptionTabWidth(count), rect.sizeDelta.y);

                    Transform label = tab.Find("Label");
                    if (label != null)
                    {
                        Undo.RecordObject(label, "Add HUD options tab");
                        RectTransform labelRect = (RectTransform)label;
                        labelRect.sizeDelta = new Vector2(HoloUiFactory.OptionTabWidth(count), labelRect.sizeDelta.y);
                    }

                    Undo.RecordObject(button, "Add HUD options tab");
                    while (button.onClick.GetPersistentEventCount() > 0)
                    {
                        UnityEventTools.RemovePersistentListener(button.onClick, 0);
                    }
                    UnityEventTools.AddVoidPersistentListener(button.onClick, new UnityAction(tabs[i].Show));

                    SerializedObject highlight = new SerializedObject(tab.GetComponent<HoloButtonHighlight>());
                    highlight.FindProperty("held").boolValue = i == s;
                    highlight.ApplyModifiedProperties();
                }

                float hintX = HoloUiFactory.OptionTabHintX(count);
                MoveX(panel.Find("Previous Tab Hint"), -hintX);
                MoveX(panel.Find("Next Tab Hint"), hintX);

                SerializedObject so = new SerializedObject(tabs[s].GetComponent<OptionsTabs>());
                SerializedProperty list = so.FindProperty("tabs");
                list.arraySize = count;
                for (int j = 0; j < count; j++)
                {
                    list.GetArrayElementAtIndex(j).objectReferenceValue = tabs[j];
                }
                so.ApplyModifiedProperties();
            }

            // The HUD tab's own rows, walked down to Back.
            List<Selectable> order = HoloUiFactory.PopulateHudPanel(hudPanel, panelMaterial, barMaterial);
            Button back = hudPanel.Find("Back").GetComponent<Button>();
            back.transform.SetAsLastSibling();
            order.Add(back);
            HoloUiFactory.ChainVertically(order);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(hud.scene);
            return "Added the HUD tab to " + hud.scene.name + ".";
        }

        private static void MoveX(Transform target, float x)
        {
            if (target == null)
            {
                return;
            }

            Undo.RecordObject(target, "Add HUD options tab");
            RectTransform rect = (RectTransform)target;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
        }

        /// <summary>The options screen of this name, active or not.</summary>
        private static Transform FindScreen(string name)
        {
            foreach (OptionsTabs tabs in Object.FindObjectsByType<OptionsTabs>(FindObjectsInactive.Include))
            {
                if (tabs.name == name)
                {
                    return tabs.transform;
                }
            }

            return null;
        }
    }
}
