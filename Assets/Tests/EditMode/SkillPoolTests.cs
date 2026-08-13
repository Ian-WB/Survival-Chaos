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

        // Draw and RecordPick exist because an offer is not a pick. Level-up puts
        // several pickups on the ring and the player takes one, so drawing must
        // not charge anything - otherwise a three-way offer spends three picks
        // and the two the player declined are gone.

        [Test]
        public void Draw_ReturnsDistinctSkills()
        {
            HealSkill heal = NewSkill<HealSkill>(0);
            MaxHealthSkill maxHealth = NewSkill<MaxHealthSkill>(1);
            AttackSpeedSkill attackSpeed = NewSkill<AttackSpeedSkill>(3);
            var pool = new SkillPool(new SkillDefinition[] { heal, maxHealth, attackSpeed }, _ => 0);

            List<SkillDefinition> drawn = pool.Draw(3);

            Assert.AreEqual(3, drawn.Count);
            CollectionAssert.AllItemsAreUnique(drawn);
        }

        [Test]
        public void Draw_ChargesNothingAgainstThePickLimits()
        {
            MaxHealthSkill once = NewSkill<MaxHealthSkill>(1);
            HealSkill heal = NewSkill<HealSkill>(0);
            var pool = new SkillPool(new SkillDefinition[] { once, heal }, _ => 0);

            pool.Draw(2);

            Assert.AreEqual(0, pool.PicksTaken(once));
            Assert.AreEqual(0, pool.PicksTaken(heal));
            Assert.AreEqual(2, pool.AvailableCount, "drawing removed skills from the pool");
        }

        [Test]
        public void Draw_ReturnsWhatItCan_WhenTheOfferIsWiderThanThePool()
        {
            HealSkill heal = NewSkill<HealSkill>(0);
            var pool = new SkillPool(new[] { heal }, _ => 0);

            // An unlimited skill still only appears once in one offer - three of
            // the same pickup is not a choice.
            Assert.AreEqual(1, pool.Draw(3).Count);
        }

        [Test]
        public void Draw_ReturnsEmpty_ForAnEmptyOffer()
        {
            HealSkill heal = NewSkill<HealSkill>(0);
            var pool = new SkillPool(new[] { heal }, _ => 0);

            Assert.IsEmpty(pool.Draw(0));
            Assert.IsEmpty(pool.Draw(-1));
        }

        [Test]
        public void RecordPick_ChargesTheLimit()
        {
            MaxHealthSkill once = NewSkill<MaxHealthSkill>(1);
            var pool = new SkillPool(new[] { once }, _ => 0);

            pool.RecordPick(once);

            Assert.AreEqual(1, pool.PicksTaken(once));
            Assert.AreEqual(0, pool.AvailableCount);
        }

        [Test]
        public void RecordPick_IgnoresNullAndSkillsThePoolDoesNotHold()
        {
            HealSkill held = NewSkill<HealSkill>(0);
            MaxHealthSkill foreign = NewSkill<MaxHealthSkill>(1);
            var pool = new SkillPool(new[] { held }, _ => 0);

            Assert.DoesNotThrow(() => pool.RecordPick(null));
            Assert.DoesNotThrow(() => pool.RecordPick(foreign));
            Assert.AreEqual(0, pool.PicksTaken(foreign));
        }

        [Test]
        public void DecliningAnOffer_LeavesTheOtherSkillsAvailable()
        {
            // The behaviour the whole pickup design rests on. Three one-pick
            // skills go out, the player takes one; the other two must still be
            // offerable next level.
            MaxHealthSkill health = NewSkill<MaxHealthSkill>(1);
            ShotUpgradeSkill shots = NewSkill<ShotUpgradeSkill>(1);
            AttackSpeedSkill speed = NewSkill<AttackSpeedSkill>(1);
            var pool = new SkillPool(new SkillDefinition[] { health, shots, speed }, _ => 0);

            List<SkillDefinition> offered = pool.Draw(3);
            pool.RecordPick(offered[1]);

            Assert.AreEqual(2, pool.AvailableCount, "the declined skills were consumed");
            Assert.AreEqual(0, pool.PicksTaken(offered[0]));
            Assert.AreEqual(1, pool.PicksTaken(offered[1]));
            Assert.AreEqual(0, pool.PicksTaken(offered[2]));
        }

        [Test]
        public void Next_StillChargesItsOwnPick()
        {
            // Next is now built on the same helper as Draw, so its old contract
            // is worth pinning: it draws and charges in one step.
            MaxHealthSkill once = NewSkill<MaxHealthSkill>(1);
            var pool = new SkillPool(new[] { once }, _ => 0);

            Assert.AreSame(once, pool.Next());
            Assert.AreEqual(1, pool.PicksTaken(once));
            Assert.IsNull(pool.Next());
        }
    }
}
