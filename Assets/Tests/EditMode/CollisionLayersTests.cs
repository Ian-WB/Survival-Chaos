using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Which gameplay layers collide, and that every collider in the prefabs sits
    /// on the layer its role puts it on.
    ///
    /// Until 25 September 2026 everything was on Default and every layer met every
    /// other, so the physics system paired rounds with rounds, pickups and the
    /// boss's own hull, and each receiver threw the pair away with a tag check.
    /// Only six pairs are handled by anything, and only those collide now.
    ///
    /// The failure these guard against is silent in the same way the old
    /// arrangement was wasteful: a target moved onto a layer the rounds do not
    /// meet simply stops being hit, and nothing logs it. The prefabs are read the
    /// way BossRoundLaneTests reads them. The Player sits in the Game scene, which
    /// a test cannot open without closing whatever is open, so its layer is left
    /// to play.
    /// </summary>
    public class CollisionLayersTests
    {
        private static readonly string[] Gameplay =
        {
            "Player", "PlayerRounds", "Enemies", "EnemyRounds", "Boss", "Pickups"
        };

        /// <summary>The pairs something handles, and so the only ones that should meet.</summary>
        private static readonly string[,] Handled =
        {
            { "Player", "EnemyRounds" },   // Player.OnTriggerEnter, enemy_Shoot
            { "Player", "Enemies" },       // a ram
            { "Player", "Boss" },          // the hull, and wreckage plates
            { "Player", "Pickups" },       // Pickup.OnTriggerEnter
            { "PlayerRounds", "Enemies" }, // Enemy and Enemy_1
            { "PlayerRounds", "Boss" },    // hull, emplacements, wreckage
        };

        private static int Layer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "layer '" + name + "' is not defined");
            return layer;
        }

        private static bool IsHandled(string a, string b)
        {
            for (int i = 0; i < Handled.GetLength(0); i++)
            {
                if ((Handled[i, 0] == a && Handled[i, 1] == b) || (Handled[i, 0] == b && Handled[i, 1] == a))
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void OnlyThePairsSomethingHandles_Collide()
        {
            var wrong = new List<string>();

            foreach (string a in Gameplay)
            {
                foreach (string b in Gameplay)
                {
                    bool collide = !Physics.GetIgnoreLayerCollision(Layer(a), Layer(b));
                    if (collide != IsHandled(a, b))
                    {
                        wrong.Add(a + " x " + b + (collide ? " collide" : " do not collide"));
                    }
                }
            }

            Assert.That(wrong, Is.Empty);
        }

        [Test]
        public void ARoundsSweep_AsksForWhatItsLayerMeets_AndNothingElse()
        {
            int playerRounds = ShootScript.CollisionMask(Layer("PlayerRounds"));
            int enemyRounds = ShootScript.CollisionMask(Layer("EnemyRounds"));

            foreach (string name in Gameplay)
            {
                int bit = 1 << Layer(name);
                Assert.AreEqual(IsHandled("PlayerRounds", name), (playerRounds & bit) != 0, "PlayerRounds and " + name);
                Assert.AreEqual(IsHandled("EnemyRounds", name), (enemyRounds & bit) != 0, "EnemyRounds and " + name);
            }
        }

        [Test]
        public void EveryPrefabCollider_SitsOnTheLayerItsRoleNeeds()
        {
            var wrong = new List<string>();
            int checkedColliders = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                {
                    checkedColliders++;
                    string expected = Expected(collider.gameObject);

                    if (expected == null)
                    {
                        wrong.Add(path + " / " + collider.name + ": no role to put it on a layer");
                    }
                    else if (collider.gameObject.layer != Layer(expected))
                    {
                        wrong.Add(path + " / " + collider.name + ": on " + LayerMask.LayerToName(collider.gameObject.layer) +
                                  ", should be " + expected);
                    }
                }
            }

            // Fewer than this and the search has stopped finding the prefabs, and
            // the list above would pass on nothing.
            Assert.That(checkedColliders, Is.GreaterThanOrEqualTo(24));
            Assert.That(wrong, Is.Empty);
        }

        private static string Expected(GameObject thing)
        {
            if (thing.CompareTag("Shoot")) return "PlayerRounds";
            if (thing.CompareTag("enemy_Shoot")) return "EnemyRounds";
            if (thing.CompareTag("Enemy")) return "Enemies";
            if (thing.CompareTag("Boss") || thing.GetComponentInParent<BossWeakPoint>(true) != null ||
                thing.GetComponentInParent<BossEmitter>(true) != null) return "Boss";
            if (thing.GetComponentInParent<Pickup>(true) != null) return "Pickups";
            return null;
        }
    }
}
