#if ENABLE_UPSCALER_FRAMEWORK
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SurvivalChaos
{
    /// <summary>
    /// AMD FSR through AMD's own signed DLLs, handed to HDRP through its upscaler
    /// framework. On an RX 6000 card that is FSR 3.1.5; AMD's loader picks the
    /// newest the card supports, so an RX 9000 gets FSR 4 from the same DLL.
    ///
    /// It replaced HDRP's built-in FSR2, Unity's own integration of FSR 2, on
    /// 29 Sep 2026. The inputs are the ones that FSR2 pass handed AMD's
    /// library, because that pass was the one known to work in this game: HDR
    /// colour, reversed depth, motion vectors scaled from HDRP's viewport units
    /// to pixels, no exposure texture and a pre-exposure of 1.
    ///
    /// The render scale stays the game's. This reports no quality mode, so HDRP
    /// renders at whatever <see cref="GraphicsDirector"/> asks for and FSR only
    /// reconstructs from it - the same arrangement the DLSS path uses.
    /// </summary>
    public sealed class Fsr3Upscaler : AbstractUpscaler
    {
        public const string UpscalerName = "FSR3";

        /// <summary>Set by <see cref="GraphicsDirector"/>; RCAS strength, 0..1.</summary>
        public static float Sharpness { get; set; }

        public static bool Sharpen { get; set; }

        /// <summary>FSR's own debug view in place of the image, for checking inputs.</summary>
        public static bool DebugView { get; set; }

        /// <summary>The context slot the main camera used last, for the F3 overlay.</summary>
        public static int LastContextId { get; private set; } = -1;

        private static readonly ProfilingSampler Sampler = new ProfilingSampler("FSR 3");

        public override string name => UpscalerName;
        public override bool isTemporal => true;
        public override bool supportsSharpening => true;

        private sealed class PassData
        {
            public TextureHandle color;
            public TextureHandle depth;
            public TextureHandle motion;
            public TextureHandle output;
            public FfxNative.DispatchParams parameters;
        }

        /// <summary>
        /// Every context slot this domain holds, so they can be given back when
        /// HDRP will not. Its upscaler framework frees a context only once a
        /// running pipeline has gone 400 frames without it. When the pipeline
        /// itself goes - every quality tier change, entering play in the
        /// editor, a script reload - it drops its contexts without cleaning any
        /// of them up, and each one strands a native FSR context and its GPU
        /// memory. The plugin has 16 slots, so FSR stopped after 16 of those.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<int> LiveContexts =
            new System.Collections.Generic.HashSet<int>();

        private static bool releaseHooked;

        public override IUpscalerContext CreateContext(UpscalerOptions options, Vector2Int displayResolution)
        {
            HookRelease();

            int id = FfxNative.ReserveContext();
            if (id < 0)
            {
                return null;
            }

            LiveContexts.Add(id);
            return new Fsr3Context(id, displayResolution);
        }

        private static void HookRelease()
        {
            if (releaseHooked)
            {
                return;
            }

            releaseHooked = true;
            RenderPipelineManager.activeRenderPipelineDisposed += ReleaseAll;
#if UNITY_EDITOR
            // In case a reload ever comes without the pipeline being disposed
            // first. Releasing twice is safe: a released slot leaves the set.
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseAll;
#endif
        }

        /// <summary>Gives back every slot still held, on the render thread.</summary>
        private static void ReleaseAll()
        {
            if (LiveContexts.Count == 0)
            {
                return;
            }

            using (CommandBuffer command = new CommandBuffer { name = "FSR 3 release" })
            {
                foreach (int id in LiveContexts)
                {
                    command.IssuePluginEventAndData(
                        FfxNative.RenderEventFunc, FfxNative.EventRelease, (System.IntPtr)id);
                }

                Graphics.ExecuteCommandBuffer(command);
            }

            LiveContexts.Clear();
        }

        /// <summary>
        /// FSR's own sequence: Halton 2,3 over a phase count that grows with the
        /// square of the upscale ratio, 18 frames at Quality and 72 at Ultra
        /// Performance. HDRP applies it to the projection and hands it back as
        /// the upscaler's jitter, with the sign AMD's library expects.
        /// </summary>
        public override void CalculateJitter(int frameIndex, float upscaleRatio, out Vector2 jitter, out bool allowScaling)
        {
            int index = frameIndex % JitterPhaseCount(upscaleRatio) + 1;
            jitter = new Vector2(HaltonSequence.Get(index, 2) - 0.5f, HaltonSequence.Get(index, 3) - 0.5f);
            allowScaling = false;
        }

        /// <summary>
        /// 8 x ratio squared, truncated, as AMD computes it. The small allowance
        /// covers a ratio rebuilt from pixel counts arriving a hair under the
        /// preset's, which would otherwise lose a whole phase at Quality.
        /// </summary>
        public static int JitterPhaseCount(float upscaleRatio)
        {
            return Mathf.Max(1, Mathf.FloorToInt(8f * upscaleRatio * upscaleRatio + 0.01f));
        }

        /// <summary>AMD's recommendation: the resolution ratio's log, one lower.</summary>
        public override float CalculateMipBias(Vector2Int preUpscaleResolution, Vector2Int postUpscaleResolution)
        {
            return base.CalculateMipBias(preUpscaleResolution, postUpscaleResolution) - 1f;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UpscalingIO io = frameData.Get<UpscalingIO>();
            if (io.context is not Fsr3Context context)
            {
                return;
            }

            LastContextId = context.Id;

            TextureDesc description = io.cameraColor.GetDescriptor(renderGraph);
            description.width = io.postUpscaleResolution.x;
            description.height = io.postUpscaleResolution.y;
            description.format = GraphicsFormatUtility.GetLinearFormat(description.format);
            description.msaaSamples = MSAASamples.None;
            description.useMipMap = false;
            description.autoGenerateMips = false;
            description.useDynamicScale = false;
            description.anisoLevel = 0;
            description.enableRandomWrite = true;
            description.clearBuffer = false;
            description.discardBuffer = false;
            description.filterMode = FilterMode.Bilinear;
            description.name = "_FSR3Output";
            TextureHandle output = renderGraph.CreateTexture(description);

            using (IUnsafeRenderGraphBuilder builder =
                   renderGraph.AddUnsafePass("FSR 3 Upscale", out PassData data, Sampler))
            {
                builder.UseTexture(io.cameraColor);
                builder.UseTexture(io.cameraDepth);
                builder.UseTexture(io.motionVectorColor);
                builder.UseTexture(output, AccessFlags.Write);

                data.color = io.cameraColor;
                data.depth = io.cameraDepth;
                data.motion = io.motionVectorColor;
                data.output = output;
                data.parameters = Parameters(io, context.Id);

                builder.SetRenderFunc((PassData pass, UnsafeGraphContext graph) =>
                {
                    CommandBuffer command = CommandBufferHelpers.GetNativeCommandBuffer(graph.cmd);
                    int id = pass.parameters.contextId;

                    SendTexture(command, pass.color, id, FfxNative.TextureColor);
                    SendTexture(command, pass.depth, id, FfxNative.TextureDepth);
                    SendTexture(command, pass.motion, id, FfxNative.TextureMotion);
                    SendTexture(command, pass.output, id, FfxNative.TextureOutput);

                    command.IssuePluginEventAndData(
                        FfxNative.RenderEventFunc, FfxNative.EventDispatch, FfxNative.Stage(pass.parameters));
                });
            }

            io.cameraColor = output;
        }

        private static FfxNative.DispatchParams Parameters(UpscalingIO io, int contextId)
        {
            // HDRP's motion vectors are the move from last frame to this one in
            // viewport units; AMD wants this frame back to last in pixels.
            float direction = io.motionVectorDirection == UpscalingIO.MotionVectorDirection.PreviousFrameToCurrentFrame
                ? -1f
                : 1f;
            bool viewportUnits = io.motionVectorDomain == UpscalingIO.MotionVectorDomain.NDC;

            uint flags = 0;
            if (io.hdrInput)
            {
                flags |= FfxNative.FlagHighDynamicRange;
            }
            if (io.invertedDepth)
            {
                flags |= FfxNative.FlagDepthInverted;
            }
            if (io.dynamicResolution.HasValue)
            {
                flags |= FfxNative.FlagDynamicResolution;
            }
            if (FfxNative.DebugChecking)
            {
                flags |= FfxNative.FlagDebugChecking;
            }

            return new FfxNative.DispatchParams
            {
                contextId = contextId,
                createFlags = flags,
                renderWidth = (uint)io.preUpscaleResolution.x,
                renderHeight = (uint)io.preUpscaleResolution.y,
                maxRenderWidth = (uint)io.maxPreUpscaleResolution.x,
                maxRenderHeight = (uint)io.maxPreUpscaleResolution.y,
                displayWidth = (uint)io.postUpscaleResolution.x,
                displayHeight = (uint)io.postUpscaleResolution.y,
                jitterX = io.subpixelJitter.x,
                jitterY = io.subpixelJitter.y,
                motionScaleX = direction * (viewportUnits ? io.motionVectorTextureSize.x : 1f),
                motionScaleY = direction * (viewportUnits ? io.motionVectorTextureSize.y : 1f),
                sharpness = Mathf.Clamp01(Sharpness),
                sharpen = Sharpen && Sharpness > 0f ? 1 : 0,
                frameTimeDeltaMs = io.deltaTime * 1000f,
                preExposure = 1f,
                cameraNear = io.nearClipPlane,
                cameraFar = io.farClipPlane,
                fovVerticalRadians = io.fieldOfViewDegrees * Mathf.Deg2Rad,
                reset = io.resetHistory ? 1 : 0,
                debugView = DebugView ? 1 : 0,
            };
        }

        /// <summary>
        /// Tells the plugin which texture fills one input. See
        /// <see cref="FfxNative.TextureEventFunc"/> for why it goes this way. A
        /// missing texture sends nothing, and the plugin then skips the upscale
        /// rather than run it on last frame's inputs.
        /// </summary>
        private static void SendTexture(CommandBuffer command, TextureHandle handle, int contextId, uint slot)
        {
            RenderTexture texture = ((RTHandle)handle).rt;
            if (texture != null)
            {
                command.IssuePluginCustomTextureUpdateV2(
                    FfxNative.TextureEventFunc, texture, FfxNative.TextureUserData(contextId, slot));
            }
        }

        /// <summary>
        /// One camera's FSR history. The native context behind it is built on
        /// the first upscale and rebuilt whenever the sizes or flags change; the
        /// plugin decides that, so this only has to hold the slot.
        /// </summary>
        private sealed class Fsr3Context : IUpscalerContext
        {
            public Fsr3Context(int id, Vector2Int displayResolution)
            {
                Id = id;
                createdForDisplayResolution = displayResolution;
            }

            public int Id { get; }

            public Vector2Int createdForDisplayResolution { get; }

            public int lastUsedFrame { get; set; }

            public bool IsValidForOptions(UpscalerOptions options) => true;

            public void Cleanup(CommandBuffer cmd)
            {
                // Only while still held: after ReleaseAll the slot may already
                // belong to another camera's context.
                if (LiveContexts.Remove(Id))
                {
                    cmd.IssuePluginEventAndData(FfxNative.RenderEventFunc, FfxNative.EventRelease, (System.IntPtr)Id);
                }
            }
        }
    }
}
#endif
