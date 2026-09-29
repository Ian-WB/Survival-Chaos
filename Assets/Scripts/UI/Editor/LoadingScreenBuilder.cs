using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Builds the loading screen prefab that <see cref="LoadingScreen"/> puts up
    /// over every scene change: an opaque cold backdrop and one centred holo
    /// panel with a title, a bar and a line saying where the load is going.
    ///
    /// A prefab in Resources rather than a part of either scene, because it has
    /// to outlive the scene that asked for it. Built from the same factory as
    /// the menus so it looks like the same machine.
    ///
    /// Built in a throwaway scene of its own, which is closed unsaved at the
    /// end. The factory registers everything with Undo, and doing that in the
    /// open scene would mark it modified for a prefab that never lived there.
    /// </summary>
    public static class LoadingScreenBuilder
    {
        private const string PrefabPath = "Assets/Resources/" + LoadingScreen.ResourcePath + ".prefab";

        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 PanelSize = new Vector2(760f, 250f);

        [MenuItem("Survival Chaos/UI/Build Loading Screen", priority = 23)]
        public static void Build()
        {
            Material panelMaterial = HoloUiFactory.EnsureBaseMaterial("HoloPanel", "Survival Chaos/Holo Panel");
            Material barMaterial = HoloUiFactory.EnsureBaseMaterial("HoloBar", "Survival Chaos/Holo Bar");
            if (panelMaterial == null || barMaterial == null)
            {
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            Scene workbench = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(workbench);

            try
            {
                GameObject root = BuildRoot(panelMaterial, barMaterial);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                Debug.Log(saved ? "Loading screen saved to " + PrefabPath : "Loading screen was not saved.");
            }
            finally
            {
                if (previous.IsValid())
                {
                    SceneManager.SetActiveScene(previous);
                }

                EditorSceneManager.CloseScene(workbench, true);
            }
        }

        private static GameObject BuildRoot(Material panelMaterial, Material barMaterial)
        {
            GameObject root = new GameObject("LoadingScreen", typeof(RectTransform));

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Over everything either scene draws, the pause and death screens
            // included, since those are where a load can start.
            canvas.sortingOrder = 1000;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HoloUiFactory.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Catches clicks, so nothing behind the screen can be pressed while
            // it is up.
            root.AddComponent<GraphicRaycaster>();

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = true;

            // Opaque: the scene behind is frozen mid-frame, and a dimmed view of
            // it reads as the game having hung rather than loading.
            GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform));
            backdrop.transform.SetParent(root.transform, false);
            HoloUiFactory.Stretch((RectTransform)backdrop.transform);
            Image backdropImage = backdrop.AddComponent<Image>();
            Color scrim = HoloUiFactory.Scrim;
            scrim.a = 1f;
            backdropImage.color = scrim;
            backdropImage.raycastTarget = true;

            Image panel = HoloUiFactory.CreatePanel(root.transform, "Panel", Centre, Centre,
                Vector2.zero, PanelSize, panelMaterial, HoloUiFactory.PanelFill, "LoadingPanel");

            TextMeshProUGUI title = HoloUiFactory.CreateText(panel.transform, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f),
                new Vector2(PanelSize.x - 60f, 70f), 46f, TextAlignmentOptions.Center);
            title.text = "Loading";

            Image bar = HoloUiFactory.CreateBarImage(panel.transform, "Loading Bar", Centre, Centre,
                new Vector2(0f, -8f), new Vector2(600f, 22f), barMaterial, HoloUiFactory.Accent, 24f);

            // The bar only ever fills, so the shader's loss ghost has nothing to
            // show. It would show the reset between two loads as a red drain.
            bar.material.SetColor("_GhostColor", HoloUiFactory.TrackDark);
            EditorUtility.SetDirty(bar.material);
            HoloBar holoBar = bar.gameObject.AddComponent<HoloBar>();
            HoloUiFactory.ConfigureBar(holoBar, null, 0f);

            TextMeshProUGUI status = HoloUiFactory.CreateText(panel.transform, "Status",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 38f),
                new Vector2(PanelSize.x - 60f, 40f), 24f, TextAlignmentOptions.Center);
            Color quiet = HoloUiFactory.Accent;
            quiet.a = 0.8f;
            status.color = quiet;
            status.text = "Entering the arena";

            LoadingScreen screen = root.AddComponent<LoadingScreen>();
            HoloUiFactory.Assign(screen, "group", group);
            HoloUiFactory.Assign(screen, "bar", bar);
            HoloUiFactory.Assign(screen, "status", status);

            // Just the two materials this made: SaveAssets would write every
            // dirty asset in the project along with them.
            AssetDatabase.SaveAssetIfDirty(panel.material);
            AssetDatabase.SaveAssetIfDirty(bar.material);
            return root;
        }
    }
}
