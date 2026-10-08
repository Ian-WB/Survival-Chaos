using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The ring of rock that drifts round the island (environment roadmap,
    /// item 42). Made by DebrisRingBuilder, in the editor assembly, which
    /// this one does not see - so these read the prefab it made and the
    /// scene it is placed in.
    ///
    /// The one that matters is the first: no rock may be near enough to
    /// come between any camera and a ship.
    /// </summary>
    public class DebrisRingTests
    {
        private const string PrefabPath = "Assets/Prefabs/Scenario/DebrisRing.prefab";

        /// <summary>The lane with the player's offset of 6, as the Game scene has it.</summary>
        private const float Lane = 19.72f;

        /// <summary>The camera's height at the middle of the band, which the builder draws heights from.</summary>
        private const float EyeHeight = 8f;

        private static GameObject Prefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath + " is missing: run Survival Chaos > Environment > Build Debris Ring");
            return prefab;
        }

        private static MeshRenderer[] Rocks()
        {
            MeshRenderer[] rocks = Prefab().GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(rocks.Length, Is.GreaterThan(50), "the ring is nearly empty");
            return rocks;
        }

        private static float FromAxis(Transform rock)
        {
            return new Vector2(rock.position.x, rock.position.z).magnitude;
        }

        /// <summary>
        /// The camera is never further from the axis than the lane plus the
        /// debug menu's longest distance. A rock further out than that is
        /// behind the lane along every line of sight that crosses the lane.
        /// A rock's mesh is one across at its widest, so half its scale is
        /// as far as any part of it reaches from its middle.
        /// </summary>
        [Test]
        public void NoRock_IsNearEnoughToCoverAShip()
        {
            float furthestCamera = Lane + CameraFraming.MaxDistance;

            foreach (MeshRenderer rock in Rocks())
            {
                float fromAxis = FromAxis(rock.transform);
                float reach = rock.transform.lossyScale.x * 0.5f;

                Assert.That(fromAxis, Is.InRange(DebrisRing.Nearest, DebrisRing.Furthest), rock.name);
                Assert.That(fromAxis - reach, Is.GreaterThan(furthestCamera + 2f),
                    rock.name + " reaches inside where the furthest camera can stand");
            }
        }

        [Test]
        public void EveryMesh_IsOneAcrossAtItsWidest()
        {
            HashSet<Mesh> meshes = new HashSet<Mesh>();

            foreach (MeshRenderer rock in Rocks())
            {
                meshes.Add(rock.GetComponent<MeshFilter>().sharedMesh);
            }

            Assert.That(meshes.Count, Is.GreaterThanOrEqualTo(6), "the ring is the same rock over and over");

            foreach (Mesh mesh in meshes)
            {
                Assert.IsNotNull(mesh, "a rock has lost its mesh");
                float widest = 0f;

                foreach (Vector3 vertex in mesh.vertices)
                {
                    widest = Mathf.Max(widest, vertex.magnitude);
                }

                Assert.AreEqual(0.5f, widest, 0.001f, mesh.name);
            }
        }

        /// <summary>
        /// Nothing in the fight may touch the ring and the ring may touch
        /// nothing: no colliders, no shadows, nothing baked, and on the
        /// moon's light layer only, so the ships' own lights pass it by.
        /// </summary>
        [Test]
        public void TheRing_IsSceneryOnly()
        {
            Assert.IsEmpty(Prefab().GetComponentsInChildren<Collider>(true), "a rock has a collider");

            foreach (MeshRenderer rock in Rocks())
            {
                Assert.AreEqual(ShadowCastingMode.Off, rock.shadowCastingMode, rock.name);
                Assert.AreEqual(1u, rock.renderingLayerMask, rock.name + " is lit by more than the moon");
                Assert.IsFalse(rock.gameObject.isStatic, rock.name + " is static, and the ring turns");
                Assert.AreEqual(0, rock.gameObject.layer, rock.name);
            }
        }

        [Test]
        public void TheRing_IsAFewThousandTriangles_OnOneMaterial()
        {
            int triangles = 0;
            Material material = null;

            foreach (MeshRenderer rock in Rocks())
            {
                triangles += rock.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
                material = material != null ? material : rock.sharedMaterial;
                Assert.AreSame(material, rock.sharedMaterial, rock.name);
            }

            Assert.That(triangles, Is.LessThan(5000));
            Assert.IsNotNull(material);
            // The SRP Batcher draws the ring either way; with instancing on, the build compiled
            // HDRP/Lit a second time for this one material, which was ten megabytes.
            Assert.IsFalse(material.enableInstancing, "the ring's material instances, which costs the build a second set of Lit shaders");
        }

        /// <summary>
        /// From across the island the far side of the lane is seen within
        /// six degrees of level. The rocks stand over that, in front of the
        /// storm's wall and the sky, and inside the frame. It also keeps
        /// them out of the fog, which thins by nearly two thirds every seven
        /// units up and would swallow a rock at the ships' height.
        /// </summary>
        [Test]
        public void EveryRock_StandsOverTheFarLane_AndClearOfTheFog()
        {
            float eyeFromAxis = Lane + CameraFraming.Default.Distance;

            foreach (MeshRenderer rock in Rocks())
            {
                Vector3 at = rock.transform.position;
                float seen = Mathf.Atan2(at.y - EyeHeight, FromAxis(rock.transform) + eyeFromAxis) * Mathf.Rad2Deg;

                Assert.That(seen, Is.InRange(7.5f, 30.5f), rock.name + " is seen " + seen.ToString("0.0") + " degrees over level");
                Assert.That(at.y - rock.transform.lossyScale.x * 0.5f, Is.GreaterThan(15f), rock.name + " hangs into the fog");
            }
        }

        [Test]
        public void TheBands_TurnSlowly_TheInnerOneFastest()
        {
            Assert.IsTrue(Prefab().TryGetComponent(out DebrisRing ring), "the prefab's root has no DebrisRing");

            SerializedObject saved = new SerializedObject(ring);
            SerializedProperty bands = saved.FindProperty("bands");
            SerializedProperty turns = saved.FindProperty("bandTurn");
            SerializedProperty tumblers = saved.FindProperty("tumblers");
            SerializedProperty tumble = saved.FindProperty("tumble");

            Assert.AreEqual(3, bands.arraySize);
            Assert.AreEqual(bands.arraySize, turns.arraySize);
            Assert.AreEqual(tumblers.arraySize, tumble.arraySize);
            Assert.That(tumblers.arraySize, Is.GreaterThan(0), "no rock tumbles");

            float last = float.MaxValue;

            for (int i = 0; i < turns.arraySize; i++)
            {
                Assert.IsNotNull(bands.GetArrayElementAtIndex(i).objectReferenceValue, "band " + i + " is not wired");
                float turn = turns.GetArrayElementAtIndex(i).floatValue;
                Assert.That(turn, Is.InRange(0.1f, 3f), "degrees a second; faster than 3 and the ring reads as a thing spinning");
                Assert.That(turn, Is.LessThan(last), "a band further out turns faster than one further in");
                last = turn;
            }

            for (int i = 0; i < tumblers.arraySize; i++)
            {
                Assert.IsNotNull(tumblers.GetArrayElementAtIndex(i).objectReferenceValue, "tumbler " + i + " is not wired");
                Assert.That(tumble.GetArrayElementAtIndex(i).vector3Value.magnitude, Is.InRange(0.1f, 8f));
            }
        }

        /// <summary>The ring goes round the world's axis, which is the island's: an instance anywhere else is a ring round nothing.</summary>
        [Test]
        public void TheGameScene_HasTheRing_OnTheAxis()
        {
            string text = File.ReadAllText("Assets/Scenes/Game.unity");
            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            int found = 0;

            foreach (Match instance in Regex.Matches(text, @"--- !u!1001 &\d+\r?\nPrefabInstance:.*?(?=\r?\n--- !u!|\z)", RegexOptions.Singleline))
            {
                if (!instance.Value.Contains("guid: " + guid))
                {
                    continue;
                }

                found++;

                foreach (string axis in new[] { "x", "y", "z" })
                {
                    Match match = Regex.Match(instance.Value, @"propertyPath: m_LocalPosition\." + axis + @"\r?\n\s+value: (-?[\d.]+)");
                    Assert.IsTrue(match.Success, "the instance records no position." + axis);
                    Assert.AreEqual(0f, float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-4f,
                        "the ring is off the axis in " + axis);
                }
            }

            Assert.AreEqual(1, found, "the Game scene should have one instance of " + PrefabPath);
        }
    }
}
