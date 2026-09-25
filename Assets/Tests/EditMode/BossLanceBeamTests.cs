using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Which way the lance runs, against the boss's own lance rounds.
    ///
    /// The beam read its direction once, from the round the first lance used,
    /// and kept it for the fight. The boss turns round whenever the player
    /// crosses it, so from then on half its lances ran out of its tail, back
    /// across its own hull - reported from play on 25 September 2026.
    /// </summary>
    public class BossLanceBeamTests
    {
        private const string BossPath = "Assets/Prefabs/Boss/Boss.prefab";

        private GameObject centre;
        private BossLanceBeam beam;

        [TearDown]
        public void TearDown()
        {
            if (beam != null)
            {
                // The mesh is made in code and belongs to no asset.
                Object.DestroyImmediate(beam.GetComponent<MeshFilter>().sharedMesh);
                Object.DestroyImmediate(beam.gameObject);
            }

            if (centre != null)
            {
                Object.DestroyImmediate(centre);
            }
        }

        /// <summary>
        /// The pairing the fix relies on: EnemyMovement turns the boss by
        /// +rotationSpeed while it travels left, and a round flies by +speed,
        /// both through RotateAround about the vertical. So the left round has
        /// to fly the positive way for the beam to leave the prow forwards.
        /// </summary>
        [Test]
        public void TheLanceRounds_FlyTheWayTheBossTravels()
        {
            LanceRounds(out ShootScript left, out ShootScript right);

            Assert.Greater(left.Speed, 0f, "the lance's travelling-left round should fly the positive way round");
            Assert.Less(right.Speed, 0f, "the lance's travelling-right round should fly the negative way round");
        }

        [Test]
        public void EveryShot_RunsTheWayItsOwnRoundFlies()
        {
            LanceRounds(out ShootScript left, out ShootScript right);

            centre = new GameObject("Test Arena Centre");
            beam = BossLanceBeam.Create(left.gameObject, null, 0);

            // Far below the band, so nothing in the scene is ever inside it.
            var prow = new Vector3(ArenaGeometry.LaneRadius, -500f, 0f);

            beam.Fire(prow, centre.transform, null, null, left.gameObject);
            Assert.AreEqual(1f, beam.Direction, "the first lance, travelling left");

            beam.Fire(prow, centre.transform, null, null, right.gameObject);
            Assert.AreEqual(-1f, beam.Direction, "a lance after the boss turned kept the first lance's direction");

            beam.Fire(prow, centre.transform, null, null, left.gameObject);
            Assert.AreEqual(1f, beam.Direction, "and after it turned back");
        }

        private static void LanceRounds(out ShootScript left, out ShootScript right)
        {
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
            Assert.IsNotNull(boss, "No boss prefab at " + BossPath);

            var emitter = boss.GetComponentInChildren<BossEmitter>(true);
            Assert.IsNotNull(emitter, "The boss prefab has no BossEmitter");

            SerializedProperty attacks = new SerializedObject(emitter).FindProperty("attacks");

            for (int i = 0; i < attacks.arraySize; i++)
            {
                SerializedProperty attack = attacks.GetArrayElementAtIndex(i);

                if (attack.FindPropertyRelative("pattern").intValue != (int)BossFirePattern.Lance)
                {
                    continue;
                }

                left = Round(attack, "projectileWhenLeft");
                right = Round(attack, "projectileWhenRight");
                return;
            }

            Assert.Fail("The boss has no Lance attack");
            left = right = null;
        }

        private static ShootScript Round(SerializedProperty attack, string side)
        {
            var prefab = attack.FindPropertyRelative(side).objectReferenceValue as GameObject;
            Assert.IsNotNull(prefab, "The lance has no " + side);

            ShootScript round = prefab.GetComponentInChildren<ShootScript>(true);
            Assert.IsNotNull(round, side + " has no ShootScript");
            return round;
        }
    }
}
