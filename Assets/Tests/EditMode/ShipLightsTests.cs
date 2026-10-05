using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The lights that fall on ships and on nothing else, and the layer that
    /// keeps them there.
    ///
    /// Until 5 October 2026 nothing lit the lane. The moon is 1 lux, the sky
    /// adds a fifth of that, and the lava lights are spent before they get out
    /// to where the ships fly, so every enemy drew as a black shape whatever
    /// colour its materials were. Three lights changed that: a key and a rim
    /// that ride on the camera, and a warm fill at the middle of the island.
    /// They reach ships only, through a rendering layer called Ships, so the
    /// island, its bake and the fog look as they did.
    ///
    /// The player's ship is on a layer of its own, Player Ship, with a key of
    /// its own at half the strength. Its paint is the lightest of any ship's
    /// and it flies nearest the camera, so under the enemies' key it came out
    /// a flat pastel that looked lit from inside. The rim and the fill reach
    /// both layers.
    ///
    /// Both ways this breaks are silent. A ship left off its layer goes back to
    /// drawing black and nothing logs it. A light let off the layers starts
    /// lighting the island, which a rebake would then be judged against. The
    /// scene is read as saved, the way the other tests read it.
    /// </summary>
    public class ShipLightsTests
    {
        private const string Layer = "Ships";
        private const string PlayerLayer = "Player Ship";

        private const string EnemyKey = "Ship Key Light";
        private const string PlayerKey = "Player Key Light";

        private static readonly string[] Lights = { EnemyKey, PlayerKey, "Ship Rim Light", "Ship Fill Light" };

        private static uint Bit(string layer)
        {
            uint bit = UnityEngine.RenderingLayerMask.GetMask(layer);
            Assert.That(bit, Is.Not.Zero, "rendering layer '" + layer + "' is not defined");
            return bit;
        }

        private static bool IsPlayer(GameObject root)
        {
            return root.GetComponent<ShipThrusters>() != null;
        }

        private static bool IsShip(GameObject root)
        {
            return root.CompareTag("Enemy") || root.CompareTag("Boss") || IsPlayer(root);
        }

        [Test]
        public void EveryShipRenderer_IsOnItsLayer_AndStillOnDefault()
        {
            uint enemies = Bit(Layer);
            uint player = Bit(PlayerLayer);
            var wrong = new List<string>();
            int checkedRenderers = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (!IsShip(root))
                {
                    continue;
                }

                uint own = IsPlayer(root) ? player : enemies;
                uint other = IsPlayer(root) ? enemies : player;

                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    checkedRenderers++;

                    if ((renderer.renderingLayerMask & own) == 0)
                    {
                        wrong.Add(path + " / " + renderer.name + ": not on its layer, so nothing lights it");
                    }

                    if ((renderer.renderingLayerMask & other) != 0)
                    {
                        wrong.Add(path + " / " + renderer.name + ": on both layers, so both keys light it");
                    }

                    if ((renderer.renderingLayerMask & 1u) == 0)
                    {
                        wrong.Add(path + " / " + renderer.name + ": off Default, so the moon and the lava miss it");
                    }
                }
            }

            // Fewer than this and the search has stopped finding the ships. 18
            // on 5 October 2026: four enemies, the Leviathan and its wreckage,
            // and the player's ship.
            Assert.That(checkedRenderers, Is.GreaterThanOrEqualTo(18));
            Assert.That(wrong, Is.Empty);
        }

        [Test]
        public void TheShipLights_ReachShipsOnly_CastNothing_AndStayOutOfTheFog()
        {
            uint enemies = Bit(Layer);
            uint player = Bit(PlayerLayer);
            SavedScene scene = SavedScene.Load("Assets/Scenes/Game.unity");

            foreach (string name in Lights)
            {
                string gameObject = scene.GameObjectNamed(name);
                Assert.That(gameObject, Is.Not.Null, name + " is not in the Game scene");

                string light = scene.Component(gameObject, "Light");
                Assert.That(light, Is.Not.Null, name + " has no Light");

                // Each key has one layer. The rim and the fill have both.
                uint reach = name == EnemyKey ? enemies : name == PlayerKey ? player : enemies | player;

                Assert.AreEqual((float)reach, scene.Float(light, "m_RenderingLayerMask"),
                    name + " must reach its ships and nothing else, or it lights the island");
                Assert.AreEqual(0f, scene.Float(light, "m_Shadows", "m_Type"), name + " casts shadows");

                // 4 is Realtime. A Mixed or Baked light here would be written
                // into the island's lightmap by the next bake.
                Assert.AreEqual(4f, scene.Float(light, "m_Lightmapping"), name + " is not Realtime");

                string data = scene.ScriptWith(gameObject, "useVolumetric");
                Assert.That(data, Is.Not.Null, name + " has no HDRP light data");
                Assert.AreEqual(0f, scene.Float(data, "useVolumetric"), name + " lights the fog");
            }
        }

        [Test]
        public void ThePlayersKey_IsDimmerThanTheEnemies()
        {
            SavedScene scene = SavedScene.Load("Assets/Scenes/Game.unity");
            float enemies = scene.Float(scene.Component(scene.GameObjectNamed(EnemyKey), "Light"), "m_Intensity");
            float player = scene.Float(scene.Component(scene.GameObjectNamed(PlayerKey), "Light"), "m_Intensity");

            // Both are directional and in lux, so the numbers compare. At the
            // same strength the player's ship is the brightest thing in the
            // lane, which is what the second key is there to stop.
            Assert.That(player, Is.GreaterThan(0f));
            Assert.That(player, Is.LessThan(enemies * 0.75f));
        }

        [Test]
        public void TheKeysAndTheRim_RideOnTheCamera()
        {
            SavedScene scene = SavedScene.Load("Assets/Scenes/Game.unity");
            string camera = scene.Component(scene.GameObjectNamed("Main Camera"), "Transform");

            foreach (string name in new[] { EnemyKey, PlayerKey, "Ship Rim Light" })
            {
                string transform = scene.Component(scene.GameObjectNamed(name), "Transform");
                Assert.AreEqual(camera, scene.Reference(transform, "m_Father"),
                    name + " must be a child of the camera, so a ship is lit the same way all round the ring");
            }
        }

        [Test]
        public void TheFill_ReachesTheLane()
        {
            SavedScene scene = SavedScene.Load("Assets/Scenes/Game.unity");
            string gameObject = scene.GameObjectNamed("Ship Fill Light");
            string light = scene.Component(gameObject, "Light");
            Vector3 position = scene.Vector(scene.Component(gameObject, "Transform"), "m_LocalPosition");

            // It stands on the island's axis, so its distance to any ship is
            // the lane's radius and whatever height separates them.
            Assert.AreEqual(0f, new Vector2(position.x, position.z).magnitude, 0.5f, "the fill is off the axis");
            Assert.That(scene.Float(light, "m_Range"), Is.GreaterThan(ArenaGeometry.LaneRadius * 1.5f),
                "the fill's range ends too close to the lane, where HDRP has already faded it out");
        }

        [Test]
        public void TheArenasExposure_IsFixed()
        {
            // Automatic exposure moved a quarter of a stop round the ring on 5
            // October 2026, darkest where the lava fills the frame, and would
            // have moved further with every light added. The emissive ladder
            // puts every glow on one rung, and a rung only means one brightness
            // while the exposure under it holds still. The value is a dial; the
            // mode is the decision.
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                "Assets/Scenes/Game/Scene Volume Profile.asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.TryGet(out Exposure exposure), Is.True, "the scene profile has no Exposure");

            Assert.That(exposure.mode.overrideState, Is.True);
            Assert.AreEqual(ExposureMode.Fixed, exposure.mode.value);
            Assert.That(exposure.fixedExposure.overrideState, Is.True);
        }
    }
}
