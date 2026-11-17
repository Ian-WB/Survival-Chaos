using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace SurvivalChaos.Editor
{
    /// <summary>
    /// Lightens the player ship's hull as its model imports. As authored, the
    /// hull's navy and blue read over lava but sink into dark rock and space
    /// at the size the ship is drawn (16 November 2026). The colours are
    /// lifted here, on import, rather than in extracted materials: the ship's
    /// upgrade parts and ShipPartsBuilder point at the model's own embedded
    /// materials, and extracting them would cut those references.
    ///
    /// Besides the lighter albedo, the hull gets more smoothness, so HDRP's
    /// built-in Fresnel lifts its grazing edges into a rim, and a faint cool
    /// emission so the outline still shows where nothing lights it.
    /// HitFlash writes _EmissiveColor through a property block and clears it
    /// afterwards, so the hit flash still works over this.
    /// </summary>
    public class ShipHullLift : AssetPostprocessor
    {
        public const string ModelPath = "Assets/Art/Models/Ships/nave 01_v06_unity.fbx";

        /// <summary>Authored sRGB base colour of the main hull: (0.294, 0.425, 0.847).</summary>
        public static readonly Color Hull = new Color(0.34f, 0.48f, 0.92f);

        /// <summary>Authored sRGB base colour of the dark panels: (0.173, 0.235, 0.439).</summary>
        public static readonly Color Dark = new Color(0.17f, 0.235f, 0.44f);

        /// <summary>Authored 0.08 on the hull, 0.47 to 0.5 on the rest.</summary>
        public const float Smoothness = 0.3f;

        /// <summary>
        /// None since 17 November 2026: with the lighter dark panels and the
        /// sheen it washed the ship out, "the colours look dull and overly
        /// bright" (Ian). The dark panels went back to as authored, which is
        /// the contrast the hull reads by, and the hull keeps a smaller lift.
        /// Before: hull (0.42, 0.56, 0.95), dark (0.27, 0.36, 0.64), smoothness 0.5,
        /// and this glow at (0.035, 0.05, 0.1).
        /// </summary>
        /// <remarks>A cool glow, a fifth of the hit flash's 0.5 at most. Tried 16 Nov 2026: a third of this vanished over dark rock, 0.1/0.15/0.3 washed the hull grey.</remarks>
        public static readonly Color Glow = new Color(0f, 0f, 0f);

        public override uint GetVersion()
        {
            return 3;
        }

        /// <summary>After HDRP's own FBX pass (-980), which sets these same properties from the file.</summary>
        public override int GetPostprocessOrder()
        {
            return 100;
        }

        // The model imports its materials through their descriptions, and
        // OnPostprocessMaterial only runs for the legacy import; this is the
        // hook HDRP itself uses to build them.
        private void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            if (assetPath != ModelPath)
            {
                return;
            }

            switch (description.materialName)
            {
                case "Materiais.002":
                case "Materiais.003":
                case "Materiais.004":
                    Lift(material, Hull);
                    break;
                case "Materiais":
                    Lift(material, Dark);
                    break;
            }
        }

        private static void Lift(Material material, Color albedo)
        {
            material.SetColor("_BaseColor", albedo);
            material.SetFloat("_Smoothness", Mathf.Max(material.GetFloat("_Smoothness"), Smoothness));
            material.SetColor("_EmissiveColor", Glow);
            material.SetColor("_EmissiveColorLDR", Glow);
            material.SetFloat("_UseEmissiveIntensity", 0f);
        }
    }
}
