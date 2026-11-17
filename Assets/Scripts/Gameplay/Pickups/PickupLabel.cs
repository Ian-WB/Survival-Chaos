using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Marks where a pickup's caption goes and what it says. The drawing is
    /// somewhere else entirely.
    ///
    /// Colour used to be the only thing separating one pickup from another, on
    /// the reasoning recorded in SkillDefinition: there is no room for a label on
    /// an object the size of a bullet, and no time to read one while dodging.
    /// That holds for salvage, which is just something to go and get, and goes
    /// out uncaptioned. It does not hold for a level-up offer - three go out,
    /// taking one forfeits the other two, and a choice the player cannot read is
    /// a choice between three colours.
    ///
    /// This was world-space text to begin with, a TextMeshPro on this very
    /// object. It read correctly and looked soft, because TextMeshPro's shader
    /// writes no motion vectors and temporal antialiasing therefore reprojected
    /// it against the background's motion - see PickupLabelBoard, which now does
    /// the drawing on the screen-space canvas where no post-processing can reach
    /// it. What is left here is the anchor and the payload.
    ///
    /// Kept out of <see cref="Pickup"/> because Pickup deliberately does not know
    /// what it grants. This is handed a finished string, the same way Pickup is
    /// handed a finished colour, and never asks the payload what it says.
    /// </summary>
    public sealed class PickupLabel : MonoBehaviour
    {
        /// <summary>What the caption reads. Empty when there is nothing to say.</summary>
        public string Caption { get; private set; }

        /// <summary>The payload's colour, which the caption is drawn and glowed in.</summary>
        public Color Tint { get; private set; }

        /// <summary>Whether the board should be drawing this at all.</summary>
        public bool Live { get; private set; }

        /// <summary>
        /// The pick an upgrade drawn as its ship part is, or 0 for a pickup
        /// drawn the old way. A part carries no caption on the ring and its
        /// edge arrow carries dots for this in place of the name; the name
        /// stays here for the pause screen's offers card.
        /// </summary>
        public int Tier { get; set; }

        /// <summary>
        /// What taking this offer does, for the tag beside the offer the ship
        /// is closest to ("+10 hull"). Empty when none is authored.
        /// </summary>
        public string Effect { get; set; }

        /// <summary>The upgrade an offer drawn as its part adds; null for anything else.</summary>
        public ShipUpgrade? Upgrade { get; set; }

        private Pickup pickup;
        private bool pickupLooked;

        /// <summary>
        /// How much of its pickup's time is left, 1 to 0; 1 for a label with
        /// no pickup above it.
        /// </summary>
        public float LifeLeft
        {
            get
            {
                if (!pickupLooked)
                {
                    pickupLooked = true;
                    pickup = GetComponentInParent<Pickup>();
                }

                return pickup != null ? pickup.LifeLeft : 1f;
            }
        }

        /// <summary>
        /// Sets what the caption says and starts it being drawn. An empty string
        /// stops it, which is what a pickup with nothing to announce wants.
        /// </summary>
        public void Show(string caption, Color color)
        {
            Caption = caption;
            Tint = color;
            Live = !string.IsNullOrWhiteSpace(caption);

            // Explicit null comparison rather than ?., because Unity overloads ==
            // on Object to report a destroyed object as null and the
            // null-conditional operator does not go through that overload. On a
            // scene reference that is the difference between a skipped call and a
            // call into a corpse.
            if (PickupLabelBoard.Instance == null)
            {
                return;
            }

            if (Live)
            {
                PickupLabelBoard.Instance.Attach(this);
            }
            else
            {
                PickupLabelBoard.Instance.Detach(this);
            }
        }

        /// <summary>
        /// Stops the caption when the pickup goes away.
        ///
        /// OnDisable rather than OnDestroy because pickups are pooled: they are
        /// disabled and reused, never destroyed, so OnDestroy would not run until
        /// the scene ended and the board would go on drawing a caption for an
        /// object that had left the ring.
        /// </summary>
        private void OnDisable()
        {
            Live = false;

            if (PickupLabelBoard.Instance != null)
            {
                PickupLabelBoard.Instance.Detach(this);
            }
        }
    }
}
