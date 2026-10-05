using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// That nothing which can hurt the ship is the colour of the lava, or of
    /// the ship's own rounds, or so bright that it has no colour left.
    ///
    /// Hostile fire was red-orange until 5 October 2026, within four degrees of
    /// hue of the lava it was fired across, and a round over the flow could not
    /// be picked out of it. It is violet now, after a few hours as a cyan that
    /// did not read as an enemy's. The colour is written by the editor tool
    /// HostileFireColour, which this assembly cannot see, so these read what
    /// it wrote: the materials, and the light the rounds carry.
    ///
    /// What would undo it is one material edited by hand, or a new hostile
    /// round given the old colour because the ones beside it used to have it.
    /// </summary>
    public class HostileFireColourTests
    {
        private const string Vfx = "Assets/Art/Materials/VFX/";

        /// <summary>Each hostile surface: a material and the property its colour is in.</summary>
        private static readonly string[,] Hostile =
        {
            { Vfx + "EnemyShot.mat", "_EmissiveColor" },
            { Vfx + "EnemyShotTip.mat", "_EmissiveColor" },
            { "Assets/Art/Materials/Ships/New Material.mat", "_EmissiveColor" },
            { Vfx + "BossRound.mat", "_EmissiveColor" },
            { Vfx + "BossRoundShell.mat", "_EmissiveColor" },
            { Vfx + "BossDisc.mat", "_EmissiveColor" },
            { Vfx + "BossLance.mat", "_BodyColor" },
            { Vfx + "BossLanceGlow.mat", "_GlowColor" },
            { Vfx + "BossMuzzleTell.mat", "_FlameColor" },
        };

        /// <summary>
        /// How far round the wheel hostile fire has to sit from the lava. The
        /// orange was 0 to 11 away. The violet is 106 to 111 away.
        /// </summary>
        private const float FromLava = 90f;

        /// <summary>And from the ship's own rounds, which are green: 129 today.</summary>
        private const float FromOurRounds = 40f;

        /// <summary>
        /// The brightest an HDRP Lit round's emission may be. Above about 2 the
        /// tonemapper turns any hue into a pastel, which is how a cyan became
        /// ice blue and would turn this violet pink. The rounds were at 3 and
        /// 2.4 until 5 October 2026. HostileFireColour.PeakCeiling is the same
        /// number, on the side that writes them.
        /// </summary>
        private const float PeakCeiling = 2f;

        private static float Hue(Color colour)
        {
            float peak = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
            Assert.That(peak, Is.GreaterThan(0f), "a black colour has no hue");

            Color.RGBToHSV(new Color(colour.r / peak, colour.g / peak, colour.b / peak), out float hue, out _, out _);
            return hue * 360f;
        }

        private static Color Read(string path, string property)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(material, Is.Not.Null, path + " is missing");
            Assert.That(material.HasProperty(property), Is.True, path + " has no " + property);

            // The shader graphs hold their colours as vectors, HDRP Lit as
            // colours. Both come back through GetVector as the stored numbers.
            Vector4 stored = material.GetVector(property);
            return new Color(stored.x, stored.y, stored.z);
        }

        private static float LavaHue()
        {
            return Hue(Read("Assets/Art/Materials/Scenario/lava_.mat", "_HotColor"));
        }

        private static float OurRoundsHue()
        {
            return Hue(Read(Vfx + "PlayerRound.mat", "_BodyColor"));
        }

        [Test]
        public void EveryHostileSurface_IsFarFromTheLava_AndFromOurOwnRounds()
        {
            float lava = LavaHue();
            float ours = OurRoundsHue();
            var wrong = new List<string>();

            for (int i = 0; i < Hostile.GetLength(0); i++)
            {
                float hue = Hue(Read(Hostile[i, 0], Hostile[i, 1]));

                if (Mathf.Abs(Mathf.DeltaAngle(hue, lava)) < FromLava)
                {
                    wrong.Add(Hostile[i, 0] + ": hue " + hue.ToString("0") + ", the lava is " + lava.ToString("0"));
                }

                if (Mathf.Abs(Mathf.DeltaAngle(hue, ours)) < FromOurRounds)
                {
                    wrong.Add(Hostile[i, 0] + ": hue " + hue.ToString("0") + ", our rounds are " + ours.ToString("0"));
                }
            }

            Assert.That(wrong, Is.Empty);
        }

        [Test]
        public void NoHostileRound_IsBrightEnoughToLoseItsColour()
        {
            var wrong = new List<string>();

            for (int i = 0; i < Hostile.GetLength(0); i++)
            {
                // The lance and the muzzle glows are shader graphs with an
                // intensity of their own; a beam's white core is the point.
                if (Hostile[i, 1] != "_EmissiveColor")
                {
                    continue;
                }

                Color glow = Read(Hostile[i, 0], Hostile[i, 1]);
                float peak = Mathf.Max(glow.r, Mathf.Max(glow.g, glow.b));

                if (peak > PeakCeiling)
                {
                    wrong.Add(Hostile[i, 0] + ": peaks at " + peak.ToString("0.##"));
                }
            }

            Assert.That(wrong, Is.Empty);
        }

        [Test]
        public void TheLightUnderARound_IsTheRoundsColour()
        {
            SavedScene scene = SavedScene.Load("Assets/Scenes/Game.unity");
            string template = scene.GameObjectNamed("Boss Bullet Light Template");
            Assert.That(template, Is.Not.Null, "the hostile rounds' light is not in the Game scene");

            float light = Hue(scene.Colour(scene.Component(template, "Light"), "m_Color"));
            float round = Hue(Read(Vfx + "BossRound.mat", "_EmissiveColor"));

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(light, round)), Is.LessThan(15f),
                "a round would throw light of a colour it is not");
        }

        [Test]
        public void EveryHostileRoundPrefab_WearsOneOfTheCheckedMaterials()
        {
            var known = new HashSet<string>();
            for (int i = 0; i < Hostile.GetLength(0); i++)
            {
                known.Add(Hostile[i, 0]);
            }

            var wrong = new List<string>();
            int checkedRounds = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (!root.CompareTag("enemy_Shoot"))
                {
                    continue;
                }

                checkedRounds++;

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null && !known.Contains(AssetDatabase.GetAssetPath(material)))
                        {
                            wrong.Add(path + " wears " + material.name + ", whose colour nothing here checks");
                        }
                    }
                }
            }

            // Ten on 5 October 2026: two enemy rounds, two stand-ins, four
            // Leviathan rounds and two discs.
            Assert.That(checkedRounds, Is.GreaterThanOrEqualTo(10));
            Assert.That(wrong, Is.Empty);
        }
    }
}
