#if ENABLE_UPSCALER_FRAMEWORK
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalChaos
{
    /// <summary>
    /// Decides which upscalers HDRP's upscaler framework gets to build.
    ///
    /// The framework is switched on by the ENABLE_UPSCALER_FRAMEWORK define,
    /// which Unity ships off, because it is the only way to hand HDRP an upscaler
    /// of our own. Switching it on also registers Unity's own framework copies
    /// of DLSS and FSR2, and HDRP builds every registered upscaler whenever it
    /// creates the pipeline - after every compile, entering play, and every
    /// change of quality tier. The DLSS copy logs a warning each time on any card
    /// that is not NVIDIA's.
    ///
    /// Neither copy is used. The pipeline assets list DLSS and FSR2 by their
    /// short names, and HDRP runs those through its own long-standing passes,
    /// which the graphics menu drives. The framework copies would only ever run
    /// if an asset named them in full, so they are taken out again here.
    ///
    /// What goes in is <see cref="Fsr3Upscaler"/>, under the name FSR3. HDRP
    /// only runs it for a pipeline asset that lists that name, and
    /// <see cref="GraphicsDirector"/> lists it only while FSR 3 is chosen.
    /// </summary>
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    public static class UpscalerRegistration
    {
        private static readonly string[] UnusedRegistrars = { "RegisterDLSS", "RegisterFSR2" };
        private static readonly string[] UnusedUpscalers = { "DLSSIUpscaler", "FSR2IUpscaler" };

        // In the editor the registrars' static constructors add them, once per
        // domain. Running those constructors here first means they have already
        // happened by the time the removal runs, whichever order Unity picks.
        static UpscalerRegistration()
        {
            RemoveUnused();
            RegisterOurs();
        }

        // The earliest a player can run anything, so FSR 3 is registered before
        // HDRP could possibly build its pipeline.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void SubsystemRegistration()
        {
            RegisterOurs();
        }

        // In a player, and in play mode, the registrars add them again from a
        // BeforeSceneLoad method. Unity does not order methods within one load
        // phase, so this runs in that phase and again once the scene has loaded,
        // which is still before its first frame is drawn.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeSceneLoad()
        {
            RemoveUnused();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad()
        {
            RemoveUnused();
        }

        private static void RegisterOurs()
        {
            UpscalerRegistry.Register<Fsr3Upscaler>(Fsr3Upscaler.UpscalerName);
        }

        private static void RemoveUnused()
        {
            foreach (string registrar in UnusedRegistrars)
            {
                Type type = CoreType(registrar);
                if (type != null)
                {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                }
            }

            foreach (string upscaler in UnusedUpscalers)
            {
                Type type = CoreType(upscaler);
                if (type != null)
                {
                    UpscalerRegistry.s_RegisteredUpscalers.Remove(type);
                }
            }
        }

        // Both live in the global namespace and exist only when their vendor
        // module is installed, so they are found by name rather than typeof.
        private static Type CoreType(string name)
        {
            return Type.GetType(name + ", Unity.RenderPipelines.Core.Runtime");
        }
    }
}
#endif
