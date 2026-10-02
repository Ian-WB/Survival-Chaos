#if ENABLE_UPSCALER_FRAMEWORK
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.RenderGraphModule;

namespace SurvivalChaos
{
    /// <summary>
    /// FSR 3's reactive mask: which pixels a transparent effect drew over this
    /// frame, so FSR trusts the current frame there instead of its history.
    ///
    /// Particles write no motion vectors. FSR therefore moves an explosion's
    /// pixels with the ground or sky behind it, and blends that into the next
    /// frame, so as the camera orbits the explosion leaves copies of itself
    /// behind. Ian, playing the 29 September build on FSR: "only particles are
    /// a bit duplicated or ghosted". The reactive mask is AMD's answer to exactly
    /// that, and HDRP's upscaler framework does not provide one.
    ///
    /// Made in two halves. A global custom pass copies the frame just before
    /// HDRP draws transparents - after the opaques, the sky, the clouds and the
    /// fog, so none of those count as transparent. The upscale pass then
    /// compares that copy with the frame it is about to upscale, in
    /// Fsr3ReactiveMask.compute, and hands the result to AMD with the other
    /// inputs.
    /// </summary>
    internal static class Fsr3ReactiveMask
    {
        /// <summary>
        /// AMD's sample values for its own generator: differences under a fifth
        /// of full scale, after tonemapping, are left to history, and anything
        /// above is marked at 0.9 rather than 1 so FSR keeps a little of its
        /// history even there.
        /// </summary>
        private const float Scale = 1f;
        private const float Cutoff = 0.2f;
        private const float MarkedValue = 0.9f;

        private const string ShaderPath = "Fsr3ReactiveMask";

        private static readonly int OpaqueArrayId = Shader.PropertyToID("_OpaqueArray");
        private static readonly int ColorArrayId = Shader.PropertyToID("_ColorArray");
        private static readonly int Opaque2DId = Shader.PropertyToID("_Opaque2D");
        private static readonly int Color2DId = Shader.PropertyToID("_Color2D");
        private static readonly int ReactiveId = Shader.PropertyToID("_Reactive");
        private static readonly int ParamsId = Shader.PropertyToID("_Params");
        private static readonly int RenderSizeId = Shader.PropertyToID("_RenderSize");

        private static OpaqueCopyPass copyPass;
        private static RTHandle opaque;

        /// <summary>
        /// The copy replaced on the last format change, kept a frame longer: the
        /// upscale pass recorded that frame may already hold it.
        /// </summary>
        private static RTHandle retired;
        private static int retiredFrame = -1;
        private static ComputeShader shader;
        private static bool shaderSearched;

        /// <summary>The frame the upscaler last recorded a pass for; the copy runs only then.</summary>
        private static int wantedFrame = -1;

        /// <summary>The frame the copy last came from.</summary>
        private static int copiedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            // The pass is registered in HDRP's own static list, which play mode
            // does not clear without a domain reload.
            Disable();
            shader = null;
            shaderSearched = false;
        }

        /// <summary>Starts copying the frame before transparents. Idempotent.</summary>
        public static void Enable()
        {
            if (copyPass != null)
            {
                return;
            }

            copyPass = new OpaqueCopyPass { name = "FSR 3 opaque copy" };
            CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.BeforeTransparent, copyPass);
        }

        /// <summary>Stops copying and frees the copy. Idempotent.</summary>
        public static void Disable()
        {
            if (copyPass != null)
            {
                CustomPassVolume.UnregisterGlobalCustomPass(copyPass);
                copyPass = null;
            }

            Release(ref opaque);
            Release(ref retired);

            wantedFrame = -1;
            copiedFrame = -1;
        }

        private static void Release(ref RTHandle handle)
        {
            if (handle != null)
            {
                RTHandles.Release(handle);
                handle = null;
            }
        }

        private static ComputeShader MaskShader
        {
            get
            {
                if (!shaderSearched)
                {
                    shaderSearched = true;
                    shader = Resources.Load<ComputeShader>(ShaderPath);
                    if (shader == null)
                    {
                        Debug.LogWarning("FSR 3's reactive mask shader is missing from Resources, so " +
                                         "particles will ghost under FSR.");
                    }
                }

                return shader;
            }
        }

        /// <summary>
        /// What the upscale pass needs from this frame, or nothing when there is
        /// no copy to compare against yet - the first frame FSR runs, before the
        /// copy pass has ever executed.
        /// </summary>
        /// <remarks>
        /// A struct, held by value in the pass's own data. As a class it was
        /// one object every rendered frame, pause menu included (scan of 2
        /// October 2026); and one shared object would not do, because a pass
        /// has to run with the values it was recorded with.
        /// </remarks>
        public struct Inputs
        {
            /// <summary>False for "nothing": no copy to compare against yet.</summary>
            public bool valid;
            public RTHandle opaqueCopy;
            public TextureHandle opaque;
            public TextureHandle reactive;
            public ComputeShader shader;
            public int kernel;
            public bool arrays;
            public Vector2Int renderSize;
        }

        /// <summary>
        /// Asks for this frame's copy and declares the mask texture. Called while
        /// the frame is being recorded, before any of it runs, so it can only
        /// say what will be needed; <see cref="Dispatch"/> checks at run time that
        /// the copy really came from this frame.
        /// </summary>
        public static Inputs Record(RenderGraph renderGraph, IUnsafeRenderGraphBuilder builder,
            TextureHandle color, Vector2Int renderSize)
        {
            Enable();
            wantedFrame = Time.frameCount;

            ComputeShader compute = MaskShader;
            if (opaque == null || compute == null)
            {
                return default;
            }

            TextureDesc colorDescription = color.GetDescriptor(renderGraph);
            bool arrays = colorDescription.dimension == TextureDimension.Tex2DArray;

            TextureDesc description = colorDescription;
            description.format = GraphicsFormat.R8_UNorm;
            description.dimension = TextureDimension.Tex2D;
            description.slices = 1;
            description.msaaSamples = MSAASamples.None;
            description.useMipMap = false;
            description.autoGenerateMips = false;
            description.enableRandomWrite = true;
            description.clearBuffer = false;
            description.discardBuffer = false;
            description.filterMode = FilterMode.Point;
            description.name = "_FSR3Reactive";

            var inputs = new Inputs
            {
                valid = true,
                opaqueCopy = opaque,
                opaque = renderGraph.ImportTexture(opaque),
                reactive = renderGraph.CreateTexture(description),
                shader = compute,
                kernel = compute.FindKernel(arrays ? "MaskArray" : "Mask2D"),
                arrays = arrays,
                renderSize = renderSize,
            };

            builder.UseTexture(inputs.opaque);
            builder.UseTexture(inputs.reactive, AccessFlags.Write);
            return inputs;
        }

        /// <summary>
        /// Writes the mask. False when the copy is not this frame's, and the
        /// caller then sends FSR no mask at all rather than a stale one.
        /// </summary>
        public static bool Dispatch(CommandBuffer command, in Inputs inputs, TextureHandle color)
        {
            // Not this frame's copy, or not the texture it was copied into.
            if (!inputs.valid || copiedFrame != Time.frameCount || inputs.opaqueCopy != opaque)
            {
                return false;
            }

            ComputeShader compute = inputs.shader;
            int kernel = inputs.kernel;

            command.SetComputeTextureParam(compute, kernel, inputs.arrays ? OpaqueArrayId : Opaque2DId, (RTHandle)inputs.opaque);
            command.SetComputeTextureParam(compute, kernel, inputs.arrays ? ColorArrayId : Color2DId, (RTHandle)color);
            command.SetComputeTextureParam(compute, kernel, ReactiveId, (RTHandle)inputs.reactive);
            command.SetComputeVectorParam(compute, ParamsId, new Vector4(Scale, Cutoff, MarkedValue, 0f));
            command.SetComputeIntParams(compute, RenderSizeId, inputs.renderSize.x, inputs.renderSize.y);
            command.DispatchCompute(compute, kernel,
                (inputs.renderSize.x + 7) / 8, (inputs.renderSize.y + 7) / 8, 1);
            return true;
        }

        /// <summary>
        /// Copies the camera colour as it stands before transparents, on frames
        /// FSR is upscaling and for game cameras only. The game has one, so
        /// "this frame" is enough to tell whose copy it is.
        /// </summary>
        private sealed class OpaqueCopyPass : CustomPass
        {
            protected override void Execute(CustomPassContext ctx)
            {
                if (retired != null && retiredFrame < Time.frameCount)
                {
                    Release(ref retired);
                }

                if (wantedFrame != Time.frameCount || ctx.hdCamera.camera.cameraType != CameraType.Game)
                {
                    return;
                }

                RTHandle source = ctx.cameraColorBuffer;
                if (source == null || source.rt == null)
                {
                    return;
                }

                GraphicsFormat format = source.rt.graphicsFormat;
                if (opaque == null || opaque.rt == null || opaque.rt.graphicsFormat != format)
                {
                    Release(ref retired);
                    retired = opaque;
                    retiredFrame = Time.frameCount;

                    opaque = RTHandles.Alloc(Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
                        colorFormat: format, useDynamicScale: true, name: "FSR3 Opaque Colour");
                }

                CustomPassUtils.Copy(ctx, source, opaque);
                copiedFrame = Time.frameCount;
            }
        }
    }
}
#endif
