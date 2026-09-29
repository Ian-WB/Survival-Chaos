#if ENABLE_UPSCALER_FRAMEWORK
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalChaos
{
    /// <summary>
    /// The game's side of SurvivalChaosFfx.dll, the native plugin that runs
    /// AMD's signed FSR DLLs on Unity's render thread.
    ///
    /// The plugin's source and build script live in Native/FfxUpscaler; the
    /// three DLLs sit in Assets/Plugins/FidelityFX/x86_64. FSR through AMD's
    /// DLLs is DirectX 12 only, so on DirectX 11 this reports NotD3D12 and the
    /// menu offers no FSR at all.
    /// </summary>
    internal static class FfxNative
    {
        private const string Plugin = "SurvivalChaosFfx";

        /// <summary>Must match Status in SurvivalChaosFfx.cpp, up to NoUnityInterface.</summary>
        public enum Status
        {
            Ready = 0,
            NotD3D12 = 1,
            LoaderMissing = 2,
            UpscalerMissing = 3,
            EntryPointsMissing = 4,
            NoUnityInterface = 5,
            NotWindows = 100,
            PluginMissing = 101,
            PluginMismatch = 102,
            OffInEditor = 103,
        }

        /// <summary>
        /// One upscale's worth of inputs. Must match DispatchParams in
        /// SurvivalChaosFfx.cpp field for field; <see cref="Probe"/> checks the
        /// sizes agree and refuses to run if they do not.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct DispatchParams
        {
            public int contextId;
            public uint createFlags;
            public uint renderWidth;
            public uint renderHeight;
            public uint maxRenderWidth;
            public uint maxRenderHeight;
            public uint displayWidth;
            public uint displayHeight;
            public float jitterX;
            public float jitterY;
            public float motionScaleX;
            public float motionScaleY;
            public float sharpness;
            public int sharpen;
            public float frameTimeDeltaMs;
            public float preExposure;
            public float cameraNear;
            public float cameraFar;
            public float fovVerticalRadians;
            public int reset;
            public int debugView;
        }

        /// <summary>
        /// Which texture a texture event carries, in the low four bits of its
        /// user data, with the context id above. Must match TextureSlot in
        /// SurvivalChaosFfx.cpp.
        /// </summary>
        public const uint TextureColor = 0;
        public const uint TextureDepth = 1;
        public const uint TextureMotion = 2;
        public const uint TextureOutput = 3;

        public static uint TextureUserData(int contextId, uint texture) => ((uint)contextId << 4) | texture;

        // FfxApiCreateContextUpscaleFlags, from ffx_upscale.h.
        public const uint FlagHighDynamicRange = 1u << 0;
        public const uint FlagDepthInverted = 1u << 3;
        public const uint FlagDynamicResolution = 1u << 6;
        public const uint FlagDebugChecking = 1u << 7;

        [DllImport(Plugin)] private static extern int ScFfx_Initialize();
        [DllImport(Plugin)] private static extern IntPtr ScFfx_GetRenderEventFunc();
        [DllImport(Plugin)] private static extern IntPtr ScFfx_GetTextureEventFunc();
        [DllImport(Plugin)] private static extern int ScFfx_EventDispatch();
        [DllImport(Plugin)] private static extern int ScFfx_EventRelease();
        [DllImport(Plugin)] private static extern int ScFfx_ParamsSize();
        [DllImport(Plugin)] private static extern int ScFfx_ReserveContext();

        [DllImport(Plugin)]
        private static extern int ScFfx_GetContextInfo(
            int contextId, StringBuilder versionName, int versionNameLength, out int lastResult);

#if UNITY_EDITOR
        /// <summary>
        /// FSR 3 stays out of the editor unless asked for. A mistake in a native
        /// plugin takes the whole process down, and the editor is where the
        /// unsaved work is. Set this EditorPrefs key to let the editor load it;
        /// without it, play in the editor has no FSR at all, since FSR 2 went.
        /// </summary>
        public const string EditorPrefKey = "SurvivalChaos.Fsr3InEditor";
#endif

        private static Status? status;
        private static IntPtr renderEventFunc;
        private static IntPtr textureEventFunc;
        private static int eventDispatch;
        private static int eventRelease;

        // Parameter blocks live in unmanaged memory because the render thread
        // reads each one after the main thread has moved on. A ring of 64 is
        // many frames of slack for one camera; it is never freed, as the render
        // thread may still be reading the last block when anything here ends.
        private const int RingSize = 64;
        private static IntPtr ring;
        private static int ringNext;
        private static readonly int ParamsSize = Marshal.SizeOf<DispatchParams>();

        public static Status CurrentStatus => status ??= Probe();

        public static bool Available => CurrentStatus == Status.Ready;

        /// <summary>
        /// Turns on FSR's own parameter checking for contexts created from here
        /// on. Its warnings arrive in the console prefixed "FSR:". Off by
        /// default; it is for checking an integration, not for play.
        /// </summary>
        public static bool DebugChecking { get; set; }

        public static IntPtr RenderEventFunc => renderEventFunc;

        /// <summary>
        /// For CommandBuffer.IssuePluginCustomTextureUpdateV2, which is how the
        /// plugin learns which D3D12 resource each input is. Unity's own FSR2
        /// module, since removed, did the same: the event carries Unity's
        /// texture ID to the thread that runs the commands, where it can be
        /// turned into a resource. A render buffer pointer read here, on the
        /// main thread, cannot - under graphics jobs, resolving one crashed a
        /// build.
        /// </summary>
        public static IntPtr TextureEventFunc => textureEventFunc;
        public static int EventDispatch => eventDispatch;
        public static int EventRelease => eventRelease;

        private static Status Probe()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.WindowsEditor)
            {
                return Status.NotWindows;
            }

            // Asked before the plugin is loaded at all, so a DirectX 11 run never
            // touches it.
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
            {
                return Status.NotD3D12;
            }

#if UNITY_EDITOR
            if (!UnityEditor.EditorPrefs.GetBool(EditorPrefKey, false))
            {
                return Status.OffInEditor;
            }
#endif

            try
            {
                if (ScFfx_ParamsSize() != ParamsSize)
                {
                    Debug.LogError("FSR 3: the native plugin does not match this build of the game.");
                    return Status.PluginMismatch;
                }

                Status result = (Status)ScFfx_Initialize();
                if (result != Status.Ready)
                {
                    Debug.LogWarning("FSR 3 is unavailable: " + result + ".");
                    return result;
                }

                renderEventFunc = ScFfx_GetRenderEventFunc();
                textureEventFunc = ScFfx_GetTextureEventFunc();
                eventDispatch = ScFfx_EventDispatch();
                eventRelease = ScFfx_EventRelease();
                return Status.Ready;
            }
            catch (DllNotFoundException)
            {
                return Status.PluginMissing;
            }
            catch (EntryPointNotFoundException)
            {
                return Status.PluginMismatch;
            }
        }

        /// <summary>A context slot for one camera, or -1 when all are taken.</summary>
        public static int ReserveContext()
        {
            return Available ? ScFfx_ReserveContext() : -1;
        }

        /// <summary>
        /// Copies one upscale's parameters where the render thread can read them,
        /// and returns the address to pass with the render event.
        /// </summary>
        public static IntPtr Stage(in DispatchParams parameters)
        {
            if (ring == IntPtr.Zero)
            {
                ring = Marshal.AllocHGlobal(ParamsSize * RingSize);
            }

            IntPtr slot = ring + ParamsSize * ringNext;
            ringNext = (ringNext + 1) % RingSize;
            Marshal.StructureToPtr(parameters, slot, false);
            return slot;
        }

        /// <summary>
        /// Which FSR the context is actually running - AMD's loader picks the
        /// newest the card supports - and how many upscales it has done. The
        /// version is empty until the first upscale has created it.
        /// </summary>
        public static int ContextInfo(int contextId, out string version, out int lastResult)
        {
            version = string.Empty;
            lastResult = 0;
            if (!Available || contextId < 0)
            {
                return -1;
            }

            StringBuilder name = new StringBuilder(64);
            int dispatches = ScFfx_GetContextInfo(contextId, name, name.Capacity, out lastResult);
            version = name.ToString();
            return dispatches;
        }
    }
}
#endif
