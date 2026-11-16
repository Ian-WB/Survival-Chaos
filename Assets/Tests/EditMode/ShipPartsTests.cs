using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The parts the upgrades bolt onto the ship, held to the upgrades.
    ///
    /// A pick with no part is silent: the ship just does not change, which
    /// is how every pick looked before 16 November 2026. So the list on the
    /// prefab is checked against the skill assets, pick for pick. Raise a
    /// skill's pick limit without building its part and this says so.
    /// </summary>
    public class ShipPartsTests
    {
        private const string PrefabPath = "Assets/Prefabs/Player/PlayerSpaceShip.prefab";

        private static readonly Dictionary<ShipUpgrade, string> Skills = new Dictionary<ShipUpgrade, string>
        {
            { ShipUpgrade.Hydra, "Assets/Data/Skills/ShotUpgrade.asset" },
            { ShipUpgrade.AttackSpeed, "Assets/Data/Skills/AttackSpeed.asset" },
            { ShipUpgrade.MaxHealth, "Assets/Data/Skills/MaxHealth.asset" },
            { ShipUpgrade.MoveSpeed, "Assets/Data/Skills/MoveSpeed.asset" },
            { ShipUpgrade.DashRecovery, "Assets/Data/Skills/DashRecovery.asset" },
            { ShipUpgrade.SlowMo, "Assets/Data/Skills/SlowMo.asset" },
            { ShipUpgrade.Deflector, "Assets/Data/Skills/Deflector.asset" },
            { ShipUpgrade.Magnet, "Assets/Data/Skills/Magnet.asset" },
        };

        private static ShipPartStage[] Stages()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath + " is missing");

            ShipParts parts = prefab.GetComponent<ShipParts>();
            Assert.IsNotNull(parts, "The player's ship has no ShipParts: run Survival Chaos > Gameplay > Build Ship Parts");
            return parts.Stages;
        }

        [Test]
        public void EveryUpgradeIsListed()
        {
            foreach (ShipUpgrade upgrade in System.Enum.GetValues(typeof(ShipUpgrade)))
            {
                Assert.IsTrue(Skills.ContainsKey(upgrade), upgrade + " has no skill asset named in this test");
            }
        }

        [Test]
        public void EveryPickOfEveryUpgrade_HasExactlyOnePart()
        {
            ShipPartStage[] stages = Stages();

            foreach (KeyValuePair<ShipUpgrade, string> entry in Skills)
            {
                SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(entry.Value);
                Assert.IsNotNull(skill, entry.Value + " is missing");
                Assert.Greater(skill.MaxPicks, 0, entry.Key + " has no pick limit, so no last part");

                for (int pick = 1; pick <= skill.MaxPicks; pick++)
                {
                    int found = 0;

                    foreach (ShipPartStage stage in stages)
                    {
                        if (stage.upgrade == entry.Key && stage.pick == pick)
                        {
                            found++;
                        }
                    }

                    Assert.AreEqual(1, found, entry.Key + " pick " + pick + " should add one part");
                }
            }
        }

        [Test]
        public void NoPartIsListedForAPickThatCannotBeTaken()
        {
            foreach (ShipPartStage stage in Stages())
            {
                SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(Skills[stage.upgrade]);
                Assert.That(stage.pick, Is.InRange(1, skill.MaxPicks), stage.upgrade + " " + stage.pick);
            }
        }

        [Test]
        public void EveryPart_HasAMeshAndAMaterialForEachOfItsSlots()
        {
            foreach (ShipPartStage stage in Stages())
            {
                string name = stage.upgrade + " " + stage.pick;

                Assert.IsNotNull(stage.mesh, name + " has no mesh");
                Assert.Greater(stage.mesh.triangles.Length, 0, name + " is empty");
                Assert.IsNotNull(stage.materials, name);
                Assert.AreEqual(stage.mesh.subMeshCount, stage.materials.Length, name + " materials");

                foreach (Material material in stage.materials)
                {
                    Assert.IsNotNull(material, name + " has an empty material slot");

                    // HitFlash only lights what has somewhere to put the light.
                    Assert.IsTrue(material.HasProperty("_EmissiveColor"), name + ": " + material.name);
                }
            }
        }

        [Test]
        public void EveryPart_CarriesALightInItsOwnColour()
        {
            foreach (ShipPartStage stage in Stages())
            {
                string name = stage.upgrade + " " + stage.pick;
                Material glow = stage.materials[stage.materials.Length - 1];

                Assert.Greater(stage.mesh.GetTriangles(stage.materials.Length - 1).Length, 0, name + " has no light");
                Assert.Greater(glow.GetColor("_EmissiveColor").maxColorComponent, 1f, name + " light is not lit");
                StringAssert.Contains(stage.upgrade.ToString(), glow.name, name);
            }
        }

        /// <summary>
        /// The collider does not grow with the parts, so the parts must not
        /// grow far: a wing a ship-length out would be a wing rounds pass
        /// through. The bare model reaches 0.26 from its origin.
        /// </summary>
        [Test]
        public void NoPartReachesFarFromTheHull()
        {
            foreach (ShipPartStage stage in Stages())
            {
                Bounds bounds = stage.mesh.bounds;
                float reach = Mathf.Max(bounds.min.magnitude, bounds.max.magnitude);
                Assert.Less(reach, 0.42f, stage.upgrade + " " + stage.pick + " reaches " + reach);
            }
        }

        [Test]
        public void NoPartStandsOnTheShipsCentreLineAhead_WhereTheRoundsLeave()
        {
            // The gun fires from the nose. A part across it would have every
            // round born inside it.
            foreach (ShipPartStage stage in Stages())
            {
                Bounds bounds = stage.mesh.bounds;
                bool ahead = bounds.min.x > 0.18f;
                Assert.IsFalse(ahead, stage.upgrade + " " + stage.pick + " is wholly ahead of the canopy");
            }
        }

        [TestCase(1, 0, false)]
        [TestCase(1, 1, true)]
        [TestCase(2, 1, false)]
        [TestCase(2, 5, true)]
        [TestCase(0, 3, false)]
        public void AStageShows_OnceItsPickIsTaken(int stagePick, int picksTaken, bool expected)
        {
            Assert.AreEqual(expected, ShipParts.Shows(stagePick, picksTaken));
        }
    }
}
