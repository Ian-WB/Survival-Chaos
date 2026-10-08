using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// The colour of everything that can hurt the ship, held in one place.
    ///
    /// Until 5 October 2026 hostile fire was red-orange: enemy rounds at hue
    /// 12, the Leviathan's rounds at 14, its discs at 13, the lance's glow at
    /// 16. The lava's hot colour is hue 12. Measured on a frozen frame with the
    /// rounds laid out across the arena, an enemy round over the lava flow sat
    /// 13 apart from it (CIE76 in Lab, where about 10 is a clear difference),
    /// and a Leviathan round 23. Everything that could hurt the ship was the
    /// colour of the largest bright thing on screen.
    ///
    /// For a few hours that day it was cyan, the lava's complement. It measured
    /// well and read wrong: on screen it was a pale ice blue, the colour of
    /// the pickups and the HUD, and nothing about it said enemy.
    ///
    /// It is violet now, and dimmer, and the second half matters as much as
    /// the first. At a peak of 3 every hue here comes out of the tonemapper as
    /// a pastel: the cyan as ice, a red as salmon, a violet as pink. That is
    /// why no hostile round reads as hostile at that brightness, whatever its
    /// colour. Below about 2 the hue survives. So this file sets each round's
    /// peak as well as its colour, and they sit under the pickups, which are
    /// at about 3.
    ///
    /// Measured again on a frozen frame, each round against the pixels it
    /// covers, worst of nine places across the arena: an enemy round 56 where
    /// the cyan had 40 and the orange 32, a Leviathan round 69 against 41 and
    /// 19 (83 over the lava flow itself), a disc 74 against 35 and 49. Under
    /// protanopia and deuteranopia the worst is 41. A red at the same peaks
    /// reads as the most hostile of all and measures the worst: 6 to 29 for
    /// the two colour-blind cases, since it is the lava's own red.
    ///
    /// The pods stay red. That makes the rule the boss is read by: red is what
    /// to shoot, violet is what to dodge. It also undoes half of what 5
    /// September did, when the rounds, the lance and the pods were brought
    /// into one warm family.
    ///
    /// Two pickups share the hue, Magnet and Max Health, as Shot Upgrade
    /// shared the orange. They are told apart from a round the way they were
    /// then: a pickup is near white with a caption over it, and stands still.
    /// </summary>
    public static class HostileFireColour
    {
        /// <summary>
        /// A round's body, a disc, a muzzle glow and the lance's edge: hue 263,
        /// as ratios with the brightest channel at 1.
        /// </summary>
        public static readonly Vector3 Bright = new Vector3(0.42f, 0.06f, 1.00f);

        /// <summary>
        /// The dimmer second surface of a round, and the lance's halo: a shell
        /// or a tip, which sits a little deeper in the blue than the body.
        /// </summary>
        public static readonly Vector3 Deep = new Vector3(0.31f, 0.02f, 1.00f);

        /// <summary>The lance's centre line: pale and hot, with the hue only just in it.</summary>
        public static readonly Vector3 Core = new Vector3(0.82f, 0.72f, 1.00f);

        /// <summary>
        /// No round's emission may peak above this. It is where the hue starts
        /// to wash out; HostileFireColourTests holds the materials to it.
        /// </summary>
        public const float PeakCeiling = 2f;

        private const string VfxFolder = "Assets/Art/Materials/VFX/";

        /// <summary>The scene light every hostile round borrows a copy of.</summary>
        private const string RoundLightName = "Boss Bullet Light Template";

        private readonly struct LitRound
        {
            public LitRound(string path, Vector3 shade, float peak, bool tintBase)
            {
                Path = path;
                Shade = shade;
                Peak = peak;
                TintBase = tintBase;
            }

            public string Path { get; }

            public Vector3 Shade { get; }

            /// <summary>The emission's brightest channel. The ratios come from the shade.</summary>
            public float Peak { get; }

            /// <summary>
            /// Whether the surface under the glow is coloured too. The enemy
            /// rounds are grey underneath and stay grey.
            /// </summary>
            public bool TintBase { get; }
        }

        private static readonly LitRound[] LitRounds =
        {
            // The peaks were 1.3, 0.65, 1.6, 3, 1.1 and 2.4 until 5 October 2026.
            new LitRound(VfxFolder + "EnemyShot.mat", Bright, 1.1f, false),
            new LitRound(VfxFolder + "EnemyShotTip.mat", Deep, 0.55f, false),
            new LitRound("Assets/Art/Materials/Ships/New Material.mat", Bright, 1.6f, true),
            new LitRound(VfxFolder + "BossRound.mat", Bright, 1.6f, true),
            new LitRound(VfxFolder + "BossRoundShell.mat", Deep, 0.75f, true),
            new LitRound(VfxFolder + "BossDisc.mat", Bright, 1.35f, true),
        };

        [MenuItem("Survival Chaos/Gameplay/Apply Hostile Fire Colour", priority = 126)]
        public static void ApplyFromMenu()
        {
            Debug.Log(Apply());
        }

        /// <summary>
        /// Writes the colour into every hostile material and, where the Game
        /// scene is open, the light the rounds carry. Re-running is safe: a
        /// material already on the colour is left alone. Each material is saved
        /// on its own, so nothing else that happens to be unsaved rides along.
        /// </summary>
        public static string Apply()
        {
            var log = new StringBuilder();
            int changed = 0;

            foreach (LitRound round in LitRounds)
            {
                changed += ApplyLit(round, log) ? 1 : 0;
            }

            changed += ApplyVector(VfxFolder + "BossLance.mat", "_CoreColor", Core, log) ? 1 : 0;
            changed += ApplyVector(VfxFolder + "BossLance.mat", "_BodyColor", Bright, log) ? 1 : 0;
            changed += ApplyVector(VfxFolder + "BossLanceGlow.mat", "_GlowColor", Deep, log) ? 1 : 0;
            changed += ApplyVector(VfxFolder + "BossMuzzleTell.mat", "_FlameColor", Bright, log) ? 1 : 0;

            ApplyRoundLight(log);

            log.Append("Hostile fire colour: ").Append(changed).Append(" material value(s) changed.");
            return log.ToString();
        }

        /// <summary>
        /// One HDRP Lit round: the emission moves to the shade at the round's
        /// peak. The surface under it, where that is coloured, takes the shade
        /// at the brightness it already had.
        /// </summary>
        private static bool ApplyLit(LitRound round, StringBuilder log)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(round.Path);
            if (material == null)
            {
                log.AppendLine("Not found: " + round.Path);
                return false;
            }

            Color glowBefore = material.GetColor("_EmissiveColor");
            Color glowAfter = AtPeak(round.Shade, round.Peak, glowBefore.a);

            Color baseBefore = material.GetColor("_BaseColor");
            Color baseAfter = round.TintBase ? AtPeak(round.Shade, Peak(baseBefore), baseBefore.a) : baseBefore;

            if (Same(glowBefore, glowAfter) && Same(baseBefore, baseAfter))
            {
                return false;
            }

            // Off before the colour is written: while the intensity mode is on,
            // HDRP rewrites the colour from its own swatch on every validate.
            HDMaterial.SetUseEmissiveIntensity(material, false);
            HDMaterial.SetEmissiveColor(material, glowAfter);

            // Only where it changes. Writing a colour back through SetColor
            // returns it a rounding error away from what was stored.
            if (round.TintBase)
            {
                material.SetColor("_BaseColor", baseAfter);
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);

            log.AppendLine(material.name + ": glow " + Describe(glowBefore) + " -> " + Describe(glowAfter));
            return true;
        }

        /// <summary>A shader graph material, whose colours are plain vectors.</summary>
        private static bool ApplyVector(string path, string property, Vector3 shade, StringBuilder log)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || !material.HasProperty(property))
            {
                log.AppendLine("Not found: " + property + " on " + path);
                return false;
            }

            Vector4 before = material.GetVector(property);
            var after = new Vector4(shade.x, shade.y, shade.z, before.w);

            if ((before - after).sqrMagnitude < 1e-6f)
            {
                return false;
            }

            material.SetVector(property, after);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);

            log.AppendLine(material.name + " " + property + ": " + Describe((Color)before) + " -> " + Describe((Color)after));
            return true;
        }

        /// <summary>
        /// The light under the rounds. It is an object in the Game scene, not an
        /// asset, so it needs that scene open; the scene is left dirty for
        /// whoever ran this to save.
        /// </summary>
        private static void ApplyRoundLight(StringBuilder log)
        {
            Light template = null;

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.name == RoundLightName)
                {
                    template = light;
                    break;
                }
            }

            if (template == null)
            {
                log.AppendLine("No " + RoundLightName + " in the open scene. Open Game and run this again.");
                return;
            }

            var after = new Color(Bright.x, Bright.y, Bright.z, template.color.a);
            if (Same(template.color, after))
            {
                return;
            }

            log.AppendLine(RoundLightName + ": " + Describe(template.color) + " -> " + Describe(after) +
                           ". Save the scene to keep it.");

            template.color = after;
            EditorUtility.SetDirty(template);
            EditorSceneManager.MarkSceneDirty(template.gameObject.scene);
        }

        private static float Peak(Color colour)
        {
            return Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
        }

        private static Color AtPeak(Vector3 shade, float peak, float alpha)
        {
            return new Color(shade.x * peak, shade.y * peak, shade.z * peak, alpha);
        }

        private static bool Same(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.001f
                   && Mathf.Abs(a.g - b.g) < 0.001f
                   && Mathf.Abs(a.b - b.b) < 0.001f;
        }

        private static string Describe(Color c)
        {
            return $"({c.r:0.##}, {c.g:0.##}, {c.b:0.##})";
        }
    }
}
