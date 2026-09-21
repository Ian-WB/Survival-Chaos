using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The lane easing on the rounds the boss fires, read off the prefabs.
    ///
    /// The boss's muzzles sit 17.9 to 19.5 from the axis, across a hull 3 units
    /// deep, and a round keeps the distance it was born at unless laneResponse
    /// eases it onto the player's lane. At 0 no keel muzzle is within the 0.17
    /// a disc needs to touch the player, so the curtain cannot hit anyone - and
    /// it looks exactly as busy as one that can. That happened through the
    /// Inspector on 21 September 2026, shipped in a build, and nothing noticed.
    ///
    /// The rounds are found through the boss's own attack list rather than
    /// listed here, so a round the boss starts firing is checked without anyone
    /// remembering to add it.
    /// </summary>
    public class BossRoundLaneTests
    {
        private const string BossPath = "Assets/Prefabs/Boss/Boss.prefab";

        /// <summary>
        /// BuildBossRig.RoundLaneResponse. The editor assembly is not referenced
        /// from the tests, so the number is repeated here and has to move with
        /// the rig's.
        /// </summary>
        private const float RigLaneResponse = 5f;

        /// <summary>
        /// The rig tunes six: both discs and bullets 3 to 6. Finding fewer means
        /// the search has stopped seeing them, not that the boss fires fewer.
        /// </summary>
        private const int RoundsTheRigTunes = 6;

        [Test]
        public void EveryRoundTheBossFires_EasesOntoTheLane()
        {
            var wrong = new List<string>();

            foreach (ShootScript round in RoundsTheBossFires())
            {
                float response = LaneResponse(round);
                if (!Mathf.Approximately(response, RigLaneResponse))
                {
                    wrong.Add(AssetDatabase.GetAssetPath(round) + " is at " + response);
                }
            }

            Assert.IsEmpty(wrong,
                "laneResponse should be " + RigLaneResponse + ": " + string.Join(", ", wrong));
        }

        /// <summary>
        /// Without this the test above passes on an empty list, which is the
        /// same silence it exists to break.
        /// </summary>
        [Test]
        public void TheSearch_FindsEveryRoundTheRigTunes()
        {
            Assert.GreaterOrEqual(RoundsTheBossFires().Count, RoundsTheRigTunes);
        }

        private static List<ShootScript> RoundsTheBossFires()
        {
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
            Assert.IsNotNull(boss, "No boss prefab at " + BossPath);

            var emitter = boss.GetComponentInChildren<BossEmitter>(true);
            Assert.IsNotNull(emitter, "The boss prefab has no BossEmitter");

            SerializedProperty attacks = new SerializedObject(emitter).FindProperty("attacks");
            var rounds = new List<ShootScript>();

            for (int i = 0; i < attacks.arraySize; i++)
            {
                SerializedProperty attack = attacks.GetArrayElementAtIndex(i);

                foreach (string side in new[] { "projectileWhenLeft", "projectileWhenRight" })
                {
                    var prefab = attack.FindPropertyRelative(side).objectReferenceValue as GameObject;

                    // The wreckage is a plate rather than a round, so it has no
                    // ShootScript and nothing to ease.
                    if (prefab != null && prefab.TryGetComponent(out ShootScript round)
                        && !rounds.Contains(round))
                    {
                        rounds.Add(round);
                    }
                }
            }

            return rounds;
        }

        private static float LaneResponse(ShootScript round)
        {
            SerializedProperty response = new SerializedObject(round).FindProperty("laneResponse");
            Assert.IsNotNull(response, "ShootScript has no laneResponse field to read");
            return response.floatValue;
        }
    }
}
