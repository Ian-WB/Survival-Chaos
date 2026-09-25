using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The waves' heights are carried into the player's band as they spawn, so
    /// moving or resizing PlayerBounds takes every stream with it. Before 25
    /// September 2026 they were world heights: raising the floor once left four
    /// streams beneath it, and lowering it 1.7 on 21 September left every stream
    /// 1.7 higher in the band than it was placed. What is pinned here is that
    /// MainRun spawns where it always has on the band as it is, and inside the
    /// band wherever the band goes. Read off MainRun itself.
    /// </summary>
    public class WaveBandHeightTests
    {
        /// <summary>PlayerBounds in the Game scene on 25 September 2026.</summary>
        private const float Floor = 2.721233f;
        private const float Ceiling = 11.618166f;

        private static WaveDefinition MainRun()
        {
            var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>("Assets/Data/Waves/MainRun.asset");
            Assert.IsNotNull(wave, "MainRun wave not found");
            return wave;
        }

        [Test]
        public void Carry_OnTheSameBand_ChangesNothing()
        {
            Assert.AreEqual(6.016f, SpawnBand.Carry(6.016f, Floor, Ceiling, Floor, Ceiling), 1e-4f);
        }

        [Test]
        public void Carry_KeepsAHeightsPlaceInTheBand()
        {
            // A third of the way up 0-9 is a third of the way up 10-28.
            Assert.AreEqual(16f, SpawnBand.Carry(3f, 0f, 9f, 10f, 28f), 1e-4f);
            // A shift alone moves it by the shift.
            Assert.AreEqual(6f, SpawnBand.Carry(3f, 0f, 9f, 3f, 12f), 1e-4f);
        }

        [Test]
        public void Carry_FromABandWithNoHeight_MovesWithItsMiddle()
        {
            Assert.AreEqual(9f, SpawnBand.Carry(5f, 4f, 4f, 6f, 10f), 1e-4f);
        }

        [Test]
        public void MainRun_RecordsTheBandAsItIs()
        {
            WaveDefinition wave = MainRun();

            Assert.AreEqual(Floor, wave.AuthoredFloor, 0.001f);
            Assert.AreEqual(Ceiling, wave.AuthoredCeiling, 0.001f);
        }

        [Test]
        public void OnTheBandAsItIs_EveryStreamSpawnsWhereItAlwaysHas()
        {
            WaveDefinition wave = MainRun();

            foreach (SpawnStream stream in wave.Streams)
            {
                Assert.AreEqual(stream.Position.y, wave.HeightIn(stream.Position.y, Floor, Ceiling), 1e-4f,
                    stream.Label);
            }
        }

        [TestCase(0f, 1f)]
        [TestCase(-1.7f, 1f)]
        [TestCase(3f, 1f)]
        [TestCase(-6f, 1f)]
        [TestCase(0f, 0.6f)]
        [TestCase(2f, 1.5f)]
        public void EveryStream_StaysInsideTheBand_WhereverTheBandGoes(float shift, float scale)
        {
            WaveDefinition wave = MainRun();
            float floor = Floor + shift;
            float ceiling = floor + ((Ceiling - Floor) * scale);
            var report = new StringBuilder();

            Assert.AreEqual(0, wave.DescribeStreamsOutside(floor, ceiling, report), report.ToString());
        }

        [Test]
        public void AStreamKeepsItsPlace_WhenTheBandMoves()
        {
            // The top obstacle tier, 10.72, sits 0.9 under the ceiling. Written as
            // a world height, a band lowered 3 would have left it wholly above.
            WaveDefinition wave = MainRun();
            const float top = 10.719113f;
            float floor = Floor - 3f;
            float ceiling = Ceiling - 3f;

            SpawnBand.RangeOf(top, new Vector2(-1.1f, 0.8f), out float low, out float high);
            Assume.That(SpawnBand.IsWhollyOutside(low, high, floor, ceiling), Is.True);

            Assert.AreEqual(Ceiling - top, ceiling - wave.HeightIn(top, floor, ceiling), 1e-4f);
        }
    }
}
