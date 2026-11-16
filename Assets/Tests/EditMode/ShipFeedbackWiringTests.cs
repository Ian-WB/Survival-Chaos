using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// What the ships' animation needs from the prefabs, none of which says
    /// anything when it is missing.
    ///
    /// An enemy with no plume mesh flies with no engine, and one with no glow
    /// fires unannounced: both are how the game looked before 16 November
    /// 2026, so neither reads as broken. And the Leviathan carries the same
    /// model script as the wave ships do, on its hull and on the rig that
    /// mirrors its muzzles. Left to animate, the hull would bank and bob
    /// under hit boxes that do not, and its guns would swing through a turn
    /// with it.
    /// </summary>
    public class ShipFeedbackWiringTests
    {
        private static readonly string[] FlyingEnemies =
        {
            "Assets/Prefabs/Enemies/Enemy.prefab",
            "Assets/Prefabs/Enemies/Enemy 1.prefab",
            "Assets/Prefabs/Enemies/Enemy 2.prefab",
        };

        private static readonly string[] Gunships =
        {
            "Assets/Prefabs/Enemies/Enemy.prefab",
            "Assets/Prefabs/Enemies/Enemy 1.prefab",
        };

        private static GameObject Load(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is missing");
            return prefab;
        }

        private static SerializedProperty Field(Object target, string name)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(name);
            Assert.IsNotNull(property, target.GetType().Name + " has no field '" + name + "'");
            return property;
        }

        [Test]
        public void EveryFlyingEnemy_IsAnimated_AndHasAnEngineToBurn()
        {
            foreach (string path in FlyingEnemies)
            {
                EnemySpaceShip ship = Load(path).GetComponentInChildren<EnemySpaceShip>(true);
                Assert.IsNotNull(ship, path + " has no EnemySpaceShip");

                Assert.IsTrue(Field(ship, "animate").boolValue, path + " is not animated");
                Assert.IsNotNull(Field(ship, "plumeMesh").objectReferenceValue, path + " has no plume mesh");
                Assert.IsNotNull(Field(ship, "flareMaterial").objectReferenceValue, path + " has no flare material");
            }
        }

        [Test]
        public void EveryFlyingEnemysModel_SitsDirectlyUnderTheShipThatMovesIt()
        {
            // The model reads its speed off the object above it, in that
            // object's axes, and a hit knocks it in them too.
            foreach (string path in FlyingEnemies)
            {
                GameObject prefab = Load(path);
                EnemySpaceShip ship = prefab.GetComponentInChildren<EnemySpaceShip>(true);
                Object mover = Field(ship, "EnemyShip").objectReferenceValue;

                Assert.AreSame(prefab, mover, path + ": the model's ship is not the prefab's root");
                Assert.AreSame(prefab.transform, ship.transform.parent, path + ": the model is not a child of the root");
                Assert.IsNotNull(prefab.GetComponent<EnemyMovement>(), path + " has no EnemyMovement on its root");
            }
        }

        [Test]
        public void EveryGunship_HasAGlowToWarnWith()
        {
            foreach (string path in Gunships)
            {
                Enemy_1 guns = Load(path).GetComponent<Enemy_1>();
                Assert.IsNotNull(guns, path + " has no Enemy_1");

                Assert.IsNotNull(Field(guns, "tellMesh").objectReferenceValue, path + " has no tell mesh");
                Assert.IsNotNull(Field(guns, "tellMaterial").objectReferenceValue, path + " has no tell material");
                Assert.Greater(Field(guns, "tellSeconds").floatValue, 0f, path + " gives no warning");
                Assert.IsNotNull(Field(guns, "shootPivot").objectReferenceValue, path + " has no gun to put it on");
            }
        }

        [Test]
        public void TheTell_IsShorterThanTheGapBetweenShots()
        {
            // A glow as long as the gap would never go out, and would warn of nothing.
            foreach (string path in Gunships)
            {
                Enemy_1 guns = Load(path).GetComponent<Enemy_1>();

                Assert.Less(
                    Field(guns, "tellSeconds").floatValue,
                    Field(guns, "spawnDelay").floatValue * 0.5f,
                    path);
            }
        }

        [Test]
        public void TheLeviathan_IsNotAnimated()
        {
            EnemySpaceShip[] parts = Load("Assets/Prefabs/Boss/Boss.prefab")
                .GetComponentsInChildren<EnemySpaceShip>(true);

            Assert.IsNotEmpty(parts, "the boss no longer carries the model script; this test guards nothing");

            foreach (EnemySpaceShip part in parts)
            {
                Assert.IsFalse(Field(part, "animate").boolValue, part.name + " would bank and bob");
            }
        }

        [Test]
        public void ThePlayersShip_CarriesTheFlaresItsMuzzleFlashIsBuiltFrom()
        {
            ShipThrusters thrusters = Load("Assets/Prefabs/Player/PlayerSpaceShip.prefab")
                .GetComponentInChildren<ShipThrusters>(true);
            Assert.IsNotNull(thrusters, "the player's ship has no ShipThrusters");

            Assert.IsNotNull(Field(thrusters, "glowMesh").objectReferenceValue);
            Assert.IsNotNull(Field(thrusters, "flareMaterial").objectReferenceValue);
            Assert.Less(Field(thrusters, "muzzleSeconds").floatValue, 0.15f,
                "the flash outlasts the gap between the fastest gun's volleys");
        }
    }
}
