using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Puts every glowing thing in the game on one brightness scale, in one
    /// authoring mode.
    ///
    /// Two separate problems had grown here.
    ///
    /// The first is that HDRP offers two ways to author emission and the project
    /// used both. Four materials set an HDR colour directly; the bullet material
    /// used the intensity mode instead, where the shown colour is a low-dynamic
    /// -range swatch multiplied by a separate intensity field. Numbers from the
    /// two modes cannot be compared by eye or in the Inspector, so nobody could
    /// tell that the bullets were the dimmest emissive surface in the game.
    ///
    /// The second is that they were. The bullet resolved to about 0.75 in linear
    /// terms - below 1, which is the point where a surface stops reading as a
    /// light source at all - while the pickups it was supposed to match ran 2.0
    /// to 3.0. The pickups were designed to look "like the bullet"; they were
    /// four times brighter than it.
    ///
    /// Re-running is safe and idempotent.
    /// </summary>
    public static class EmissiveLadder
    {
        /// <summary>
        /// Anything the player has to pick out of a busy screen and act on:
        /// bullets, pickups.
        ///
        /// One rung, deliberately. These are the same kind of object from the
        /// player's point of view - a small bright thing that matters - so a
        /// difference in brightness between them would read as a difference in
        /// importance rather than as a difference in kind. Colour carries the
        /// identity; brightness carries "look at me", and they all mean it
        /// equally.
        ///
        /// Bloom in this project runs at threshold 0 and intensity 0.2, so
        /// nothing here blows out; the value has to do the work on its own.
        /// This is the one number to move if the screen feels too hot.
        /// </summary>
        private const float GameplayRung = 3.0f;

        private const string ProjectilePath = "Assets/Art/Materials/VFX/Projectile.mat";
        private const string PickupGlowPath = "Assets/Art/Materials/VFX/PickupGlow.mat";

        [MenuItem("Survival Chaos/Normalise Emissive Brightness", priority = 51)]
        public static void Apply()
        {
            int changed = 0;

            changed += FixProjectile() ? 1 : 0;
            changed += FixPickupGlow() ? 1 : 0;
            changed += NormaliseSkillColours();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Emissive ladder applied to {changed} asset(s). The health drop colour lives " +
                      "on the PickupSpawner in the scene and is normalised separately - see " +
                      "NormaliseHealthDropColour.");
        }

        /// <summary>
        /// The bullets: off the intensity mode, onto the gameplay rung.
        ///
        /// Order matters. useEmissiveIntensity has to be cleared before the
        /// colour is written, because while it is set HDRP recomputes
        /// _EmissiveColor from the LDR swatch and the intensity field on every
        /// validate - so a colour written first is silently overwritten by the
        /// old dim value.
        /// </summary>
        private static bool FixProjectile()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ProjectilePath);
            if (material == null)
            {
                Debug.LogWarning($"Not found: {ProjectilePath}");
                return false;
            }

            Color before = material.GetColor("_EmissiveColor");

            HDMaterial.SetUseEmissiveIntensity(material, false);
            HDMaterial.SetEmissiveColor(material, ScaleToPeak(before, GameplayRung));

            EditorUtility.SetDirty(material);

            Debug.Log($"Projectile: {Describe(before)} in intensity mode -> " +
                      $"{Describe(material.GetColor("_EmissiveColor"))} as a direct HDR colour.");
            return true;
        }

        /// <summary>
        /// The pickup prefab's shared material.
        ///
        /// Only ever seen on a pickup that failed to configure itself - every
        /// live pickup overrides this through a MaterialPropertyBlock with its
        /// skill's own colour. Kept on the rung anyway so the fallback is not a
        /// different brightness from the thing it stands in for.
        /// </summary>
        private static bool FixPickupGlow()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(PickupGlowPath);
            if (material == null)
            {
                Debug.LogWarning($"Not found: {PickupGlowPath}");
                return false;
            }

            Color scaled = ScaleToPeak(material.GetColor("_EmissiveColor"), GameplayRung);

            HDMaterial.SetUseEmissiveIntensity(material, false);
            HDMaterial.SetEmissiveColor(material, scaled);
            material.SetColor("_UnlitColor", scaled);

            EditorUtility.SetDirty(material);
            return true;
        }

        /// <summary>
        /// Puts every skill's pickup colour on the rung without changing its hue.
        ///
        /// The four colours were authored at 2.4 and 3.0, which made two of the
        /// upgrades look slightly less important than the other two for no
        /// reason anybody chose.
        /// </summary>
        private static int NormaliseSkillColours()
        {
            int changed = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
                if (skill == null)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty colour = serialized.FindProperty("pickupColor");
                if (colour == null)
                {
                    continue;
                }

                Color before = colour.colorValue;
                Color after = ScaleToPeak(before, GameplayRung);

                if (Approximately(before, after))
                {
                    continue;
                }

                colour.colorValue = after;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(skill);
                changed++;

                Debug.Log($"{skill.name}: {Describe(before)} -> {Describe(after)}");
            }

            return changed;
        }

        /// <summary>
        /// The timed health drop's colour, which is a field on the spawner in the
        /// scene rather than an asset - so it needs the scene open and saved.
        /// </summary>
        [MenuItem("Survival Chaos/Normalise Health Drop Colour", priority = 52)]
        public static void NormaliseHealthDropColour()
        {
            // Any rather than First: there is only ever one spawner, and the
            // ordered variant is deprecated for depending on instance ID order.
            PickupSpawner spawner = Object.FindAnyObjectByType<PickupSpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("No PickupSpawner in the open scene.");
                return;
            }

            SerializedObject serialized = new SerializedObject(spawner);
            SerializedProperty colour = serialized.FindProperty("healthColor");
            if (colour == null)
            {
                Debug.LogWarning("PickupSpawner has no healthColor field.");
                return;
            }

            Color before = colour.colorValue;
            colour.colorValue = ScaleToPeak(before, GameplayRung);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);

            Debug.Log($"Health drop: {Describe(before)} -> {Describe(colour.colorValue)}. " +
                      "Save the scene to keep it.");
        }

        /// <summary>
        /// Scales a colour so its brightest channel lands on <paramref name="peak"/>,
        /// leaving the ratios between channels alone.
        ///
        /// Scaling rather than clamping is what keeps the hue. Clamping a colour
        /// to a ceiling squashes its brightest channel toward the others and
        /// drifts it toward white, which on a set of pickups distinguished only
        /// by colour is the one thing that must not happen.
        /// </summary>
        private static Color ScaleToPeak(Color colour, float peak)
        {
            float highest = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));

            if (highest <= 0.0001f)
            {
                // Black has no hue to preserve, so there is nothing to scale.
                // Returning grey at the rung is the honest fallback.
                return new Color(peak, peak, peak, colour.a);
            }

            float factor = peak / highest;
            return new Color(colour.r * factor, colour.g * factor, colour.b * factor, colour.a);
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.001f
                   && Mathf.Abs(a.g - b.g) < 0.001f
                   && Mathf.Abs(a.b - b.b) < 0.001f;
        }

        private static string Describe(Color c)
        {
            return $"({c.r:0.##}, {c.g:0.##}, {c.b:0.##}) peak {Mathf.Max(c.r, Mathf.Max(c.g, c.b)):0.##}";
        }
    }
}
