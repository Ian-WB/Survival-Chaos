using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Two offers carrying the same skill, one collected, the other still out.
    ///
    /// Before this was fixed the second pickup kept the label it was placed
    /// with and granted the next stage whatever that stage's level gate said:
    /// Shot Upgrade from the level-4 and level-5 offers gave Triple Shot at
    /// level 5 instead of 10, under a pickup reading "Double Shot". Built from
    /// the real SkillSelect, PickupSpawner and Pickup, with the offers put out
    /// directly so which skills they carry is not left to the random draw.
    /// </summary>
    public class PickupOfferRefreshTests
    {
        private readonly List<Object> created = new List<Object>();

        private Player player;
        private SkillSelect select;
        private PickupSpawner spawner;

        private ShotUpgradeSkill shot;
        private MagnetSkill magnet;
        private DeflectorSkill deflector;

        [SetUp]
        public void SetUp()
        {
            ObjectPool.Clear();
            RunStats.Clear();

            shot = Skill<ShotUpgradeSkill>(3, 4, 10, 18);
            magnet = Skill<MagnetSkill>(2);
            deflector = Skill<DeflectorSkill>(2);

            // One prefab for every pickup, as in the game: a trigger, the
            // component, and a label to read the caption back from.
            var prefab = Track(new GameObject("Pickup"));
            prefab.AddComponent<SphereCollider>().isTrigger = true;
            Pickup pickup = prefab.AddComponent<Pickup>();
            var pickupFields = new SerializedObject(pickup);
            pickupFields.FindProperty("label").objectReferenceValue = prefab.AddComponent<PickupLabel>();
            pickupFields.ApplyModifiedPropertiesWithoutUndo();

            player = Track(new GameObject("Player")).AddComponent<Player>();
            select = Track(new GameObject("SkillSelect")).AddComponent<SkillSelect>();
            spawner = Track(new GameObject("PickupSpawner")).AddComponent<PickupSpawner>();

            var spawnerFields = new SerializedObject(spawner);
            spawnerFields.FindProperty("pickupPrefab").objectReferenceValue = prefab;
            spawnerFields.FindProperty("skillSelect").objectReferenceValue = select;
            spawnerFields.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            ObjectPool.Clear();

            GameObject root = GameObject.Find("Object Pool");
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            foreach (Object thing in created)
            {
                if (thing != null)
                {
                    Object.DestroyImmediate(thing);
                }
            }

            created.Clear();
            RunStats.Clear();
        }

        private GameObject Track(GameObject thing)
        {
            created.Add(thing);
            return thing;
        }

        private T Skill<T>(int maxPicks, params int[] levels) where T : SkillDefinition
        {
            T skill = ScriptableObject.CreateInstance<T>();
            skill.name = typeof(T).Name;
            created.Add(skill);

            var serialized = new SerializedObject(skill);
            serialized.FindProperty("displayName").stringValue = typeof(T).Name;
            serialized.FindProperty("maxPicks").intValue = maxPicks;
            SerializedProperty gates = serialized.FindProperty("availableFromLevel");
            gates.arraySize = levels.Length;

            for (int i = 0; i < levels.Length; i++)
            {
                gates.GetArrayElementAtIndex(i).intValue = levels[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return skill;
        }

        /// <summary>Wires the run's skills and the player, as the scene does.</summary>
        private void Offering(params SkillDefinition[] skills)
        {
            var serialized = new SerializedObject(select);
            SerializedProperty list = serialized.FindProperty("skills");
            list.arraySize = skills.Length;

            for (int i = 0; i < skills.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
            }

            serialized.FindProperty("pickups").objectReferenceValue = spawner;
            serialized.FindProperty("player").objectReferenceValue = player;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Puts one offer out at a level, and hands back its pickups in the order given.</summary>
        private List<Pickup> OfferAt(int level, params SkillDefinition[] skills)
        {
            player.currentLevel = level;
            var before = new HashSet<Pickup>(Live());
            spawner.OfferLevelUp(skills);

            var placed = new List<Pickup>();
            foreach (SkillDefinition skill in skills)
            {
                placed.Add(Live().Find(p => !before.Contains(p) && !placed.Contains(p) && p.Skill == skill));
            }

            return placed;
        }

        private static List<Pickup> Live()
        {
            var live = new List<Pickup>();
            GameObject root = GameObject.Find("Object Pool");

            if (root == null)
            {
                return live;
            }

            foreach (Transform child in root.transform)
            {
                if (child.gameObject.activeSelf && child.TryGetComponent(out Pickup pickup))
                {
                    live.Add(pickup);
                }
            }

            return live;
        }

        private static string Caption(Pickup pickup) => pickup.GetComponent<PickupLabel>().Caption;

        /// <summary>How many Shot Upgrades the player has actually been given.</summary>
        private int ShotStage() => new SerializedObject(player).FindProperty("shotUpgrades").intValue;

        [Test]
        public void AStageWhoseGateIsShut_IsSwappedForASkillNotOnThatOffer()
        {
            Offering(shot, magnet, deflector);
            List<Pickup> first = OfferAt(4, shot, magnet);
            List<Pickup> second = OfferAt(5, shot, magnet);

            spawner.OnCollected(first[0]);

            // The second pick of Shot Upgrade opens at 10, and the player is 5.
            Pickup stale = second[0];
            Assert.IsTrue(stale.gameObject.activeSelf, "the pickup left the ring instead of being swapped");
            Assert.AreSame(deflector, stale.Skill);
            Assert.AreEqual(deflector.GetPickupName(1), Caption(stale));
            Assert.AreSame(magnet, second[1].Skill, "the rest of the offer changed too");

            spawner.OnCollected(stale);

            Assert.AreEqual(1, ShotStage(), "Shot Upgrade was taken twice, the second time before its level");
            Assert.IsTrue(player.HasDeflector);
        }

        [Test]
        public void AStageStillOpen_IsRelabelledForTheNextPick()
        {
            Offering(shot, magnet);
            List<Pickup> first = OfferAt(11, shot, magnet);
            List<Pickup> second = OfferAt(12, shot, magnet);

            Assert.AreEqual("Double Shot", Caption(second[0]));

            spawner.OnCollected(first[0]);

            Assert.AreSame(shot, second[0].Skill);
            Assert.AreEqual("Triple Shot", Caption(second[0]),
                "the label still names the stage the player already has");
        }

        [Test]
        public void ASkillAtItsLimit_WithNothingToSwapIn_LeavesTheOfferToTheRest()
        {
            DeflectorSkill once = Skill<DeflectorSkill>(1);
            Offering(once, magnet);
            List<Pickup> first = OfferAt(3, once, magnet);
            List<Pickup> second = OfferAt(4, once, magnet);

            spawner.OnCollected(first[0]);

            Assert.IsFalse(second[0].gameObject.activeSelf, "a pick past the limit is still on the ring");
            Assert.IsTrue(second[1].gameObject.activeSelf);
            Assert.AreEqual(1, second[1].Offer.LiveCount);
        }

        [Test]
        public void AnOfferEmptiedThatWay_LeavesSalvage()
        {
            DeflectorSkill once = Skill<DeflectorSkill>(1);
            Offering(once);
            List<Pickup> first = OfferAt(3, once);
            OfferAt(4, once);

            spawner.OnCollected(first[0]);

            // Read off the ring rather than off the stale pickup: the salvage is
            // drawn from the pool, and the object that pickup just gave back is
            // the one it gets.
            List<Pickup> left = Live();
            Assert.AreEqual(1, left.Count, "the level-up's offer vanished without leaving anything");
            Assert.IsNull(left[0].Skill, "a pick past the limit is still on the ring");
            Assert.Greater(left[0].HealAmount, 0);
        }

        [Test]
        public void CollectingAStaleSkill_GrantsOneThatCanBeTaken()
        {
            // The guard behind the refresh: taken anyway, a closed skill is
            // swapped at the last moment rather than granted.
            Offering(shot, magnet);
            player.currentLevel = 5;

            select.ApplyCollected(shot);
            LogAssert.Expect(LogType.Warning, new Regex("collected past its level gate or pick limit"));
            select.ApplyCollected(shot);

            Assert.AreEqual(1, ShotStage());
            Assert.AreEqual(4f, player.MagnetReach, "the substitute was not granted");
        }
    }
}
