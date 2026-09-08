using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Spins the visible half of a disc round about its own axis.
    ///
    /// It has to be a separate component on a child rather than something the
    /// projectile does to itself, because ShootScript owns the root's rotation
    /// outright: its Update ends in a LookAt every frame, so anything written to
    /// the root's rotation is gone before it is drawn. The mesh already lives on
    /// a child on every boss round, so this sits where the mesh does and turns in
    /// local space, underneath a parent that is still free to be aimed.
    ///
    /// The axis is the child's own Y, which the prefab points along the parent's
    /// Z - and the parent's Z is what LookAt aims at the middle of the arena. So
    /// the disc's face contains both the direction of travel and the vertical,
    /// and it rolls along its path like a blade rather than flying flat like a
    /// frisbee. That is the orientation that reads as a circle from a camera
    /// looking across the ring, which is where the player is; a frisbee would be
    /// edge-on and read as a line.
    ///
    /// Uses gameplay time so pause, slow motion and endings also affect the spin.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DiscSpin : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Degrees per second about the disc's own axis. Fast enough to read as spin " +
                 "rather than as drift, slow enough that it does not strobe against the frame " +
                 "rate - a disc turning near a whole revolution per frame reads as standing " +
                 "still, or as turning backwards.")]
        private float degreesPerSecond = 540f;

        private Transform body;

        private void Awake()
        {
            body = transform;
        }

        /// <summary>
        /// Starts each life from the same angle.
        ///
        /// Rounds come out of the pool, so without this a reused disc would begin
        /// wherever the last one was destroyed - which is invisible on a single
        /// disc and obvious on a curtain of twelve, where they would fan out
        /// instead of leaving as a rank.
        /// </summary>
        private void OnEnable()
        {
            if (body == null)
            {
                body = transform;
            }

            body.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void Update()
        {
            body.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }
    }
}
