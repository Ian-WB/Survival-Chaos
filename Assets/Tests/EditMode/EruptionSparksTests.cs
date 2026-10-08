using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The volcano's embers and lava bombs (environment roadmap, item 38).
    /// Made by EruptionSparksBuilder, in the editor assembly, which this one
    /// does not see - so these read the prefab, the material and the scene
    /// it made.
    ///
    /// The one that matters is the reach: nothing the volcano throws may
    /// arrive among the ships.
    /// </summary>
    public class EruptionSparksTests
    {
        private const string PrefabPath = "Assets/Prefabs/VFX/EruptionSparks.prefab";
        private const string MaterialPath = "Assets/Art/Materials/VFX/Embers.mat";

        /// <summary>The lane with the player's offset of 6, as the Game scene has it.</summary>
        private const float Lane = 19.72f;

        private static GameObject Prefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath + " is missing: run Survival Chaos > Environment > Build Eruption Sparks");
            return prefab;
        }

        private static ParticleSystem Named(string name)
        {
            foreach (ParticleSystem system in Prefab().GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system.name == name)
                {
                    return system;
                }
            }

            Assert.Fail("the prefab has no particle system called " + name);
            return null;
        }

        [Test]
        public void ThrownStraightUp_ABombComesDownWhereItLeft()
        {
            Assert.AreEqual(0f, EruptionSparks.Reach(8f, 0f, 5.9f, 12f), 1e-4f);
        }

        [Test]
        public void ABombGoesFurther_ThrownHarder_WiderOrFromHigher()
        {
            float reach = EruptionSparks.Reach(6f, 15f, 5.9f, 10f);

            Assert.That(EruptionSparks.Reach(8f, 15f, 5.9f, 10f), Is.GreaterThan(reach));
            Assert.That(EruptionSparks.Reach(6f, 25f, 5.9f, 10f), Is.GreaterThan(reach));
            Assert.That(EruptionSparks.Reach(6f, 15f, 5.9f, 14f), Is.GreaterThan(reach));
            Assert.That(EruptionSparks.Reach(6f, 15f, 3f, 10f), Is.GreaterThan(reach), "weaker gravity carries it further");
        }

        /// <summary>
        /// A flat throw worked by hand: 45 degrees at 10, gravity 10, landing
        /// level with where it left, flies 10 * 10 / 10.
        /// </summary>
        [Test]
        public void TheReach_IsTheSchoolbookOne()
        {
            Assert.AreEqual(10f, EruptionSparks.Reach(10f, 45f, 10f, 0f), 1e-3f);
        }

        [Test]
        public void PastTheLimit_IsMeasuredFromTheAxis_NotInHeight()
        {
            Vector3 axis = new Vector3(1f, 0f, -2f);

            Assert.IsFalse(EruptionSparks.IsPast(new Vector3(1f, 500f, -2f), axis, 12f), "height is not distance from the axis");
            Assert.IsFalse(EruptionSparks.IsPast(new Vector3(12.9f, 3f, -2f), axis, 12f));
            Assert.IsTrue(EruptionSparks.IsPast(new Vector3(13.1f, 3f, -2f), axis, 12f));
            Assert.IsTrue(EruptionSparks.IsPast(new Vector3(1f, 3f, 10.1f), axis, 12f));
        }

        /// <summary>
        /// The hardest, widest throw the prefab allows, from where the Game
        /// scene puts it, falling all the way to height 0 - lower than any
        /// ground a bomb could reach - still comes down inside the limit,
        /// and the limit is seven units short of the lane.
        /// </summary>
        [Test]
        public void TheFurthestBomb_LandsWellShortOfTheLane()
        {
            ParticleSystem bombs = Named("Lava Bombs");
            ParticleSystem.MainModule main = bombs.main;
            ParticleSystem.ShapeModule shape = bombs.shape;

            SavedScene scene = SavedScene.Load("Assets/Scenes/Game.unity");
            Vector3 at = PlacedAt(scene);

            float gravity = Mathf.Abs(Physics.gravity.y) * main.gravityModifier.constantMax;
            float reach = EruptionSparks.Reach(main.startSpeed.constantMax, shape.angle, gravity, at.y);
            float furthest = new Vector2(at.x, at.z).magnitude + shape.radius + reach;

            SerializedObject sparks = new SerializedObject(Prefab().GetComponent<EruptionSparks>());
            float limit = sparks.FindProperty("keepWithin").floatValue;

            Assert.That(limit, Is.GreaterThan(0f), "with no limit a retuned throw has nothing to stop it");
            Assert.That(limit, Is.LessThanOrEqualTo(Lane - 7f), "the limit itself is too near the lane");
            Assert.That(furthest, Is.LessThan(limit),
                "the throw reaches " + furthest.ToString("0.00") + " from the axis; bombs would be put out in mid-air");
        }

        /// <summary>Where the sparks' instance sits in the saved Game scene, read off its prefab instance.</summary>
        private static Vector3 PlacedAt(SavedScene scene)
        {
            string text = File.ReadAllText("Assets/Scenes/Game.unity");
            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);

            foreach (Match instance in Regex.Matches(text, @"--- !u!1001 &\d+\r?\nPrefabInstance:.*?(?=\r?\n--- !u!|\z)", RegexOptions.Singleline))
            {
                if (!instance.Value.Contains("guid: " + guid))
                {
                    continue;
                }

                return new Vector3(Local(instance.Value, "x"), Local(instance.Value, "y"), Local(instance.Value, "z"));
            }

            Assert.Fail("the Game scene has no instance of " + PrefabPath);
            return Vector3.zero;
        }

        private static float Local(string instance, string axis)
        {
            Match match = Regex.Match(instance, @"propertyPath: m_LocalPosition\." + axis + @"\r?\n\s+value: (-?[\d.]+)");
            Assert.IsTrue(match.Success, "the instance records no position." + axis);
            return float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void ThePrefab_IsWired_AndTheBombsOnlyLeaveWhenTold()
        {
            EruptionSparks sparks = Prefab().GetComponent<EruptionSparks>();
            Assert.IsNotNull(sparks);

            SerializedObject wiring = new SerializedObject(sparks);
            Assert.AreSame(Named("Embers"), wiring.FindProperty("embers").objectReferenceValue);
            Assert.AreSame(Named("Lava Bombs"), wiring.FindProperty("bombs").objectReferenceValue);

            ParticleSystem bombs = Named("Lava Bombs");
            Assert.IsFalse(bombs.emission.enabled, "the bombs must only leave on a surge");
            Assert.IsFalse(bombs.main.playOnAwake);
            Assert.AreEqual(ParticleSystemSimulationSpace.World, bombs.main.simulationSpace);
        }

        /// <summary>
        /// A bomb ends where it meets the island. Without the collision it
        /// would fall through the rock and out of the bottom of the world,
        /// trailing sparks past the underside.
        /// </summary>
        [Test]
        public void ABomb_LandsOnTheIsland_AndLeavesItsTailAndItsMark()
        {
            ParticleSystem bombs = Named("Lava Bombs");
            ParticleSystem.CollisionModule collision = bombs.collision;

            Assert.IsTrue(collision.enabled);
            Assert.AreEqual(ParticleSystemCollisionType.World, collision.type);
            Assert.AreEqual(ParticleSystemCollisionMode.Collision3D, collision.mode);
            Assert.AreEqual(1f, collision.lifetimeLoss.constantMax, "a bomb that lands must end there");
            Assert.AreNotEqual(0, collision.collidesWith.value & LayerMask.GetMask("Default"),
                "the island is on Default; the bombs must collide with it");
            Assert.AreEqual(0, collision.collidesWith.value & LayerMask.GetMask("Player", "Enemies", "Boss", "Pickups"),
                "a bomb must not be stopped by anything that flies");

            ParticleSystem.SubEmittersModule children = bombs.subEmitters;
            Assert.IsTrue(children.enabled);

            var kinds = new List<string>();
            for (int i = 0; i < children.subEmittersCount; i++)
            {
                Assert.IsNotNull(children.GetSubEmitterSystem(i));
                kinds.Add(children.GetSubEmitterSystem(i).name + ":" + children.GetSubEmitterType(i));
            }

            Assert.That(kinds, Is.EquivalentTo(new[] { "Bomb Tail:Birth", "Bomb Splash:Death", "Bomb Glow:Death" }));
        }

        /// <summary>
        /// A particle's colour is eight bits a channel. A start colour above 1
        /// is clamped, and clamped unevenly: (3, 1, 0.2) becomes (1, 1, 0.2),
        /// which is yellow. The brightness belongs on the material.
        /// </summary>
        [Test]
        public void EverySparksColour_FitsInAParticle()
        {
            foreach (ParticleSystem system in Prefab().GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MinMaxGradient colour = system.main.startColor;

                foreach (Color c in new[] { colour.colorMin, colour.colorMax })
                {
                    Assert.That(c.maxColorComponent, Is.LessThanOrEqualTo(1f), system.name + " has a start colour past 1");
                    Assert.That(c.r, Is.GreaterThan(c.g * 1.8f), system.name + " is not orange");
                    Assert.That(c.g, Is.GreaterThan(c.b), system.name + " is not orange");
                }
            }
        }

        [Test]
        public void TheMaterial_IgnoresFog_AndStaysUnderWhite()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(material, MaterialPath);
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));

            Assert.AreEqual(0f, material.GetFloat("_EnableFogOnTransparent"),
                "the sparks fly inside the plume, which is fog; fogged, only their tails show");
            Assert.IsFalse(material.IsKeywordEnabled("_ENABLE_FOG_ON_TRANSPARENT"));

            float brightness = material.GetColor("_AlbedoColor").maxColorComponent;
            Assert.That(brightness, Is.InRange(1.5f, 3.2f), "much past 3 the tonemapper turns orange sparks white");

            foreach (ParticleSystemRenderer renderer in Prefab().GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Assert.AreSame(material, renderer.sharedMaterial, renderer.name);
            }
        }

        /// <summary>The island, the cone and the lava each carry a mesh collider, and the scene has one instance of the sparks.</summary>
        [Test]
        public void TheGameScene_HasTheSparksOnce_AndGroundToLandOn()
        {
            string scene = File.ReadAllText("Assets/Scenes/Game.unity");

            Assert.AreEqual(3, Regex.Matches(scene, @"--- !u!64 &").Count, "mesh colliders in the Game scene");

            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            Assert.AreEqual(1, Regex.Matches(scene, @"m_SourcePrefab: \{fileID: \d+, guid: " + guid).Count,
                "instances of the sparks in the Game scene");
        }

        /// <summary>
        /// The meshes the bombs land on carry their collision data ready-made.
        /// Without it the build makes the data itself and warns that a later
        /// Unity will not; and these meshes are remade whenever the island is
        /// split again, which would quietly drop the setting.
        /// </summary>
        [TestCase("Assets/Art/Models/Scenario/Split/ilha principal 1 (no lava) (base).asset")]
        [TestCase("Assets/Art/Models/Scenario/Split/ilha principal 1 (no lava) (cone).asset")]
        [TestCase("Assets/Art/Models/Scenario/Split/ilha lava.asset")]
        public void TheGround_CarriesItsCollisionReadyMade(string path)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Assert.IsNotNull(mesh, path);

            SerializedProperty baked = new SerializedObject(mesh).FindProperty("m_PreBakeTriangleCollisionMesh");
            Assert.IsNotNull(baked, "this Unity keeps the setting under another name");
            Assert.IsTrue(baked.boolValue, path + ": run Survival Chaos > Environment > Build Eruption Sparks");
        }

        [Test]
        public void SurgesBegun_CountsTheOnesTheClockHasPassed()
        {
            var surges = new List<float> { 15f, 120f, 160f };

            Assert.AreEqual(0, EruptionCurve.Begun(0f, surges));
            Assert.AreEqual(1, EruptionCurve.Begun(15f, surges));
            Assert.AreEqual(2, EruptionCurve.Begun(159.9f, surges));
            Assert.AreEqual(3, EruptionCurve.Begun(400f, surges));
            Assert.AreEqual(0, EruptionCurve.Begun(400f, null));
        }
    }
}
