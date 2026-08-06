using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Routes the title video through the overlay canvas instead of the camera.
    ///
    /// Camera Far Plane draws the video as scene content, so HDRP exposes and
    /// tonemaps it - with a fixed exposure tuned for a lava-lit volcano, which is
    /// why it came out dark. Screen Space Overlay renders outside the pipeline
    /// entirely, the same property the holographic shaders rely on, so what
    /// reaches the screen is what the file contains.
    ///
    /// The render texture is created here rather than by hand because two of its
    /// settings have to be right together: an 8-bit format, because sRGB has no
    /// 10-bit variant in the graphics APIs, and the sRGB flag itself, because
    /// without it a linear-space project applies a second gamma and lifts every
    /// compression artifact out of the shadows.
    /// </summary>
    public static class MenuVideoFixer
    {
        private const string TexturePath = "Assets/UI/RenderTextures/VideoOutput.renderTexture";
        private const string BackgroundName = "Video Background";

        [MenuItem("Survival Chaos/UI/Fix Menu Video Output", priority = 23)]
        public static void Fix()
        {
            VideoPlayer player = Object.FindAnyObjectByType<VideoPlayer>(FindObjectsInactive.Include);
            if (player == null)
            {
                EditorUtility.DisplayDialog("No video player",
                    "Open the Menu scene first - this needs the scene's VideoPlayer.", "OK");
                return;
            }

            Canvas canvas = FindOverlayCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("No overlay canvas",
                    "This needs a Screen Space Overlay canvas to put the video behind.", "OK");
                return;
            }

            RenderTexture target = CreateTarget(player);

            Undo.RecordObject(player, "Fix menu video");
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = target;
            player.targetCamera = null;
            EditorUtility.SetDirty(player);

            RawImage background = CreateBackground(canvas, target);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = background.gameObject;

            Debug.Log("Menu video routed through the overlay canvas at " + target.width + "x" +
                      target.height + ", sRGB on. It no longer passes through HDRP exposure or " +
                      "tonemapping, so it should read exactly as the file does.", background);
        }

        private static Canvas FindOverlayCanvas()
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

        /// <summary>
        /// Builds the render target at the clip's own resolution. Matching the
        /// source means the only resample is the final one to the display, rather
        /// than one into the texture and another out of it.
        /// </summary>
        private static RenderTexture CreateTarget(VideoPlayer player)
        {
            int width = 1920;
            int height = 1080;

            if (player.clip != null)
            {
                width = Mathf.Max(1, (int)player.clip.width);
                height = Mathf.Max(1, (int)player.clip.height);
            }

            // ARGB32 rather than ARGB2101010: sRGB has no 10-bit variant, and
            // 8-bit sRGB beats 10-bit misread as linear - the source is 8-bit
            // anyway, and sRGB spends its precision where the eye needs it.
            RenderTexture target = new RenderTexture(width, height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "VideoOutput",
                // No geometry is drawn into this, only a video blit, so
                // multisampling would cost bandwidth for nothing.
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            RenderTexture existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(TexturePath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(target, existing);
                Object.DestroyImmediate(target);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(target, TexturePath);
            return target;
        }

        /// <summary>
        /// The full-screen image the video lands on. Forced to be the canvas's
        /// first child, because sibling order is draw order - anywhere else and it
        /// covers the menu it is supposed to sit behind.
        /// </summary>
        private static RawImage CreateBackground(Canvas canvas, RenderTexture target)
        {
            Transform existing = canvas.transform.Find(BackgroundName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            GameObject go = new GameObject(BackgroundName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Fix menu video");

            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            RawImage image = Undo.AddComponent<RawImage>(go);
            image.texture = target;
            // Nothing behind it to click, and catching pointers here would only
            // steal them from the menu.
            image.raycastTarget = false;

            return image;
        }
    }
}
