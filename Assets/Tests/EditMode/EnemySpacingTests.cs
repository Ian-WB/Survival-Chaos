using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The arithmetic that keeps two enemies from being drawn inside each
    /// other. Positions are the second enemy's centre measured from the first's.
    /// </summary>
    public class EnemySpacingTests
    {
        [Test]
        public void EnemiesAlreadyClearAreLeftAlone()
        {
            EnemySpacing.Clear(2f, 0f, 1f, 0.5f, out float along, out float up);
            Assert.AreEqual(0f, along);
            Assert.AreEqual(0f, up);

            EnemySpacing.Clear(0f, 0.6f, 1f, 0.5f, out along, out up);
            Assert.AreEqual(0f, along);
            Assert.AreEqual(0f, up);
        }

        [Test]
        public void TouchingEdgesCountAsClear()
        {
            EnemySpacing.Clear(1f, 0f, 1f, 0.5f, out float along, out float up);
            Assert.AreEqual(0f, along);
            Assert.AreEqual(0f, up);
        }

        [Test]
        public void FlatShipsPartInHeightBecauseThatIsTheShorterWayOut()
        {
            // Side by side they would need 0.9 along the lane, or 0.4 in height.
            EnemySpacing.Clear(0.1f, 0.1f, 1f, 0.5f, out float along, out float up);
            Assert.AreEqual(0f, along);
            Assert.AreEqual(-0.2f, up, 0.0001f, "the first moves down, away from the second above it");
        }

        [Test]
        public void EnemiesNoseToTailPartAlongTheLane()
        {
            EnemySpacing.Clear(-0.9f, 0f, 1f, 0.5f, out float along, out float up);
            Assert.AreEqual(0.05f, along, 0.0001f, "the first moves on, away from the second behind it");
            Assert.AreEqual(0f, up);
        }

        [Test]
        public void EachMovesHalfSoTheTwoTogetherCloseTheWholeOverlap()
        {
            EnemySpacing.Clear(0f, 0.3f, 1f, 0.5f, out _, out float first);
            EnemySpacing.Clear(0f, -0.3f, 1f, 0.5f, out _, out float second);

            // The second enemy's own view is the mirror of the first's.
            Assert.AreEqual(-first, second, 0.0001f);
            Assert.AreEqual(0.5f - 0.3f, System.Math.Abs(first) * 2f, 0.0001f);
        }

        [Test]
        public void EnemiesOnTheSameSpotStillPart()
        {
            EnemySpacing.Clear(0f, 0f, 1f, 0.5f, out float along, out float up);
            Assert.AreEqual(0f, along);
            Assert.AreEqual(-0.25f, up, 0.0001f);
        }

        /// <summary>
        /// The Scout's box and the Fighter's sit off their pivots in opposite
        /// directions. With the roots 1.18 apart the roots say clear, by a
        /// hair, and the boxes overlap; the solver has to see the boxes.
        /// </summary>
        [Test]
        public void TheBoxIsMeasured_NotTheRoot()
        {
            GameObject scout = Enemy(new Vector3(-0.59f, 5f, 0f), 1.1669872f, -0.11919751f);
            GameObject fighter = Enemy(new Vector3(0.59f, 5f, 0f), 0.9485289f, 0.18914628f);

            try
            {
                BoxCollider scoutBox = scout.GetComponent<BoxCollider>();
                BoxCollider fighterBox = fighter.GetComponent<BoxCollider>();
                float reach = (scoutBox.size.x + fighterBox.size.x) * 0.5f + EnemySpacing.Gap;

                float roots = fighter.transform.position.x - scout.transform.position.x;
                EnemySpacing.Clear(roots, 0f, reach, 1f, out float along, out float _);
                Assert.AreEqual(0f, along, "the roots alone should read as clear, or this proves nothing");

                float boxes = EnemySpacing.BoxCentre(fighter.transform, fighterBox).x -
                              EnemySpacing.BoxCentre(scout.transform, scoutBox).x;
                Assert.AreEqual(0.8717f, boxes, 0.001f);

                EnemySpacing.Clear(boxes, 0f, reach, 1f, out along, out float _);
                Assert.Less(along, -0.1f, "the Scout should be sent back, away from the Fighter");
            }
            finally
            {
                Object.DestroyImmediate(scout);
                Object.DestroyImmediate(fighter);
            }
        }

        [Test]
        public void TheBoxCentre_FollowsTheEnemysTurnAndScale()
        {
            GameObject enemy = Enemy(new Vector3(3f, 2f, 1f), 1f, 0.2f);

            try
            {
                enemy.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                enemy.transform.localScale = Vector3.one * 0.5f;

                // Its own x points along world -z after the turn, at half size.
                Vector3 centre = EnemySpacing.BoxCentre(enemy.transform, enemy.GetComponent<BoxCollider>());
                Assert.AreEqual(3f, centre.x, 0.0001f);
                Assert.AreEqual(2f, centre.y, 0.0001f);
                Assert.AreEqual(0.9f, centre.z, 0.0001f);

                Assert.AreEqual(enemy.transform.position, EnemySpacing.BoxCentre(enemy.transform, null));
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        // Facing down the world's -x from the Fighter's side, as two enemies on
        // the lane face the axis: local x is world -x, the lane's direction.
        private static GameObject Enemy(Vector3 position, float width, float centreX)
        {
            GameObject enemy = new GameObject("Spacing Test Enemy");
            enemy.transform.position = position;
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            BoxCollider box = enemy.AddComponent<BoxCollider>();
            box.size = new Vector3(width, 0.8f, 1f);
            box.center = new Vector3(centreX, 0f, 0f);
            return enemy;
        }
    }
}
