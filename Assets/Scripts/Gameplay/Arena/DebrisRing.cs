using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The rock drifting round the island, far outside the lane (environment
    /// roadmap, item 42). Flying round the ring used to turn the island and
    /// nothing else: the storm's wall is over a kilometre off and hardly
    /// shifts. The ring puts rock at 60 to 150 from the axis, so there is
    /// something in the middle distance that slides past at its own rate.
    ///
    /// **It can never cover a ship.** The camera is at most
    /// <see cref="CameraFraming.MaxDistance"/> outside the lane. Anything
    /// further from the axis than the camera is behind the lane along every
    /// line of sight that crosses the lane, and the nearest rock is further
    /// out than that by ten. DebrisRingTests holds it.
    ///
    /// **It is scenery only.** No colliders, no shadows, nothing baked; one
    /// material; lit by the moon like the island and by none of the ships'
    /// lights. The rocks stand above the level the far side of the lane is
    /// seen at, so they are in front of the sky and the storm and not behind
    /// the ships across the island, and they are clear of the fog, which
    /// lies low.
    ///
    /// The rock is in three bands, each turning about the axis at its own
    /// slow rate, the inner one fastest, and the shards tumble. The islets
    /// keep their tops up. All of it follows the run's clock, so a pause or
    /// a hit-stop holds it with everything else.
    ///
    /// DebrisRingBuilder, in the editor, makes the meshes, the material and
    /// the prefab, and fills in every field here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebrisRing : MonoBehaviour
    {
        /// <summary>No rock's middle is nearer the axis than this, or further than <see cref="Furthest"/>.</summary>
        public const float Nearest = 60f;
        public const float Furthest = 150f;

        [SerializeField]
        [Tooltip("The three bands' roots. Each turns about the axis.")]
        private Transform[] bands = new Transform[0];

        [SerializeField]
        [Tooltip("Degrees a second each band turns, in the order above.")]
        private float[] bandTurn = new float[0];

        [SerializeField]
        [Tooltip("The rocks that tumble: the shards. The islets are not here.")]
        private Transform[] tumblers = new Transform[0];

        [SerializeField]
        [Tooltip("Degrees a second each of those turns about its own axes.")]
        private Vector3[] tumble = new Vector3[0];

        private void Update()
        {
            float step = Time.deltaTime;

            if (step <= 0f)
            {
                return;
            }

            int count = Mathf.Min(bands.Length, bandTurn.Length);

            for (int i = 0; i < count; i++)
            {
                bands[i].Rotate(0f, bandTurn[i] * step, 0f, Space.Self);
            }

            count = Mathf.Min(tumblers.Length, tumble.Length);

            for (int i = 0; i < count; i++)
            {
                tumblers[i].Rotate(tumble[i] * step, Space.Self);
            }
        }
    }
}
