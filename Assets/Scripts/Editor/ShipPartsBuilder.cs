using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Builds the parts the upgrades bolt onto the player's ship, and lists
    /// them on its prefab for <see cref="ShipParts"/>.
    ///
    /// The design is the ship with every pick taken, worked back from: guns
    /// under the nose, on the pods and on the roof for Hydra; a row of
    /// cooling fins under the belly for Attack Speed; four pieces of plate
    /// for Max HP; boosters, raked wings, wingtip engines and a tail wing
    /// for Move Speed; a ring on the rocket for Dash; a halo on the antenna
    /// for Slow Mo; emitters on the pods for the Deflector; a coil round the canopy
    /// for the Magnet. The starting ship is the model as it was drawn, and
    /// needed no change to take them.
    ///
    /// Every number is in the model's own space, nose along +X, a unit to
    /// a world unit, read off the model's meshes on 16 November 2026: hull
    /// from -0.20 to 0.10 and 0.22 wide, canopy out to 0.17, rocket back to
    /// -0.26 round a centre 0.048 up, pods 0.16 out to each side.
    ///
    /// Designed for one view. The camera sees the ship side-on and nothing
    /// else, so a part earns its place by moving the outline: up, down,
    /// ahead or behind. The first pass was drawn in three views and laid
    /// wings and plate across the flank, where they covered the model's
    /// yellow side panel and showed as a dark mass; that panel is left
    /// clear now.
    ///
    /// The parts are flat-shaded and in the model's own three hull
    /// materials, so they read as more of the same ship. The fourth slot is
    /// a small light in the colour of the upgrade's pickup, which is what
    /// says which pick a part came from.
    /// </summary>
    public static class ShipPartsBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Player/PlayerSpaceShip.prefab";
        private const string ModelPath = "Assets/Art/Models/Ships/nave 01_v06_unity.fbx";
        private const string MeshFolder = "Assets/Art/Models/Ships/Parts";
        private const string MaterialFolder = "Assets/Art/Materials/Ships/Parts";
        private const string SkillFolder = "Assets/Data/Skills";

        private const string HullMaterial = "Materiais.004";
        private const string DarkMaterial = "Materiais";
        private const string TrimMaterial = "Materiais.001";

        /// <summary>
        /// The brightest channel of a part's light. Under the 1.5 a hit flash
        /// is held to would be dim for something this small; much over 2 and
        /// ACES takes the colour out of it.
        /// </summary>
        private const float GlowIntensity = 2.2f;

        private const int Hull = 0;
        private const int Dark = 1;
        private const int Trim = 2;
        private const int Glow = 3;

        /// <summary>The rocket's centre line, which the collar and the ring go round.</summary>
        private const float RocketY = 0.048f;

        /// <summary>The middle of a pod, up and out.</summary>
        private const float PodY = 0.03f;
        private const float PodZ = 0.16f;

        private static readonly Dictionary<ShipUpgrade, string> SkillAssets = new Dictionary<ShipUpgrade, string>
        {
            { ShipUpgrade.Hydra, "ShotUpgrade" },
            { ShipUpgrade.AttackSpeed, "AttackSpeed" },
            { ShipUpgrade.MaxHealth, "MaxHealth" },
            { ShipUpgrade.MoveSpeed, "MoveSpeed" },
            { ShipUpgrade.DashRecovery, "DashRecovery" },
            { ShipUpgrade.SlowMo, "SlowMo" },
            { ShipUpgrade.Deflector, "Deflector" },
            { ShipUpgrade.Magnet, "Magnet" },
        };

        /// <summary>The skill asset behind an upgrade, for the test that holds the two together.</summary>
        public static string SkillPath(ShipUpgrade upgrade)
        {
            return SkillFolder + "/" + SkillAssets[upgrade] + ".asset";
        }

        [MenuItem("Survival Chaos/Gameplay/Build Ship Parts", priority = 128)]
        public static void BuildFromMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var log = new StringBuilder();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (prefab == null)
            {
                return "No prefab at " + PrefabPath + ". Nothing was changed.";
            }

            Material[] model = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>().ToArray();
            Material hull = model.FirstOrDefault(m => m.name == HullMaterial);
            Material dark = model.FirstOrDefault(m => m.name == DarkMaterial);
            Material trim = model.FirstOrDefault(m => m.name == TrimMaterial);

            if (hull == null || dark == null || trim == null)
            {
                return "The model at " + ModelPath + " is missing one of its hull materials. Nothing was changed.";
            }

            EnsureFolder(MeshFolder);
            EnsureFolder(MaterialFolder);

            var stages = new List<ShipPartStage>();
            int triangles = 0;

            foreach (ShipUpgrade upgrade in Enum.GetValues(typeof(ShipUpgrade)))
            {
                Material glow = BuildGlow(upgrade);
                int picks = PickLimit(upgrade);

                for (int pick = 1; pick <= picks; pick++)
                {
                    var kit = new Kit();
                    Shape(kit, upgrade, pick);

                    if (kit.Empty)
                    {
                        log.AppendFormat("{0} {1} has no shape; left off.\n", upgrade, pick);
                        continue;
                    }

                    Mesh mesh = Save(kit, MeshFolder + "/ShipPart " + upgrade + " " + pick + ".asset");
                    triangles += mesh.triangles.Length / 3;

                    stages.Add(new ShipPartStage
                    {
                        upgrade = upgrade,
                        pick = pick,
                        mesh = mesh,
                        materials = new[] { hull, dark, trim, glow },
                    });
                }
            }

            ShipParts parts = prefab.GetComponent<ShipParts>();

            if (parts == null)
            {
                parts = prefab.AddComponent<ShipParts>();
            }

            var serialized = new SerializedObject(parts);
            SerializedProperty list = serialized.FindProperty("stages");
            list.arraySize = stages.Count;

            for (int i = 0; i < stages.Count; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("upgrade").enumValueIndex = (int)stages[i].upgrade;
                entry.FindPropertyRelative("pick").intValue = stages[i].pick;
                entry.FindPropertyRelative("mesh").objectReferenceValue = stages[i].mesh;

                SerializedProperty materials = entry.FindPropertyRelative("materials");
                materials.arraySize = stages[i].materials.Length;

                for (int m = 0; m < stages[i].materials.Length; m++)
                {
                    materials.GetArrayElementAtIndex(m).objectReferenceValue = stages[i].materials[m];
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
            AssetDatabase.SaveAssets();

            log.AppendFormat("{0} parts, {1} triangles with every pick taken.\n", stages.Count, triangles);
            return log.ToString();
        }

        /// <summary>How many picks of an upgrade a run can take: one part each.</summary>
        private static int PickLimit(ShipUpgrade upgrade)
        {
            SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SkillPath(upgrade));
            return skill != null ? Mathf.Max(0, skill.MaxPicks) : 0;
        }

        // ------------------------------------------------------------ shapes

        private static void Shape(Kit kit, ShipUpgrade upgrade, int pick)
        {
            switch (upgrade)
            {
                case ShipUpgrade.Hydra: Guns(kit, pick); break;
                case ShipUpgrade.AttackSpeed: CoolingFin(kit, pick); break;
                case ShipUpgrade.MaxHealth: Plate(kit, pick); break;
                case ShipUpgrade.MoveSpeed: Engines(kit, pick); break;
                case ShipUpgrade.DashRecovery: BurnerRing(kit); break;
                case ShipUpgrade.SlowMo: Halo(kit, pick); break;
                case ShipUpgrade.Deflector: Emitters(kit, pick); break;
                case ShipUpgrade.Magnet: Coil(kit); break;
            }
        }

        /// <summary>A barrel from a mount to a muzzle, with a collar and a lit bore.</summary>
        private static void Barrel(Kit kit, Vector3 from, float length, float radius)
        {
            Vector3 muzzle = from + Vector3.right * length;

            kit.Material(Dark);
            kit.Tube(from, muzzle, radius, radius, 6);

            kit.Material(Trim);
            kit.Tube(muzzle - Vector3.right * 0.03f, muzzle + Vector3.right * 0.004f, radius * 1.55f, radius * 1.55f, 6);

            kit.Material(Glow);
            kit.Tube(muzzle + Vector3.right * 0.004f, muzzle + Vector3.right * 0.008f, radius * 0.9f, radius * 0.5f, 6);
        }

        /// <summary>
        /// Hydra: two guns on the blocks under the nose, two more on the
        /// pods, and a battery on the roof. The eye counts three rows of
        /// barrels down the side; the gun fires two, four, then eight.
        /// </summary>
        private static void Guns(Kit kit, int pick)
        {
            if (pick == 1)
            {
                kit.Both(() => Barrel(kit, new Vector3(0.11f, -0.115f, 0.038f), 0.125f, 0.014f));
            }
            else if (pick == 2)
            {
                kit.Both(() => Barrel(kit, new Vector3(0.06f, PodY, PodZ), 0.15f, 0.013f));
            }
            else
            {
                kit.Material(Dark);
                kit.Box(new Vector3(-0.005f, 0.14f, 0f), new Vector3(0.09f, 0.03f, 0.075f));

                kit.Material(Hull);
                kit.Hex(
                    new Vector3(-0.07f, 0.145f, -0.052f), new Vector3(0.05f, 0.145f, -0.052f),
                    new Vector3(0.05f, 0.145f, 0.052f), new Vector3(-0.07f, 0.145f, 0.052f),
                    new Vector3(-0.05f, 0.185f, -0.04f), new Vector3(0.05f, 0.18f, -0.04f),
                    new Vector3(0.05f, 0.18f, 0.04f), new Vector3(-0.05f, 0.185f, 0.04f));

                kit.Material(Trim);
                kit.Box(new Vector3(-0.01f, 0.186f, 0f), new Vector3(0.06f, 0.006f, 0.03f));

                kit.Both(() => Barrel(kit, new Vector3(0.045f, 0.163f, 0.024f), 0.15f, 0.012f));
            }
        }

        /// <summary>
        /// Attack Speed: one swept fin under the belly a pick, front to back,
        /// six in all. A gun that fires faster runs hotter.
        /// </summary>
        private static void CoolingFin(Kit kit, int pick)
        {
            float x = -0.03f - 0.024f * (pick - 1);

            kit.Material(Trim);
            kit.Extrude(-0.055f, 0.055f,
                new Vector2(x + 0.012f, -0.03f), new Vector2(x - 0.003f, -0.03f),
                new Vector2(x - 0.026f, -0.13f), new Vector2(x - 0.012f, -0.13f));

            kit.Material(Glow);
            kit.Box(new Vector3(x - 0.02f, -0.136f, 0f), new Vector3(0.015f, 0.014f, 0.112f));
        }

        /// <summary>
        /// Max HP: a brow over the canopy, skirts under the side panel, a
        /// skid under the nose, a collar round the rocket.
        /// </summary>
        private static void Plate(Kit kit, int pick)
        {
            if (pick == 1)
            {
                kit.Material(Dark);
                kit.Extrude(-0.082f, 0.082f,
                    new Vector2(0.066f, 0.126f), new Vector2(0.066f, 0.152f),
                    new Vector2(0.136f, 0.139f), new Vector2(0.128f, 0.117f));
                kit.Extrude(-0.082f, 0.082f,
                    new Vector2(0.128f, 0.117f), new Vector2(0.136f, 0.139f),
                    new Vector2(0.182f, 0.084f), new Vector2(0.164f, 0.078f));

                kit.Material(Trim);
                kit.Box(new Vector3(0.174f, 0.079f, 0f), new Vector3(0.016f, 0.014f, 0.172f), Quaternion.Euler(0f, 0f, -50f));

                kit.Material(Glow);
                kit.Box(new Vector3(0.1f, 0.149f, 0f), new Vector3(0.03f, 0.006f, 0.03f), Quaternion.Euler(0f, 0f, -11f));
            }
            else if (pick == 2)
            {
                kit.Both(() =>
                {
                    // Dark, bordered in trim: in the hull's own blue it was
                    // a plate nobody could see against the hull.
                    kit.Material(Dark);
                    kit.Extrude(0.107f, 0.127f,
                        new Vector2(-0.172f, -0.03f), new Vector2(0.066f, -0.03f),
                        new Vector2(0.066f, -0.066f), new Vector2(0.04f, -0.1f),
                        new Vector2(-0.15f, -0.1f), new Vector2(-0.172f, -0.05f));

                    kit.Material(Trim);
                    kit.Box(new Vector3(-0.053f, -0.034f, 0.129f), new Vector3(0.238f, 0.01f, 0.006f));
                    kit.Box(new Vector3(-0.055f, -0.096f, 0.129f), new Vector3(0.19f, 0.01f, 0.006f));

                    kit.Material(Glow);
                    kit.Box(new Vector3(-0.11f, -0.065f, 0.129f), new Vector3(0.026f, 0.026f, 0.006f));
                    kit.Box(new Vector3(-0.05f, -0.065f, 0.129f), new Vector3(0.026f, 0.026f, 0.006f));
                    kit.Box(new Vector3(0.01f, -0.065f, 0.129f), new Vector3(0.026f, 0.026f, 0.006f));
                });
            }
            else if (pick == 3)
            {
                // A skid under the nose blocks: the one piece of plate that
                // moves the ship's bottom edge.
                kit.Material(Dark);
                kit.Extrude(-0.082f, 0.082f,
                    new Vector2(-0.034f, -0.15f), new Vector2(-0.022f, -0.184f),
                    new Vector2(0.12f, -0.184f), new Vector2(0.12f, -0.15f));
                kit.Extrude(-0.082f, 0.082f,
                    new Vector2(0.12f, -0.15f), new Vector2(0.12f, -0.184f),
                    new Vector2(0.168f, -0.156f), new Vector2(0.156f, -0.142f));

                kit.Material(Trim);
                kit.Box(new Vector3(0.049f, -0.187f, 0f), new Vector3(0.146f, 0.01f, 0.17f));

                kit.Material(Glow);
                kit.Box(new Vector3(0.05f, -0.167f, 0f), new Vector3(0.05f, 0.016f, 0.168f));
            }
            else
            {
                var front = new Vector3(-0.155f, RocketY, 0f);
                var back = new Vector3(-0.218f, RocketY, 0f);

                // Twice the girth of the rocket's neck, which is the thin
                // part of the ship's outline from the side.
                kit.Material(Hull);
                kit.Tube(front, back, 0.094f, 0.084f, 8);

                kit.Material(Trim);
                kit.Tube(front + Vector3.left * 0.008f, front + Vector3.left * 0.022f, 0.099f, 0.098f, 8);
                kit.Tube(back + Vector3.right * 0.016f, back + Vector3.right * 0.004f, 0.09f, 0.088f, 8);

                kit.Material(Glow);
                kit.Tube(front + Vector3.left * 0.028f, front + Vector3.left * 0.042f, 0.094f, 0.092f, 8);
            }
        }

        /// <summary>A small rocket: a nose, a body, a bell, a band and a lit throat.</summary>
        private static void Rocket(Kit kit, Vector3 nose, float length, float radius)
        {
            Vector3 shoulder = nose + Vector3.left * (length * 0.2f);
            Vector3 waist = nose + Vector3.left * (length * 0.8f);
            Vector3 mouth = nose + Vector3.left * length;

            kit.Material(Hull);
            kit.Tube(nose, shoulder, radius * 0.35f, radius, 8);
            kit.Tube(shoulder, waist, radius, radius * 0.9f, 8);

            kit.Material(Dark);
            kit.Tube(waist, mouth, radius * 0.8f, radius * 1.3f, 8);

            kit.Material(Trim);
            kit.Tube(shoulder + Vector3.left * (length * 0.12f), shoulder + Vector3.left * (length * 0.24f),
                radius * 1.08f, radius * 1.06f, 8);

            kit.Material(Glow);
            kit.Tube(mouth + Vector3.right * 0.004f, mouth + Vector3.left * 0.003f, radius * 1.05f, radius * 0.7f, 8);
        }

        /// <summary>
        /// Move Speed: boosters under the tail, wings raked up off the shoulders,
        /// an engine on each wingtip, and a tail wing over the rocket.
        /// </summary>
        private static void Engines(Kit kit, int pick)
        {
            if (pick == 1)
            {
                kit.Both(() => Rocket(kit, new Vector3(-0.105f, -0.042f, 0.064f), 0.175f, 0.028f));
            }
            else if (pick == 2)
            {
                kit.Both(() =>
                {
                    // Up off the shoulder and raked back, leaning out a
                    // little. The camera only ever sees the ship side-on,
                    // and a wing laid flat showed it an edge and a slab
                    // across the flank.
                    kit.Material(Hull);
                    kit.Hex(
                        new Vector3(-0.1f, 0.118f, 0.098f), new Vector3(0.022f, 0.118f, 0.098f),
                        new Vector3(0.022f, 0.118f, 0.114f), new Vector3(-0.1f, 0.118f, 0.114f),
                        new Vector3(-0.135f, 0.27f, 0.192f), new Vector3(-0.058f, 0.27f, 0.192f),
                        new Vector3(-0.058f, 0.27f, 0.204f), new Vector3(-0.135f, 0.27f, 0.204f));

                    kit.Material(Trim);
                    kit.Hex(
                        new Vector3(0.022f, 0.118f, 0.096f), new Vector3(0.04f, 0.118f, 0.096f),
                        new Vector3(0.04f, 0.118f, 0.116f), new Vector3(0.022f, 0.118f, 0.116f),
                        new Vector3(-0.058f, 0.27f, 0.19f), new Vector3(-0.042f, 0.27f, 0.19f),
                        new Vector3(-0.042f, 0.27f, 0.206f), new Vector3(-0.058f, 0.27f, 0.206f));

                    kit.Material(Glow);
                    kit.Hex(
                        new Vector3(-0.085f, 0.17f, 0.146f), new Vector3(-0.05f, 0.17f, 0.146f),
                        new Vector3(-0.05f, 0.17f, 0.152f), new Vector3(-0.085f, 0.17f, 0.152f),
                        new Vector3(-0.105f, 0.225f, 0.178f), new Vector3(-0.07f, 0.225f, 0.178f),
                        new Vector3(-0.07f, 0.225f, 0.184f), new Vector3(-0.105f, 0.225f, 0.184f));
                });
            }
            else if (pick == 3)
            {
                kit.Both(() => Rocket(kit, new Vector3(-0.005f, 0.278f, 0.2f), 0.155f, 0.022f));
            }
            else
            {
                // Behind the antenna and above the rocket, where nothing
                // else is: fins on the roof stood between the camera and
                // the Slow Mo halo.
                kit.Material(Hull);
                kit.Hex(
                    new Vector3(-0.29f, 0.208f, -0.13f), new Vector3(-0.225f, 0.212f, -0.13f),
                    new Vector3(-0.225f, 0.212f, 0.13f), new Vector3(-0.29f, 0.208f, 0.13f),
                    new Vector3(-0.29f, 0.218f, -0.13f), new Vector3(-0.225f, 0.226f, -0.13f),
                    new Vector3(-0.225f, 0.226f, 0.13f), new Vector3(-0.29f, 0.218f, 0.13f));

                kit.Material(Glow);
                kit.Box(new Vector3(-0.292f, 0.213f, 0f), new Vector3(0.006f, 0.008f, 0.2f));

                kit.Both(() =>
                {
                    kit.Material(Trim);
                    kit.Extrude(0.13f, 0.142f,
                        new Vector2(-0.305f, 0.185f), new Vector2(-0.225f, 0.185f),
                        new Vector2(-0.21f, 0.25f), new Vector2(-0.275f, 0.25f));

                    kit.Material(Dark);
                    kit.Hex(
                        new Vector3(-0.2f, 0.1f, 0.05f), new Vector3(-0.17f, 0.1f, 0.05f),
                        new Vector3(-0.17f, 0.1f, 0.066f), new Vector3(-0.2f, 0.1f, 0.066f),
                        new Vector3(-0.27f, 0.21f, 0.05f), new Vector3(-0.245f, 0.21f, 0.05f),
                        new Vector3(-0.245f, 0.21f, 0.066f), new Vector3(-0.27f, 0.21f, 0.066f));
                });
            }
        }

        /// <summary>Dash: a burner ring round the mouth of the rocket.</summary>
        private static void BurnerRing(Kit kit)
        {
            var centre = new Vector3(-0.258f, RocketY, 0f);

            kit.Material(Trim);
            kit.Torus(centre, Vector3.right, 0.086f, 0.016f, 12, 5);

            kit.Material(Glow);
            kit.Torus(centre + Vector3.left * 0.016f, Vector3.right, 0.086f, 0.009f, 12, 4);
        }

        /// <summary>
        /// Slow Mo: a ring round the ball of the antenna with a lit core,
        /// then a second, wider ring tipped across the first.
        /// </summary>
        private static void Halo(Kit kit, int pick)
        {
            var ball = new Vector3(-0.176f, 0.16f, 0.001f);

            if (pick == 1)
            {
                // Flat to the camera, so it is a circle and not a line.
                kit.Material(Trim);
                kit.Torus(ball, Vector3.forward, 0.04f, 0.007f, 14, 4);

                kit.Material(Glow);
                kit.Gem(ball, 0.02f);
            }
            else
            {
                kit.Material(Glow);
                kit.Torus(ball, new Vector3(0.75f, 0.35f, 0.56f).normalized, 0.058f, 0.006f, 16, 4);
            }
        }

        /// <summary>Deflector: an emitter on top of each pod, then one under each.</summary>
        private static void Emitters(Kit kit, int pick)
        {
            float side = pick == 1 ? 1f : -1f;

            kit.Both(() =>
            {
                kit.Material(Dark);
                kit.Tube(
                    new Vector3(-0.06f, PodY + side * 0.03f, PodZ), new Vector3(-0.06f, PodY + side * 0.07f, PodZ),
                    0.022f, 0.012f, 6);

                kit.Material(Trim);
                kit.Tube(
                    new Vector3(-0.06f, PodY + side * 0.066f, PodZ), new Vector3(-0.06f, PodY + side * 0.074f, PodZ),
                    0.019f, 0.019f, 6);

                kit.Material(Glow);
                kit.Gem(new Vector3(-0.06f, PodY + side * 0.098f, PodZ), 0.027f);
            });
        }

        /// <summary>Magnet: a coil round the foot of the canopy, with three lit poles.</summary>
        private static void Coil(Kit kit)
        {
            var centre = new Vector3(0.108f, 0.03f, 0f);

            kit.Material(Trim);
            kit.Torus(centre, Vector3.right, 0.104f, 0.015f, 14, 5);

            kit.Material(Glow);
            kit.Box(centre + Vector3.down * 0.106f, new Vector3(0.04f, 0.03f, 0.06f));
            kit.Box(centre + Vector3.forward * 0.106f, new Vector3(0.04f, 0.06f, 0.03f));
            kit.Box(centre + Vector3.back * 0.106f, new Vector3(0.04f, 0.06f, 0.03f));
        }

        // ------------------------------------------------------------ assets

        /// <summary>
        /// A part's light, in the colour of its upgrade's pickup: lit, so a
        /// hit flash reaches it like the rest of the hull.
        /// </summary>
        private static Material BuildGlow(ShipUpgrade upgrade)
        {
            string path = MaterialFolder + "/ShipPartGlow " + upgrade + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("HDRP/Lit");

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            Color colour = new Color(1f, 1f, 1f);
            SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SkillPath(upgrade));

            if (skill != null)
            {
                colour = skill.PickupColor;
            }

            float peak = Mathf.Max(0.0001f, colour.maxColorComponent);
            Color unit = new Color(colour.r / peak, colour.g / peak, colour.b / peak, 1f);

            material.SetColor("_BaseColor", unit * 0.25f + new Color(0f, 0f, 0f, 1f));
            material.SetFloat("_Smoothness", 0.5f);
            material.SetFloat("_UseEmissiveIntensity", 0f);
            material.SetColor("_EmissiveColorLDR", unit);
            material.SetColor("_EmissiveColor", unit * GlowIntensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh Save(Kit kit, string path)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;

            if (created)
            {
                mesh = new Mesh();
            }

            kit.Fill(mesh);
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);

            if (created)
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }

            return mesh;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            int slash = folder.LastIndexOf('/');
            EnsureFolder(folder.Substring(0, slash));
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }

        // --------------------------------------------------------------- kit

        /// <summary>
        /// Flat-shaded solids in four materials. Every face is turned to
        /// look away from a point inside its solid, so no shape here has to
        /// get its winding right, and a mirrored one cannot get it wrong.
        /// </summary>
        private sealed class Kit
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int>[] triangles =
            {
                new List<int>(), new List<int>(), new List<int>(), new List<int>(),
            };

            private int material;
            private bool mirrored;

            public bool Empty => vertices.Count == 0;

            public void Material(int index)
            {
                material = index;
            }

            /// <summary>Builds a shape on the +Z side and again mirrored to -Z.</summary>
            public void Both(Action shape)
            {
                mirrored = false;
                shape();
                mirrored = true;
                shape();
                mirrored = false;
            }

            public void Fill(Mesh mesh)
            {
                mesh.Clear();
                mesh.SetVertices(vertices);
                mesh.subMeshCount = triangles.Length;

                for (int i = 0; i < triangles.Length; i++)
                {
                    mesh.SetTriangles(triangles[i], i);
                }

                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }

            private Vector3 Place(Vector3 point)
            {
                return mirrored ? new Vector3(point.x, point.y, -point.z) : point;
            }

            private void Face(Vector3 inside, params Vector3[] corners)
            {
                inside = Place(inside);

                var placed = new Vector3[corners.Length];
                Vector3 middle = Vector3.zero;

                for (int i = 0; i < corners.Length; i++)
                {
                    placed[i] = Place(corners[i]);
                    middle += placed[i];
                }

                middle /= corners.Length;

                Vector3 normal = Vector3.zero;

                for (int i = 1; i < placed.Length - 1; i++)
                {
                    normal += Vector3.Cross(placed[i] - placed[0], placed[i + 1] - placed[0]);
                }

                if (normal.sqrMagnitude < 1e-14f)
                {
                    return;
                }

                bool flip = Vector3.Dot(normal, middle - inside) < 0f;

                for (int i = 1; i < placed.Length - 1; i++)
                {
                    int first = vertices.Count;
                    vertices.Add(placed[0]);
                    vertices.Add(placed[flip ? i + 1 : i]);
                    vertices.Add(placed[flip ? i : i + 1]);
                    triangles[material].Add(first);
                    triangles[material].Add(first + 1);
                    triangles[material].Add(first + 2);
                }
            }

            /// <summary>Six faces between a ring of four corners and the ring of four across from it.</summary>
            public void Hex(Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3, Vector3 b0, Vector3 b1, Vector3 b2, Vector3 b3)
            {
                Vector3 inside = (a0 + a1 + a2 + a3 + b0 + b1 + b2 + b3) / 8f;

                Face(inside, a0, a1, a2, a3);
                Face(inside, b0, b1, b2, b3);
                Face(inside, a0, a1, b1, b0);
                Face(inside, a1, a2, b2, b1);
                Face(inside, a2, a3, b3, b2);
                Face(inside, a3, a0, b0, b3);
            }

            public void Box(Vector3 centre, Vector3 size)
            {
                Box(centre, size, Quaternion.identity);
            }

            public void Box(Vector3 centre, Vector3 size, Quaternion turn)
            {
                Vector3 h = size * 0.5f;

                Vector3 Corner(float x, float y, float z)
                {
                    return centre + turn * new Vector3(x * h.x, y * h.y, z * h.z);
                }

                Hex(Corner(-1, -1, -1), Corner(1, -1, -1), Corner(1, -1, 1), Corner(-1, -1, 1),
                    Corner(-1, 1, -1), Corner(1, 1, -1), Corner(1, 1, 1), Corner(-1, 1, 1));
            }

            /// <summary>A convex outline drawn in X and Y, given thickness from one Z to another.</summary>
            public void Extrude(float nearZ, float farZ, params Vector2[] outline)
            {
                int count = outline.Length;
                var near = new Vector3[count];
                var far = new Vector3[count];
                Vector3 inside = Vector3.zero;

                for (int i = 0; i < count; i++)
                {
                    near[i] = new Vector3(outline[i].x, outline[i].y, nearZ);
                    far[i] = new Vector3(outline[i].x, outline[i].y, farZ);
                    inside += near[i] + far[i];
                }

                inside /= count * 2;

                Face(inside, near);
                Face(inside, far);

                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    Face(inside, near[i], near[next], far[next], far[i]);
                }
            }

            /// <summary>A capped run of sides from one end to the other, wider or narrower as it goes.</summary>
            public void Tube(Vector3 from, Vector3 to, float fromRadius, float toRadius, int sides)
            {
                Vector3 axis = (to - from).normalized;
                Vector3 across = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 other = Vector3.Cross(axis, across);
                Vector3 inside = (from + to) * 0.5f;

                var near = new Vector3[sides];
                var far = new Vector3[sides];

                for (int i = 0; i < sides; i++)
                {
                    float angle = (i + 0.5f) * Mathf.PI * 2f / sides;
                    Vector3 spoke = across * Mathf.Cos(angle) + other * Mathf.Sin(angle);
                    near[i] = from + spoke * fromRadius;
                    far[i] = to + spoke * toRadius;
                }

                Face(inside, near);
                Face(inside, far);

                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    Face(inside, near[i], near[next], far[next], far[i]);
                }
            }

            public void Torus(Vector3 centre, Vector3 axis, float radius, float thickness, int segments, int sides)
            {
                axis = axis.normalized;
                Vector3 across = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 other = Vector3.Cross(axis, across);

                var rings = new Vector3[segments][];
                var middles = new Vector3[segments];

                for (int s = 0; s < segments; s++)
                {
                    float around = s * Mathf.PI * 2f / segments;
                    Vector3 spoke = across * Mathf.Cos(around) + other * Mathf.Sin(around);
                    middles[s] = centre + spoke * radius;
                    rings[s] = new Vector3[sides];

                    for (int i = 0; i < sides; i++)
                    {
                        float angle = (i + 0.5f) * Mathf.PI * 2f / sides;
                        rings[s][i] = middles[s] + (spoke * Mathf.Cos(angle) + axis * Mathf.Sin(angle)) * thickness;
                    }
                }

                for (int s = 0; s < segments; s++)
                {
                    int nextRing = (s + 1) % segments;
                    Vector3 inside = (middles[s] + middles[nextRing]) * 0.5f;

                    for (int i = 0; i < sides; i++)
                    {
                        int next = (i + 1) % sides;
                        Face(inside, rings[s][i], rings[s][next], rings[nextRing][next], rings[nextRing][i]);
                    }
                }
            }

            /// <summary>Eight faces: the smallest thing that reads as a point of light.</summary>
            public void Gem(Vector3 centre, float radius)
            {
                Vector3[] tips =
                {
                    centre + Vector3.right * radius, centre + Vector3.forward * radius,
                    centre + Vector3.left * radius, centre + Vector3.back * radius,
                };

                Vector3 top = centre + Vector3.up * radius;
                Vector3 bottom = centre + Vector3.down * radius;

                for (int i = 0; i < 4; i++)
                {
                    Face(centre, tips[i], tips[(i + 1) % 4], top);
                    Face(centre, tips[i], tips[(i + 1) % 4], bottom);
                }
            }
        }
    }
}
