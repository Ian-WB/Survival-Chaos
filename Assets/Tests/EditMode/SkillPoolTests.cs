using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Covers the selection rules, including the two defects in the original
    /// SkillSelect: a skill that was never reachable, and a pool that emptied
    /// permanently so every later level-up silently became a heal.
    /// </summary>
    public class SkillPoolTests
    {
        private readonly List<SkillDefinition> created = new List<SkillDefinition>();

        [TearDown]
        public void TearDown()
        {
            foreach (SkillDefinition definition in created)
            {
                Object.DestroyImmediate(definition);
            }

            created.Clear();
        }

        private T NewSkill<T>(int maxPicks) where T : SkillDefinition
        {
            T definition = ScriptableObject.CreateInstance<T>();
            SetMaxPicks(definition, maxPicks);
            created.Add(definition);
            return definition;
        }

        /// <summary>maxPicks is a private serialized field, so drive it the way the Inspector would.</summary>
        private static void SetMaxPicks(SkillDefinition definition, int maxPicks)
        {
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("maxPicks").intValue = maxPicks;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void Next_ReturnsNull_WhenPoolHasNoSkills()
        {
            var pool = new SkillPool(new List<SkillDefinition>(), _ => 0);

            Assert.IsNull(pool.Next());
        }

        [Test]
        public void Next_RecordsThePick()
        {
            HealSkill heal = NewSkill<HealSkill>(2);
            var pool = new SkillPool(new[] { heal }, _ => 0);

            pool.Next();

            Assert.AreEqual(1, pool.PicksTaken(heal));
        }

        [Test]
        public void Skill_LeavesThePool_OnceItsPickLimitIsReached()
        {
            MaxHealthSkill once = NewSkill<MaxHealthSkill>(1);
            var pool = new SkillPool(new[] { once }, _ => 0);

            pool.Next();

            Assert.AreEqual(0, pool.AvailableCount);
            Assert.IsNull(pool.Next());
        }

        [Test]
        public void EverySkillStaysReachable_UntilItsOwnLimit()
        {
            // The original code never added the attack-speed skill to its list,
            // so it could not be drawn at all.
            AttackSpeedSkill attackSpeed = NewSkill<AttackSpeedSkill>(3);
            MaxHealthSkill maxHealth = NewSkill<MaxHealthSkill>(1);
            var pool = new SkillPool(new SkillDefinition[] { attackSpeed, maxHealth }, count => count - 1);

            // Highest index is maxHealth until it is exhausted, then attackSpeed.
            pool.Next();
            pool.Next();
            pool.Next();

            Assert.AreEqual(1, pool.PicksTaken(maxHealth));
            Assert.AreEqual(2, pool.PicksTaken(attackSpeed));
        }

        [Test]
        public void UnlimitedSkill_KeepsThePoolFromRunningDry()
        {
            HealSkill unlimited = NewSkill<HealSkill>(0);
            MaxHealthSkill once = NewSkill<MaxHealthSkill>(1);
            var pool = new SkillPool(new SkillDefinition[] { once, unlimited }, _ => 0);

            for (int i = 0; i < 20; i++)
            {
                Assert.IsNotNull(pool.Next(), $"pool ran dry on pick {i + 1}");
            }

            Assert.AreEqual(1, pool.AvailableCount);
        }

        [Test]
        public void Constructor_IgnoresNullsAndDuplicates()
        {
            HealSkill heal = NewSkill<HealSkill>(0);
            var pool = new SkillPool(new SkillDefinition[] { heal, null, heal });

            Assert.AreEqual(1, pool.AvailableCount);
        }

        [Test]
        public void Next_ClampsAnOutOfRangeSelection()
        {
            HealSkill heal = NewSkill<HealSkill>(0);
            var pool = new SkillPool(new[] { heal }, _ => 99);

            Assert.AreSame(heal, pool.Next());
        }

        [Test]
        public void ShotUpgrade_NamesEachStageInPickOrder()
        {
            ShotUpgradeSkill shots = NewSkill<ShotUpgradeSkill>(4);

            Assert.AreEqual("Double Shot!", shots.GetDisplayName(1));
            Assert.AreEqual("Triple Shot!", shots.GetDisplayName(2));
            Assert.AreEqual("SexTUPLO Shot!", shots.GetDisplayName(3));
            Assert.AreEqual("Back Shot!", shots.GetDisplayName(4));
        }

        [Test]
        public void ShotUpgrade_ClampsStageNameBeyondTheLastStage()
        {
            ShotUpgradeSkill shots = NewSkill<ShotUpgradeSkill>(4);

            Assert.AreEqual("Back Shot!", shots.GetDisplayName(99));
        }
    }
}
