using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// A short committed burst along the player's heading, with invincibility for
    /// its whole length.
    ///
    /// The one movement verb the game was missing. Everything else the ship can
    /// do is continuous - orbit, climb, flip - so every threat could be answered
    /// by having started moving earlier, and none of them could be answered
    /// *now*. A dash is the answer that arrives late, which is what makes an
    /// attack worth telegraphing at all.
    ///
    /// This object owns the decision and the timing; <see cref="PlayerMovement"/>
    /// owns the motion, because the camera runs that same component and has to
    /// make the identical move. The split is why the dash cannot simply add to
    /// this transform.
    ///
    /// Sized against the arena rather than by feel. At the authored orbit speed
    /// the ship covers 29.2 degrees a second, so a 0.22s burst at five times that
    /// is 32 degrees of arc - about 77 world units at the orbit radius, against a
    /// boss hull 70 units wide along the ring. That is the number that matters:
    /// one dash carries you all the way through the boss rather than into the
    /// middle of it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDash : MonoBehaviour
    {
        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("How long one burst lasts, in seconds. With the multiplier below this decides " +
                 "the distance covered - 0.22s at 5x is roughly 77 world units of arc, which is " +
                 "just wider than the boss hull.")]
        private float duration = 0.22f;

        [SerializeField]
        [Range(1f, 12f)]
        [Tooltip("Speed during the burst, as a multiple of the player's current speed. " +
                 "Multiplies whatever the Move Speed picks have already bought, so a dash stays " +
                 "worth taking late in a run.")]
        private float speedMultiplier = 5f;

        [SerializeField]
        [Range(0f, 5f)]
        [Tooltip("Seconds after a burst ends before another may start. Measured from the end of " +
                 "the dash, so this is the gap between dashes rather than the gap between starts.")]
        private float cooldown = 1f;

        [SerializeField]
        [Tooltip("Player whose flip decides which way a dash goes when no direction is held. " +
                 "Found on this object when left empty.")]
        private Player player;

        /// <summary>
        /// How much held input counts as a direction.
        ///
        /// Only has to reject noise. The stick already arrives through a 0.125
        /// deadzone, and the keyboard ramp passes this within about a thirtieth
        /// of a second of the key going down, so a player who taps a direction
        /// and immediately dashes gets the direction they asked for.
        /// </summary>
        private const float HeldThreshold = 0.1f;

        private DashCycle cycle;

        /// <summary>
        /// Whether this object is the one currently holding the shared burst on.
        ///
        /// The state lives on PlayerMovement and is static, so somebody has to
        /// remember to switch it off. Tracked rather than inferred from the cycle
        /// because the cycle keeps answering after this component is gone.
        /// </summary>
        private bool holdingBurst;

        /// <summary>True while the dash's invincibility is up.</summary>
        public bool Invincible => cycle != null && cycle.IsDashing(Time.time);

        /// <summary>
        /// How far the cooldown has recovered, 0 to 1. For a UI bar; nothing
        /// reads it yet.
        /// </summary>
        public float ReadyFraction => cycle != null ? cycle.ReadyFraction(Time.time) : 1f;

        private void Awake()
        {
            cycle = new DashCycle(duration, cooldown);

            if (player == null)
            {
                TryGetComponent(out player);
            }
        }

        /// <summary>
        /// Drops the burst if this object goes while one is running.
        ///
        /// The boost is static and shared with the camera, so a ship destroyed or
        /// disabled mid-dash would leave both flying at five times speed with
        /// nothing left alive to stop them. PlayerMovement clears it on scene
        /// unload as well; this covers the case where the run carries on.
        /// </summary>
        private void OnDisable()
        {
            ReleaseBurst();
        }

        private void Update()
        {
            float now = Time.time;

            if (holdingBurst && !cycle.IsDashing(now))
            {
                ReleaseBurst();
            }

            if (!GameInput.DashPressed || !cycle.TryBegin(now))
            {
                return;
            }

            Vector2 heading = Heading();
            holdingBurst = true;
            PlayerMovement.BeginDash(heading.x, heading.y, speedMultiplier);

            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.PlayerDash);
            }
        }

        /// <summary>
        /// Which way this burst goes, decided once at the moment it starts.
        ///
        /// Normalised, so a dash covers its authored distance whether the player
        /// was leaning on the stick or resting against it - the burst is a fixed
        /// move, not a boost proportional to how hard you were already pushing.
        /// </summary>
        private Vector2 Heading()
        {
            Vector2 held = new Vector2(GameInput.Horizontal, GameInput.Vertical);

            if (held.sqrMagnitude >= HeldThreshold * HeldThreshold)
            {
                return held.normalized;
            }

            // Nothing held, so the ship goes the way its nose is pointing. The
            // flip is the only thing that knows which way that is, and it is the
            // same sign convention PlayerMovement steers by. If it reads backwards
            // on your model, negate it here rather than reversing the flip -
            // SpaceShipPitch documents the same rule for the same reason.
            float forward = player != null && player.DirectionFlipped ? -1f : 1f;
            return new Vector2(forward, 0f);
        }

        private void ReleaseBurst()
        {
            if (!holdingBurst)
            {
                return;
            }

            holdingBurst = false;
            PlayerMovement.EndDash();
        }
    }
}
