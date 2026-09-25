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
    /// Sized against the arena rather than by feel. At the authored 5.6 units a
    /// second a 0.22s burst at five times that covers about 6.2 units of arc,
    /// against a boss hull 7.05 units wide along the ring. It was 7.7, all the way
    /// through the hull, until the 22 September 2026 slow-down took the ship from
    /// 7 to 5.6 and the burst with it. The ram is still dodged because it closes
    /// head-on as well, about four more units in the burst at the boss's 20
    /// degrees a second; if that ever reads as a wall, a speedMultiplier of 6.25
    /// puts the 7.7 back.
    ///
    /// The climb is sized separately, and against something else. It used to
    /// share that multiplier, which made a vertical dash 7.7 units too - 87% of
    /// a flight band 8.9 tall, so a dash from the floor meant for the middle
    /// landed near the ceiling, and the 11 September 2026 playtest said the dash
    /// travelled too far. Nothing needs that much. The ram cannot be climbed
    /// over at any distance, since its hull is taller than the band, and the
    /// lance is left by clearing its height by about half a unit. What the climb
    /// does answer to is the boss's three banks, which sit on the band's floor,
    /// middle and ceiling about 4.45 units apart - so one climb was sized to move
    /// you one height, 4.47 units at 7. At 5.6 it is about 3.6, most of one.
    ///
    /// Runs before everything at the default order, PlayerMovement included.
    /// The ship and the camera each run their own PlayerMovement off the one
    /// shared burst, and Unity orders nothing between scripts unless told to: had
    /// this run between the two, one would have travelled a frame of dash the
    /// other did not, and nothing ever puts the camera back on the ship's
    /// bearing, so the difference would have stayed. Found by ChatGPT's scan on
    /// 25 September 2026. It also reads the pad's A - a dash, and the button
    /// that presses Resume - before the EventSystem handles that same press, so
    /// resuming does not dash as well.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    public sealed class PlayerDash : MonoBehaviour
    {
        [SerializeField]
        [Range(0.05f, 1f)]
        [Tooltip("How long one burst lasts, in seconds. With the multiplier below this decides " +
                 "the distance covered - 0.22s at 5x a 5.6 ship is about 6.2 world units of arc, " +
                 "less than the boss hull's 7.05, so a dash gets through the ram only because the " +
                 "ram closes head-on too.")]
        private float duration = 0.22f;

        [SerializeField]
        [Range(1f, 12f)]
        [Tooltip("Speed during the burst, as a multiple of the player's current speed. " +
                 "Multiplies whatever the Move Speed picks have already bought, so a dash stays " +
                 "worth taking late in a run.")]
        private float speedMultiplier = 5f;

        [SerializeField]
        [Range(1f, 12f)]
        [Tooltip("Speed of the burst's vertical half, as a multiple of the player's current climb " +
                 "speed. Separate from the multiplier above because the ring distance is sized " +
                 "against the ram and the climb is not: 0.22s at 2.9x a 5.6 climb is about 3.6 " +
                 "units, most of the 4.45 between the boss's three heights.")]
        private float climbMultiplier = 2.9f;

        [SerializeField]
        [Range(0f, 15f)]
        [Tooltip("Seconds after a burst ends before another may start. Measured from the end of " +
                 "the dash, so this is the gap between dashes rather than the gap between starts. " +
                 "10 since 25 September 2026, when it was 1: a dash is a thing to save now, not a " +
                 "thing to spend on every ram.")]
        private float cooldown = 10f;

        [SerializeField]
        [Tooltip("Player whose flip decides which way a dash goes when no direction is held. " +
                 "Found on this object when left empty.")]
        private Player player;

        /// <summary>
        /// How much held input counts as a direction.
        ///
        /// Only has to reject noise. The stick already arrives through a 0.125
        /// deadzone, and the keyboard ramp passes this within about a sixtieth
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
        /// How far the cooldown has recovered, 0 to 1. Read by <see cref="DashBar"/>.
        /// </summary>
        public float ReadyFraction => cycle != null ? cycle.ReadyFraction(Time.time) : 1f;

        /// <summary>Seconds between dashes as things stand, upgrades included.</summary>
        public float Cooldown => cycle != null ? cycle.Cooldown : cooldown;

        /// <summary>
        /// Takes <paramref name="seconds"/> off the cooldown, for the Dash
        /// Recovery upgrade, stopping at <paramref name="floor"/>. A floor the
        /// cooldown is already under leaves it where it is rather than raising it.
        /// </summary>
        public void ShortenCooldown(float seconds, float floor)
        {
            if (cycle == null)
            {
                return;
            }

            float current = cycle.Cooldown;
            cycle.SetCooldown(Mathf.Min(current, Mathf.Max(floor, current - seconds)));
        }

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
            if (PauseMenu.GameIsPaused || RunOutcome.RunEnded || Time.timeScale <= 0f) { return; }
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
            PlayerMovement.BeginDash(heading.x, heading.y, speedMultiplier, climbMultiplier);

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
