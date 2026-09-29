#if ENABLE_UPSCALER_FRAMEWORK
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos
{
    /// <summary>
    /// Puts FSR 3 in the active pipeline asset's upscaler list while it is the
    /// chosen upscaler, and takes it out again otherwise.
    ///
    /// This is the one place the game writes to a pipeline asset, and only
    /// because HDRP leaves no other way. It runs the first upscaler in that list
    /// that can run for the camera. DLSS has a per-camera switch the graphics
    /// menu turns off, but a framework upscaler has none. Listed
    /// permanently, FSR 3 would take over whenever the menu says Off but the
    /// render scale is below 100% or dynamic resolution is on, the cases HDRP's
    /// own upscale filter is meant to handle.
    ///
    /// So the name goes in only in play and only while chosen. It comes out of
    /// every asset it was put in when the choice changes, the quality tier moves
    /// to another asset, the director goes away, or play mode ends. Nothing
    /// marks the asset dirty, and <c>Fsr3Tests</c> checks that no asset on
    /// disk lists it.
    /// </summary>
    internal static class Fsr3Listing
    {
        private static readonly List<HDRenderPipelineAsset> ListedIn = new List<HDRenderPipelineAsset>();

        public static void Apply(bool wanted)
        {
            HDRenderPipelineAsset current = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            wanted &= Application.isPlaying && current != null;

            for (int i = ListedIn.Count - 1; i >= 0; i--)
            {
                if (!wanted || ListedIn[i] != current)
                {
                    Remove(ListedIn[i]);
                    ListedIn.RemoveAt(i);
                }
            }

            List<string> names = Names(current);
            if (names == null)
            {
                return;
            }

            if (!wanted)
            {
                // Also heals an asset that was saved with the name in it.
                names.Remove(Fsr3Upscaler.UpscalerName);
                return;
            }

            if (!names.Contains(Fsr3Upscaler.UpscalerName))
            {
                names.Add(Fsr3Upscaler.UpscalerName);
            }

            if (!ListedIn.Contains(current))
            {
                ListedIn.Add(current);
            }
        }

        public static void Clear()
        {
            foreach (HDRenderPipelineAsset asset in ListedIn)
            {
                Remove(asset);
            }

            ListedIn.Clear();
        }

        private static void Remove(HDRenderPipelineAsset asset)
        {
            Names(asset)?.Remove(Fsr3Upscaler.UpscalerName);
        }

        // The settings come back as a copied struct, but the list inside is the
        // asset's own, so changes to it reach the asset.
        private static List<string> Names(HDRenderPipelineAsset asset)
        {
            return asset == null
                ? null
                : asset.currentPlatformRenderPipelineSettings.dynamicResolutionSettings.advancedUpscalerNames;
        }

        // Normally empty by now, cleared as the last session ended. Clearing again
        // costs nothing and covers a session that ended without that hook.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Clear();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void ClearOnLeavingPlay()
        {
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                {
                    Clear();
                }
            };
        }
#endif
    }
}
#endif
