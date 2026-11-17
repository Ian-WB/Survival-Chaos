using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// One collectible item on the ring: the thing the player flies into to take
    /// an upgrade.
    ///
    /// This component knows how to look like a pickup, how long it lives, and
    /// how to notice the player. It deliberately does not know what it grants -
    /// the payload is carried opaquely and handed back to the spawner on
    /// contact. Keeping the reward out of here is what lets one prefab serve
    /// every skill and salvage as well.
    ///
    /// Spawned through ObjectPool, so everything that belongs to one life is set
    /// in <see cref="Configure"/> rather than in a constructor or in Awake.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Pickup : MonoBehaviour
    {
        [Header("Motion")]
        [SerializeField]
        [Tooltip("Degrees per second the item spins about its own axis. Pure decoration - " +
                 "it makes a small object read as collectible rather than as scenery.")]
        private float spinSpeed = 120f;

        [SerializeField]
        [Tooltip("How far the item drifts up and down from where it was placed, in world units.")]
        private float bobHeight = 0.25f;

        [SerializeField]
        [Tooltip("Full up-and-down cycles per second.")]
        private float bobFrequency = 0.8f;

        [Header("Expiry")]
        [SerializeField]
        [Tooltip("Seconds of life left at which the item starts flashing, warning that " +
                 "the offer is about to be forfeited.")]
        private float warnWithin = 3f;

        [SerializeField]
        [Tooltip("Flashes per second during the warning.")]
        private float warnFlashRate = 4f;

        [Header("Look")]
        [SerializeField]
        [Tooltip("Renderer tinted to the payload's colour. Left empty, the first renderer " +
                 "found in the children is used.")]
        private Renderer tintTarget;

        [SerializeField]
        [Tooltip("Names the upgrade above the item. Optional - without one the pickup is " +
                 "told apart by colour alone, which is how it worked before.")]
        private PickupLabel label;

        [Header("Part (17 November 2026)")]
        [SerializeField]
        [Tooltip("Additive glow drawn behind an upgrade's part, tinted per upgrade. Without one the " +
                 "part shows with no glow.")]
        private Material partGlow;

        [SerializeField]
        [Range(1f, 4f)]
        [Tooltip("The least a part is drawn on the ring, against its size on the hull.")]
        private float partSize = 3f;

        [SerializeField]
        [Range(1f, 6f)]
        [Tooltip("The most a part is drawn on the ring, against its size on the hull. A small part " +
                 "is drawn larger than a big one, toward the same size, inside these two.")]
        private float partSizeMost = 5.5f;

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Half the length the parts are drawn at on the ring, in world units, as far as " +
                 "the two limits above allow.")]
        private float partRadius = 0.9f;

        [SerializeField]
        [Tooltip("Degrees a second the part turns on the ring.")]
        private float partSpin = 40f;

        [SerializeField]
        [Range(0f, 60f)]
        [Tooltip("Degrees the part leans toward the camera, so a flat part shows its face.")]
        private float partTilt = 24f;

        [SerializeField]
        [Range(0.1f, 1f)]
        [Tooltip("Seconds the part flies from the ring to its mount.")]
        private float pullSeconds = 0.35f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How far the flight bows out at its middle, against its length.")]
        private float pullBow = 0.3f;

        [SerializeField]
        [Range(0.1f, 1f)]
        [Tooltip("Seconds the offers not taken take to break up and go.")]
        private float breakSeconds = 0.3f;

        [SerializeField]
        [Range(0.05f, 0.6f)]
        [Tooltip("Seconds the flash at the mount lasts once the part clamps on.")]
        private float flashSeconds = 0.2f;

        [SerializeField]
        [Range(0f, 2f)]
        [Tooltip("How far off its mount the part stops to line up before it slams home, in world units.")]
        private float alignGap = 0.7f;

        [SerializeField]
        [Range(0f, 0.4f)]
        [Tooltip("Seconds the part holds off its mount, turning into its exact angle.")]
        private float alignSeconds = 0.12f;

        [SerializeField]
        [Range(0f, 90f)]
        [Tooltip("Degrees the part is still turned from its mounted angle when it arrives to line up.")]
        private float alignTwist = 35f;

        [SerializeField]
        [Range(0.01f, 0.2f)]
        [Tooltip("Seconds the slam from the lining-up point onto the mount takes.")]
        private float slamSeconds = 0.05f;

        /// <summary>Where a part pickup is in its life once it is no longer waiting on the ring.</summary>
        private enum Phase
        {
            Waiting,
            Pulling,
            Breaking,
            Flashing,
        }

        private Phase phase;
        private PickupPartLook look;
        private bool showsPart;
        private Transform mount;
        private Color partColor;
        private float phaseTime;
        private float spin;
        private Vector3 pullFrom;
        private Quaternion pullTurn;
        private Vector3 pullSize;
        private Vector3 pullBowBy;
        private Vector3 lastStep;
        private Vector3 alignAway;
        private Quaternion alignOff;
        private Vector3 drift;
        private Vector3 tumble;

        /// <summary>
        /// HDRP's Unlit shader takes its emission from this property. Setting the
        /// base colour alone leaves the object lit but not glowing, which is the
        /// difference between a pickup you notice mid-fight and one you do not.
        /// </summary>
        private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");

        private static readonly int UnlitColor = Shader.PropertyToID("_UnlitColor");

        /// <summary>
        /// Applied through a property block rather than by assigning to
        /// <c>renderer.material</c>. The latter silently clones the material on
        /// first touch, so every pickup would leak an instance that outlives it
        /// and never returns to the pool with the rest of the object.
        /// </summary>
        private MaterialPropertyBlock tintBlock;

        private PickupSpawner owner;
        private Vector3 anchor;
        private float expiresAt;
        private bool collected;

        /// <summary>The last whole second of the warning that has ticked, so each ticks once.</summary>
        private int lastTick;

        /// <summary>
        /// The tinted core's authored size, read before any life can change it.
        /// Salvage shrinks the core, and a pooled pickup keeps whatever size it
        /// last had - an upgrade drawn from the pool after a piece of salvage
        /// would otherwise come out scrap-sized.
        /// </summary>
        private Vector3 coreScale = Vector3.one;

        /// <summary>
        /// Where in its bob cycle this pickup starts.
        ///
        /// Without it every pickup drives off the same clock and a three-way
        /// offer rises and falls in lockstep, which reads as one animated object
        /// in three places rather than three separate things.
        /// </summary>
        private float bobPhase;

        /// <summary>What this pickup grants. Read by the spawner, opaque here.</summary>
        public SkillDefinition Skill { get; private set; }

        /// <summary>Health granted when <see cref="Skill"/> is null.</summary>
        public int HealAmount { get; private set; }

        /// <summary>The offer this belongs to, or null for a standalone drop.</summary>
        public SkillOffer Offer { get; set; }

        /// <summary>The lifetime this pickup was given when it went out.</summary>
        private float lifespan;

        /// <summary>
        /// How much of its time is left, 1 as it goes out and 0 as it expires.
        /// Read by the edge arrow that points at an offer out of sight.
        /// </summary>
        public float LifeLeft => lifespan > 0f ? Mathf.Clamp01((expiresAt - Time.time) / lifespan) : 1f;

        /// <summary>True while this is an upgrade drawn as its part, waiting on the ring to be pulled in.</summary>
        public bool WaitsAsPart => showsPart && phase == Phase.Waiting && !collected;

        /// <summary>True while the part is flying to the ship.</summary>
        public bool Pulling => phase == Phase.Pulling;

        /// <summary>The trigger's radius in the world: how far from the middle contact is made.</summary>
        public float ContactRadius
        {
            get
            {
                if (!TryGetComponent(out SphereCollider sphere))
                {
                    return 0f;
                }

                Vector3 scale = transform.lossyScale;
                return sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            }
        }

        private void Awake()
        {
            if (tintTarget == null)
            {
                tintTarget = GetComponentInChildren<Renderer>();
            }

            if (tintTarget != null)
            {
                coreScale = tintTarget.transform.localScale;
            }
        }

        /// <summary>
        /// Sets the pickup up for a life. Called immediately after Spawn, before
        /// the object has had a frame to run.
        /// </summary>
        /// <param name="spawner">Told when this is taken or runs out.</param>
        /// <param name="skill">The upgrade granted, or null for salvage.</param>
        /// <param name="healAmount">Health granted when <paramref name="skill"/> is null.</param>
        /// <param name="color">Glow colour, normally the skill's own.</param>
        /// <param name="caption">
        /// What the label above the item says, or null or empty for none. A
        /// finished string rather than something to derive from
        /// <paramref name="skill"/>: the caller already knows how many times the
        /// skill has been taken, which is what decides the wording of a
        /// multi-stage upgrade, and this component knows nothing about its
        /// payload on purpose.
        /// </param>
        /// <param name="lifetime">Seconds before the pickup gives up and expires.</param>
        /// <param name="size">
        /// The core's size against the prefab's, 1 for an upgrade. The collider
        /// is left alone: a smaller pickup should look smaller, not be harder to
        /// take.
        /// </param>
        public void Configure(
            PickupSpawner spawner,
            SkillDefinition skill,
            int healAmount,
            Color color,
            string caption,
            float lifetime,
            float size)
        {
            owner = spawner;
            Skill = skill;
            HealAmount = healAmount;
            Offer = null;

            collected = false;
            phase = Phase.Waiting;
            transform.rotation = Quaternion.identity;
            HidePart();
            anchor = transform.position;
            bobPhase = Random.value * Mathf.PI * 2f;

            // Unscaled would keep the clock running through the death screen.
            // Time.time is the right one: a paused game should not expire an
            // offer the player never got to answer.
            expiresAt = Time.time + Mathf.Max(0.1f, lifetime);
            lifespan = Mathf.Max(0.1f, lifetime);
            lastTick = int.MaxValue;

            // Only a child's scale. On the root it would scale the trigger too.
            if (tintTarget != null && tintTarget.transform != transform)
            {
                tintTarget.transform.localScale = coreScale * Mathf.Max(0.01f, size);
            }

            Tint(color);

            if (label != null)
            {
                label.Show(caption, color);
            }
        }

        /// <summary>
        /// Changes what an upgrade already on the ring grants, or only what its
        /// caption says, without starting its life over.
        ///
        /// For an offer that went stale while it was out: the same skill was
        /// taken from another offer first, so this one's stage has moved on, or
        /// its gate or pick limit now shuts it. The clock, the anchor and the
        /// size carry on, so the colour and the caption are all that change.
        /// </summary>
        public void Retarget(SkillDefinition skill, Color color, string caption)
        {
            Skill = skill;
            Tint(color);

            if (label != null)
            {
                label.Show(caption, color);
            }
        }

        /// <summary>
        /// Draws this upgrade as the stage it fits: the part instead of the
        /// glowing core, and no caption over it. The label keeps the name for
        /// the pause screen's offers card, and the edge arrow draws the dots.
        /// </summary>
        public void ShowPart(ShipPartStage stage, Transform partMount, int pick, Color color, string effect = null)
        {
            if (stage.mesh == null || partMount == null || !EnsureLook())
            {
                HidePart();
                return;
            }

            showsPart = true;
            mount = partMount;
            partColor = color;
            transform.rotation = Quaternion.identity;
            look.Show(stage, partMount, pick, color, partRadius, partSize, partSizeMost);

            if (tintTarget != null)
            {
                tintTarget.enabled = false;
            }

            if (label != null)
            {
                label.Tier = pick;
                label.Effect = effect;
                label.Upgrade = stage.upgrade;
            }
        }

        /// <summary>Back to the glowing core, for salvage and for anything with no part.</summary>
        public void HidePart()
        {
            showsPart = false;
            mount = null;
            look?.Hide();

            if (tintTarget != null)
            {
                tintTarget.enabled = true;
            }

            if (label != null)
            {
                label.Tier = 0;
                label.Effect = null;
                label.Upgrade = null;
            }
        }

        /// <summary>
        /// The part breaks off the ring and flies to its mount. Called by the
        /// spawner, which has already locked the choice.
        /// </summary>
        public void StartPull()
        {
            if (!showsPart || look == null)
            {
                return;
            }

            collected = true;
            phase = Phase.Pulling;
            phaseTime = 0f;
            HideLabel();
            look.ShowDots(false);
            look.Part.enabled = true;

            look.CentreOnMount();
            Transform holder = look.Holder;
            pullFrom = transform.position;
            pullTurn = holder.rotation;
            pullSize = holder.localScale;
            lastStep = Vector3.zero;

            Vector3 toShip = mount != null ? mount.position - pullFrom : Vector3.zero;
            Vector3 side = Vector3.Cross(toShip, Vector3.up).normalized * (Random.value < 0.5f ? -1f : 1f);
            pullBowBy = (side * 0.8f + Vector3.up * 0.6f) * (toShip.magnitude * pullBow);

            // It lines up on the side it came from, held in the mount's own
            // turn so the point follows the ship as it banks.
            Vector3 away = toShip.sqrMagnitude > 0.000001f ? -toShip.normalized : Vector3.up;
            alignAway = mount != null ? Quaternion.Inverse(mount.rotation) * away : away;
            alignOff = Quaternion.AngleAxis(alignTwist, Random.onUnitSphere);
        }

        /// <summary>The stage pivot the part flies to; null when it shows no part.</summary>
        public Transform Mount => mount;

        /// <summary>Where the part stops to line up: off its mount, on the side it came from.</summary>
        private Vector3 AlignPoint => mount.position + mount.rotation * alignAway * alignGap;

        /// <summary>One of the offers not taken: it breaks up where it is and goes.</summary>
        public void BreakApart()
        {
            collected = true;
            HideLabel();

            if (!showsPart || look == null)
            {
                ObjectPool.Despawn(gameObject);
                return;
            }

            phase = Phase.Breaking;
            phaseTime = 0f;
            look.ShowDots(false);
            pullSize = look.Holder.localScale;
            drift = (Random.onUnitSphere + Vector3.up * 0.6f) * 1.6f;
            tumble = Random.onUnitSphere;
        }

        private void HideLabel()
        {
            if (label != null)
            {
                label.Show(string.Empty, partColor);
            }
        }

        private bool EnsureLook()
        {
            if (look != null)
            {
                return true;
            }

            if (tintTarget == null || !tintTarget.TryGetComponent(out MeshFilter core))
            {
                return false;
            }

            look = new PickupPartLook(transform, partGlow, core.sharedMesh, tintTarget.sharedMaterial);
            return true;
        }

        /// <summary>Real time that stops for the pause menu: Slow Mo does not drag the flight out.</summary>
        private static float FlightDelta => PauseMenu.GameIsPaused ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        private void StepPull()
        {
            // Death or the end of the run mid-flight: nothing is granted and
            // nothing is left on the ring.
            if (mount == null || owner == null || !owner.CanLand)
            {
                ObjectPool.Despawn(gameObject);
                return;
            }

            phaseTime += FlightDelta;
            Transform holder = look.Holder;
            Quaternion lined = mount.rotation * alignOff;
            float flying = Mathf.Max(0.05f, pullSeconds);
            float aligned = flying + alignSeconds;

            // Three beats, after the suit-up: fly in, hold off the mount and
            // turn into place, then slam the last stretch home.
            if (phaseTime < flying)
            {
                float t = phaseTime / flying;
                float eased = PartOffer.Ease(t);
                transform.position = PartOffer.Flight(pullFrom, AlignPoint, pullBowBy, eased);
                holder.rotation = Quaternion.Slerp(pullTurn, lined, Mathf.SmoothStep(0f, 1f, t));
                holder.localScale = Vector3.Lerp(pullSize, look.HullScale, eased);
                look.Glow(1f - 0.6f * eased, Mathf.Lerp(1f, 1f / look.Size, eased));
                return;
            }

            holder.localScale = look.HullScale;

            if (phaseTime < aligned)
            {
                float k = (phaseTime - flying) / Mathf.Max(0.0001f, alignSeconds);
                Vector3 shiver = Random.insideUnitSphere * (0.03f * (1f - k));
                transform.position = AlignPoint + shiver;
                holder.rotation = Quaternion.Slerp(lined, mount.rotation, Mathf.SmoothStep(0f, 1f, k));
                // It charges up as it lines up.
                look.Glow(0.4f + 0.8f * k, 1f / look.Size, 0.5f * k);
                return;
            }

            float s = Mathf.Clamp01((phaseTime - aligned) / slamSeconds);
            holder.rotation = mount.rotation;
            transform.position = Vector3.Lerp(AlignPoint, mount.position, s * s * s);
            lastStep = mount.position - AlignPoint;

            if (s < 1f)
            {
                return;
            }

            transform.position = mount.position;

            if (!owner.Land(this, lastStep))
            {
                ObjectPool.Despawn(gameObject);
                return;
            }

            // The stage is on the ship as of this frame: the copy goes in the same one.
            holder.gameObject.SetActive(false);
            phase = Phase.Flashing;
            phaseTime = 0f;
        }

        private void StepFlash()
        {
            phaseTime += FlightDelta;
            float k = Mathf.Clamp01(phaseTime / flashSeconds);

            if (mount != null)
            {
                transform.position = mount.position;
            }

            look.Glow(3f * (1f - k), Mathf.Lerp(0.35f, 0.9f, k), 0.65f);

            if (k >= 1f)
            {
                ObjectPool.Despawn(gameObject);
            }
        }

        private void StepBreak()
        {
            phaseTime += Time.deltaTime;
            float k = Mathf.Clamp01(phaseTime / breakSeconds);
            Transform holder = look.Holder;

            transform.position += drift * Time.deltaTime;
            holder.rotation = Quaternion.AngleAxis(540f * Time.deltaTime, tumble) * holder.rotation;
            holder.localScale = pullSize * (1f - 0.7f * k);
            look.Part.enabled = Mathf.Repeat(k * 9f, 1f) > k * 0.8f;
            // The glow goes faster than the part, so a flickered-out part never
            // leaves a bare disc of colour behind.
            look.Glow((1f - k) * (1f - k), 1f - 0.3f * k);

            if (k >= 1f)
            {
                ObjectPool.Despawn(gameObject);
            }
        }

        private void LateUpdate()
        {
            if (!showsPart || look == null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            if (phase == Phase.Waiting)
            {
                Vector3 right = camera.transform.right;
                right.y = 0f;
                right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
                look.Holder.rotation = Quaternion.AngleAxis(partTilt, right) * Quaternion.AngleAxis(spin, Vector3.up);
            }

            look.Face(camera, transform.position);
        }

        private void Update()
        {
            switch (phase)
            {
                case Phase.Pulling:
                    StepPull();
                    return;
                case Phase.Breaking:
                    StepBreak();
                    return;
                case Phase.Flashing:
                    StepFlash();
                    return;
            }

            if (collected)
            {
                return;
            }

            float remaining = expiresAt - Time.time;

            if (remaining <= 0f)
            {
                Expire();
                return;
            }

            // The anchor rather than the position, so the bob and spin carry on
            // around wherever the Magnet upgrade has drawn it to.
            if (owner != null)
            {
                anchor = owner.Attract(anchor, Time.deltaTime);
            }

            Animate(remaining);
            TickWarning(remaining);

            if (WaitsAsPart && owner != null && owner.InPullReach(this))
            {
                owner.BeginPull(this);
            }
        }

        /// <summary>
        /// A tick on each of the last whole seconds of an upgrade on offer,
        /// alongside the flashing, which was silent until 30 September 2026.
        /// Salvage flashes without it: it turns up in the middle of fights all
        /// the time, and a tick for every piece would be noise. The pickups of
        /// one offer tick together, and the sound's retrigger guard makes that
        /// one tick.
        /// </summary>
        private void TickWarning(float remaining)
        {
            if (Skill == null || remaining > warnWithin)
            {
                return;
            }

            int second = Mathf.CeilToInt(remaining);
            if (second >= lastTick)
            {
                return;
            }

            lastTick = second;
            if (GameSounds.Instance != null)
            {
                GameSounds.Play(GameSounds.Instance.OfferExpiring);
            }
        }

        /// <summary>
        /// Spin, bob, and - once the clock is nearly out - flash.
        ///
        /// The flash is the only honest way to run a forfeit timer. An offer that
        /// vanishes without warning reads as a bug; one that visibly runs down
        /// reads as a decision the player was given and did not take.
        /// </summary>
        private void Animate(float remaining)
        {
            Vector3 position = anchor;
            position.y += Mathf.Sin(bobPhase + Time.time * bobFrequency * Mathf.PI * 2f) * bobHeight;
            transform.position = position;

            // A part turns slowly and is turned in LateUpdate, against the
            // camera. Its last seconds thin the glow and flicker the part.
            if (showsPart)
            {
                spin += partSpin * Time.deltaTime;

                if (look != null)
                {
                    float left = remaining <= warnWithin ? Mathf.Clamp01(remaining / warnWithin) : 1f;
                    look.Glow(Mathf.Lerp(0.2f, 1f, left), Mathf.Lerp(0.7f, 1f, left));
                    look.Part.enabled = remaining > warnWithin
                        || Mathf.Repeat(remaining * warnFlashRate * 1.5f, 1f) > 0.3f;
                }

                return;
            }

            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

            // Only the core flashes. The label is left steady on purpose: the
            // last three seconds are exactly when the player is still deciding,
            // and text that is missing half of them is text they have to wait
            // for. The warning is carried by the thing being warned about.
            if (remaining <= warnWithin && tintTarget != null)
            {
                bool on = Mathf.Repeat(remaining * warnFlashRate, 1f) > 0.5f;
                tintTarget.enabled = on;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Guarded because the trigger can fire more than once in a physics
            // step, and a second entry would apply the upgrade twice.
            if (collected || !other.CompareTag("Player"))
            {
                return;
            }

            // The run is decided: its results are settled, and an upgrade
            // taken in the beat before the card would change a build nobody
            // flies again. Left where it is rather than marked taken.
            if (RunOutcome.Decided)
            {
                return;
            }

            // A part is pulled in rather than taken on contact. Contact only
            // starts the pull, for a ship fast enough to pass the reach in a frame.
            if (showsPart)
            {
                if (owner != null && phase == Phase.Waiting)
                {
                    owner.BeginPull(this);
                }

                return;
            }

            collected = true;

            if (owner != null)
            {
                owner.OnCollected(this);
            }
        }

        private void Expire()
        {
            collected = true;

            if (owner != null)
            {
                owner.OnExpired(this);
            }
        }

        /// <summary>
        /// Puts the renderer back before the object returns to the pool.
        ///
        /// The flash leaves the renderer disabled about half the time, and a
        /// pooled object keeps whatever state it died in. Without this a pickup
        /// that timed out mid-flash would come back invisible - and be collected
        /// by a player who could not see it.
        /// </summary>
        private void OnDisable()
        {
            if (tintTarget != null)
            {
                tintTarget.enabled = true;
            }

            // Pooled: the next life starts on the ring with the core, whatever this one ended as.
            phase = Phase.Waiting;
            HidePart();
        }

        private void Tint(Color color)
        {
            if (tintTarget == null)
            {
                tintTarget = GetComponentInChildren<Renderer>();
            }

            if (tintTarget == null)
            {
                return;
            }

            tintBlock ??= new MaterialPropertyBlock();
            tintTarget.GetPropertyBlock(tintBlock);

            tintBlock.SetColor(EmissiveColor, color);

            // The unlit colour is what shows where bloom does not reach - the
            // object's own surface. Kept in step so the pickup is the same colour
            // whether or not post-processing is on, which matters because the
            // bottom quality tiers switch bloom off.
            tintBlock.SetColor(UnlitColor, color);

            tintTarget.SetPropertyBlock(tintBlock);
        }
    }
}
