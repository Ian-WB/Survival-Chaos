using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The arithmetic of the paused close-up: how narrow the lens goes, and
    /// that the ship lands where the view says it will.
    /// </summary>
    public class PauseShipViewTests
    {
        [Test]
        public void TheLens_TakesInTheHeightAskedFor()
        {
            float lens = PauseShipView.LensFor(2f, 10f);
            float seen = 2f * 10f * Mathf.Tan(lens * 0.5f * Mathf.Deg2Rad);

            Assert.AreEqual(2f, seen, 0.0001f);
        }

        [Test]
        public void TheCloseView_IsFarTighterThanTheGames()
        {
            // The camera stands about 12.7 from the ship and plays at tens of degrees.
            Assert.Less(PauseShipView.LensFor(PauseShipView.FrameHeight, 12.7f), 15f);
        }

        [TestCase(0f, 0f)]
        [TestCase(-0.27f, 0.04f)]
        [TestCase(0.3f, -0.2f)]
        public void Aim_PutsTheShipWhereItSays(float x, float y)
        {
            const float lens = 11f;
            const float aspect = 16f / 9f;

            var toward = new Vector3(1.5f, -0.8f, 12f);
            var place = new Vector2(x, y);

            Quaternion rotation = PauseShipView.Aim(toward, place, lens, aspect);
            Vector3 inView = Quaternion.Inverse(rotation) * toward;

            float halfHeight = Mathf.Tan(lens * 0.5f * Mathf.Deg2Rad);

            Assert.Greater(inView.z, 0f);
            Assert.AreEqual(x, inView.x / inView.z / (2f * halfHeight * aspect), 0.0005f);
            Assert.AreEqual(y, inView.y / inView.z / (2f * halfHeight), 0.0005f);
        }

        [Test]
        public void TheShipSitsClearOfTheMiddle_WhereTheMenuIs()
        {
            Assert.Greater(Mathf.Abs(PauseShipView.Place.x), 0.15f);
            Assert.Less(Mathf.Abs(PauseShipView.Place.x), 0.4f);
        }

        [TestCase(1.35f, 0f, 1f)]
        [TestCase(1.35f, 0.5f, 1.175f)]
        [TestCase(1.35f, 1f, 1.35f)]
        [TestCase(1f, 0.5f, 1f)]
        public void TheHitBox_TakesItsShareOfTheModelsGrowth(float model, float growth, float expected)
        {
            Assert.AreEqual(expected, Player.HitBoxScale(model, growth), 0.0001f);
        }

        [Test]
        public void ThePlayersModel_IsDrawnLargerThanAuthored()
        {
            var host = new GameObject("player");

            try
            {
                var serialized = new UnityEditor.SerializedObject(host.AddComponent<Player>());
                UnityEditor.SerializedProperty scale = serialized.FindProperty("modelScale");

                Assert.IsNotNull(scale);
                Assert.That(scale.floatValue, Is.InRange(1.15f, 1.5f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
