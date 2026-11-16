using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Draws the pickup captions as screen-space UI instead of world-space text,
    /// so temporal antialiasing cannot smear them.
    ///
    /// The world-space labels were legible but soft, and the cause was structural
    /// rather than a setting anyone had got wrong. Dumping the passes on both
    /// renderers of the pickup prefab shows it plainly:
    ///
    ///     Core  -> HDRP/Unlit                 8 passes, one of them MOTIONVECTORS
    ///     Label -> TextMeshPro/Distance Field 1 pass,   LightMode ""
    ///
    /// A label that writes no motion vectors gets reprojected by TAA using
    /// whatever motion sits in the buffer behind it, and these bob, billboard and
    /// orbit - so every frame was blended against a history taken from the wrong
    /// place. Raising the camera's TAA sharpening helps the whole image and does
    /// not fix that.
    ///
    /// This board lives on the Screen Space - Overlay canvas, which the UI
    /// composites after HDRP has finished its frame. Nothing in the post chain
    /// touches it, which is why the HUD beside it has always been sharp.
    ///
    /// **The cost is bloom.** Overlay UI is composited after bloom too, so an HDR
    /// face colour no longer glows by itself. The glow here is TextMeshPro's own
    /// Glow feature, which lives in the glyph shader and needs no post-processing
    /// at all - the same look by a different route.
    /// </summary>
    public sealed class PickupLabelBoard : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Camera the world positions are projected through. Falls back to Camera.main.")]
        private Camera view;

        [SerializeField]
        [Tooltip("Typeface for the captions. Left empty, TextMeshPro's default is used.")]
        private TMP_FontAsset font;

        [SerializeField]
        [Tooltip("Caption size in canvas units at the reference distance below.")]
        private float fontSize = 30f;

        [SerializeField]
        [Tooltip("Distance at which a caption is drawn at full size. Closer than this it " +
                 "stops growing; further away it shrinks towards the floor below.")]
        private float referenceDistance = 16f;

        [SerializeField]
        [Range(0.2f, 1f)]
        [Tooltip("Smallest fraction of the size above a distant caption may shrink to. " +
                 "Screen-space text does not shrink with distance on its own, and a caption " +
                 "on the far side of the arena drawn at full size reads as being nearby.")]
        private float minimumScale = 0.55f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Strength of the glyph glow. This stands in for the bloom that overlay UI " +
                 "cannot receive.")]
        private float glowPower = 0.4f;

        [SerializeField]
        [Tooltip("The arena axis the volcano stands on. Falls back to the world origin, which " +
                 "is where the island already is.")]
        private Transform arenaCenter;

        [SerializeField]
        [Tooltip("Radius of the volcano at the height everything sits at. Zero switches the " +
                 "occlusion test off and draws every caption, hidden or not.")]
        private float occluderRadius = 5f;

        [Header("Combat numbers")]
        [SerializeField]
        [ColorUsage(showAlpha: false, hdr: true)]
        [Tooltip("Colour of the experience earned where a kill happened.")]
        private Color experienceTint = new Color(1f, 0.78f, 0.25f);

        [SerializeField]
        [ColorUsage(showAlpha: false, hdr: true)]
        [Tooltip("Colour of the health restored where a piece of salvage was taken.")]
        private Color healthTint = new Color(0.35f, 1f, 0.55f);

        [SerializeField]
        [Range(0.2f, 3f)]
        [Tooltip("How long one number stays up, in seconds.")]
        private float numberSeconds = 0.9f;

        [SerializeField]
        [Range(0f, 8f)]
        [Tooltip("World units a number climbs per second while it fades, so several at the " +
                 "same spot separate instead of stacking into an unreadable pile.")]
        private float numberRise = 2.2f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("A kill's experience joins the last number instead of starting its own when " +
                 "that number is younger than this, in seconds, and near enough (below). 0 " +
                 "gives every kill its own number again.")]
        private float mergeSeconds = 0.35f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How near, in world units, a kill must be to the last number to join it.")]
        private float mergeDistance = 3.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Canvas units at the top of the screen that kill numbers fade out of before they " +
                 "reach. The timer and boss bars are there, and their empty track is see-through, " +
                 "so a number under one still showed across it. 0 lets numbers rise anywhere.")]
        private float hudTopBand = 100f;

        [Header("Offers out of sight")]
        [SerializeField]
        [Tooltip("Draw an offer's caption at the side of the screen, with an arrow, while the " +
                 "pickup is behind the volcano or off screen.")]
        private bool edgeArrows = true;

        [SerializeField]
        [Tooltip("Gap between the screen edge and an edge caption, in canvas units.")]
        private float edgeMargin = 28f;

        [SerializeField]
        [Range(0.2f, 1f)]
        [Tooltip("Size an edge caption shrinks to as its offer runs out, as a share of the " +
                 "caption size. It starts at full size.")]
        private float edgeSmallest = 0.55f;

        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("How far from the top and bottom of the screen an edge caption is kept, as a " +
                 "share of the screen's height.")]
        private float edgeBand = 0.25f;

        /// <summary>
        /// The board every label talks to.
        ///
        /// One per scene: the labels have no other way to find their canvas, and a
        /// second board would draw a second copy of every caption.
        /// </summary>
        public static PickupLabelBoard Instance { get; private set; }

        /// <summary>
        /// Labels currently asking to be drawn, in no particular order.
        ///
        /// Their order decides which pooled slot each one gets, which is why the
        /// slot's text and colour are rewritten every frame rather than at lease
        /// time - a label leaving the list shifts every label behind it onto a
        /// different slot.
        /// </summary>
        private readonly List<PickupLabel> attached = new List<PickupLabel>();

        /// <summary>
        /// One number owed to the screen: what it says, where it was earned, and
        /// when.
        ///
        /// A position rather than a Transform, unlike the captions above. What
        /// this names is an event and not an object - the enemy it belongs to is
        /// already back in the pool by the time the number is drawn, and following
        /// it would put the reward on whatever spawned next.
        /// </summary>
        private struct FloatingNumber
        {
            public Vector3 Origin;
            public string Caption;
            public Color Tint;
            public float Born;

            /// <summary>The experience it shows, so kills that join it can add to it. 0 for health.</summary>
            public int Amount;
        }

        /// <summary>
        /// Whether a kill's experience should join a number already up rather
        /// than start its own: it is young enough, and the kill is near where
        /// it now stands.
        ///
        /// Hydra's rounds take a cluster down within a few frames, and each
        /// kill used to raise its own "+22" from nearly the same point; they
        /// overlapped into a run of digits nobody could read (15 November
        /// 2026 capture). One number counting up says the same and stays legible.
        /// </summary>
        public static bool ShouldMerge(Vector3 numberNow, float age, Vector3 kill,
            float windowSeconds, float distance)
        {
            return windowSeconds > 0f
                && age <= windowSeconds
                && (numberNow - kill).sqrMagnitude <= distance * distance;
        }

        /// <summary>
        /// The most that may be up at once. Kills peak near eight a second and a
        /// number lives under one, so this is roughly three times what the game
        /// can actually produce; it exists so a runaway caller cannot grow the
        /// slot pool without bound, and the oldest goes first because it is the
        /// one already fading out.
        /// </summary>
        private const int MaxNumbers = 24;

        private readonly List<FloatingNumber> numbers = new List<FloatingNumber>();

        private readonly List<TextMeshProUGUI> slots = new List<TextMeshProUGUI>();

        /// <summary>
        /// Each slot's own material, kept alongside it so the per-frame path never
        /// touches TMP_Text.fontMaterial. Reading that property is what clones the
        /// shared material, and it is not obviously a mutating call - doing it in
        /// LateUpdate would be a clone attempt sixty times a second.
        /// </summary>
        private readonly List<Material> materials = new List<Material>();

        private static readonly int FaceColor = Shader.PropertyToID("_FaceColor");
        private static readonly int GlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowPower = Shader.PropertyToID("_GlowPower");
        private static readonly int GlowOuter = Shader.PropertyToID("_GlowOuter");
        private static readonly int GlowInner = Shader.PropertyToID("_GlowInner");
        private static readonly int GlowOffset = Shader.PropertyToID("_GlowOffset");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    "A second PickupLabelBoard is in the scene; every caption would be drawn " +
                    "twice. Leaving the first one in charge.", this);
                return;
            }

            Instance = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Each slot's material is a clone, created the moment fontMaterial was
            // read in Build. Nothing else frees them: TextMeshPro's own
            // DestroyImmediate of m_fontMaterial sits inside a commented-out block
            // in TextMeshProUGUI, so the clones outlive the scene and pile up one
            // per caption slot per load. Pickup.Tint dodges the same trap with a
            // property block; these cannot, because a CanvasRenderer ignores them.
            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i] == null)
                {
                    continue;
                }

                // Play mode defers to the end of the frame; the editor has no frame
                // to defer to, and leaving it to Destroy there does nothing at all.
                if (Application.isPlaying)
                {
                    Destroy(materials[i]);
                }
                else
                {
                    DestroyImmediate(materials[i]);
                }
            }

            materials.Clear();
            slots.Clear();
        }

        /// <summary>
        /// Puts the experience a kill was worth where the kill happened.
        ///
        /// Experience rather than damage, which is what a floating number in a
        /// shooter usually is, because damage in this game is always exactly 1 -
        /// every weapon, every enemy, the boss included. A column of ones rising
        /// off every hit would be thirteen hundred glyphs a run telling the player
        /// something they already know. The reward is the number that varies: a
        /// Fighter is worth three Scouts, and knowing that mid-run is what makes
        /// going after one rather than the other a decision. It is the amount
        /// the player's bar took, after the multiplier, which is why the player
        /// is the caller rather than the enemy that died.
        ///
        /// Silent when there is no board in the scene, matching GameSounds - the
        /// player should not have to know whether the HUD exists.
        /// </summary>
        public static void Experience(Vector3 where, int amount)
        {
            if (Instance == null || amount <= 0)
            {
                return;
            }

            Instance.AddExperience(where, amount);
        }

        /// <summary>
        /// Adds a kill's experience to the newest experience number when it
        /// is young and near, and starts a number otherwise. A joined number
        /// starts its life again from where it stands, so it keeps rising
        /// without a jump and stays up while the kills keep coming.
        /// </summary>
        private void AddExperience(Vector3 where, int amount)
        {
            float now = Time.unscaledTime;

            for (int i = numbers.Count - 1; i >= 0; i--)
            {
                FloatingNumber number = numbers[i];
                if (number.Amount <= 0)
                {
                    continue;
                }

                float age = now - number.Born;
                Vector3 standing = number.Origin + Vector3.up * (numberRise * age);

                if (ShouldMerge(standing, age, where, mergeSeconds, mergeDistance))
                {
                    number.Amount += amount;
                    number.Caption = "+" + number.Amount;
                    number.Origin = standing;
                    number.Born = now;
                    numbers[i] = number;
                    return;
                }

                // Only the newest experience number is a candidate. An older
                // one that happens to be near is already on its way out.
                break;
            }

            Add(where, "+" + amount, experienceTint);
            FloatingNumber added = numbers[numbers.Count - 1];
            added.Amount = amount;
            numbers[numbers.Count - 1] = added;
        }

        /// <summary>
        /// Puts the health a piece of salvage restored where it was taken.
        ///
        /// Salvage carries no caption of its own, so this is where the player
        /// learns what the green scrap was. Marked HP, because a bare "+1" in the
        /// same place as the experience numbers would read as another one of
        /// those in a different colour.
        /// </summary>
        public static void Health(Vector3 where, int amount)
        {
            if (Instance == null || amount <= 0)
            {
                return;
            }

            Instance.Add(where, "+" + amount + " HP", Instance.healthTint);
        }

        private void Add(Vector3 where, string caption, Color tint)
        {
            if (numbers.Count >= MaxNumbers)
            {
                numbers.RemoveAt(0);
            }

            numbers.Add(new FloatingNumber
            {
                Origin = where,
                Caption = caption,
                Tint = tint,

                // Unscaled, because both endings stop time outright. On scaled
                // time the last kill of a winning run would leave its number
                // frozen over the victory screen for as long as the screen was
                // up. HitFlash holds off for the same reason.
                Born = Time.unscaledTime,
            });
        }

        /// <summary>Starts drawing a label. Safe to call when it is already attached.</summary>
        public void Attach(PickupLabel label)
        {
            if (label != null && !attached.Contains(label))
            {
                attached.Add(label);
            }
        }

        public void Detach(PickupLabel label)
        {
            attached.Remove(label);
        }

        /// <summary>
        /// Puts every attached caption where its pickup is on screen.
        ///
        /// LateUpdate because the camera moves in its own, and projecting through
        /// a camera that has not finished moving puts the caption a frame behind
        /// the object it names - the one artefact this whole approach exists to
        /// avoid.
        /// </summary>
        private void LateUpdate()
        {
            Camera camera = Resolve();

            if (camera == null)
            {
                HideFrom(0);
                return;
            }

            int drawn = 0;
            int leftEdge = 0;
            int rightEdge = 0;

            occupied.Clear();
            pendingEdges.Clear();

            for (int i = 0; i < attached.Count; i++)
            {
                PickupLabel label = attached[i];

                if (label == null || !label.Live)
                {
                    continue;
                }

                Vector3 screen = camera.WorldToScreenPoint(label.transform.position);

                // Behind the camera projects to a mirrored point in front of it,
                // so without this a pickup at the player's back draws its caption
                // across the middle of the screen.
                bool behind = screen.z <= 0f;

                // In front of the camera is not the same as visible: the volcano
                // stands in the middle of the ring, and a pickup on the far side
                // projects to a perfectly good screen position behind it.
                bool hidden = behind || Occluded(camera.transform.position, label.transform.position);

                bool offScreen = !behind
                    && (screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height);

                if (!hidden && !offScreen)
                {
                    TextMeshProUGUI slot = Slot(drawn++);

                    slot.gameObject.SetActive(true);
                    slot.text = label.Caption;
                    Place(slot, screen, Side.Centre);
                    slot.fontSize = fontSize * ScaleAt(screen.z);

                    occupied.Add(Footprint(slot, screen, Side.Centre));
                    Paint(drawn - 1, label.Tint);
                    continue;
                }

                if (edgeArrows)
                {
                    pendingEdges.Add(new PendingEdge { Label = label, Screen = screen, Behind = behind });
                }
            }

            // After every caption on screen, so an edge caption can step
            // clear of one it would land on: offers sit at the ship's height,
            // so the one at the side and one just inside it share a line.
            float step = fontSize * 1.2f * CanvasScale();

            for (int i = 0; i < pendingEdges.Count; i++)
            {
                PendingEdge pending = pendingEdges[i];
                PickupLabel label = pending.Label;

                bool left = EdgeIsLeft(camera, pending.Screen, pending.Behind);
                int stacked = left ? leftEdge++ : rightEdge++;
                Side side = left ? Side.Left : Side.Right;

                TextMeshProUGUI edge = Slot(drawn++);
                edge.gameObject.SetActive(true);
                edge.text = left ? "<  " + label.Caption : label.Caption + "  >";
                edge.fontSize = fontSize * Mathf.Lerp(edgeSmallest, 1f, label.LifeLeft);

                Vector3 point = EdgePoint(pending.Screen, pending.Behind, left, stacked);
                Rect footprint = Footprint(edge, point, side);

                for (int tries = 0; tries < 4 && Overlaps(footprint, occupied); tries++)
                {
                    point.y -= step;
                    footprint.y -= step;
                }

                Place(edge, point, side);
                occupied.Add(footprint);
                Paint(drawn - 1, label.Tint);
            }

            drawn = DrawNumbers(camera, drawn);

            HideFrom(drawn);
        }

        /// <summary>
        /// Draws the combat numbers, retires the ones that have run out, and
        /// reports how many slots are now in use.
        ///
        /// Backwards, so retiring one does not skip the next. They share the
        /// caption slots rather than owning a pool of their own: both are one
        /// line of glowing text projected through the same camera, and a second
        /// pool would mean a second set of cloned materials to free and a second
        /// copy of the occlusion and distance rules to keep in step with these.
        /// </summary>
        private int DrawNumbers(Camera camera, int drawn)
        {
            for (int i = numbers.Count - 1; i >= 0; i--)
            {
                FloatingNumber number = numbers[i];
                float age = Time.unscaledTime - number.Born;

                if (age >= numberSeconds)
                {
                    numbers.RemoveAt(i);
                    continue;
                }

                Vector3 world = number.Origin + Vector3.up * (numberRise * age);
                Vector3 screen = camera.WorldToScreenPoint(world);

                // Same two rejections the captions make, and for the same
                // reasons: behind the camera projects to a mirrored point in
                // front of it, and the volcano stands between the camera and
                // half the ring.
                if (screen.z <= 0f || Occluded(camera.transform.position, world))
                {
                    continue;
                }

                // Kept out of the band the top bars live in. The board already
                // draws under the HUD, but an empty track is see-through, and
                // a number rising into the timer bar read as on top of it
                // (15 November 2026 capture).
                float clear = TopBandFade(screen.y);
                if (clear <= 0f)
                {
                    continue;
                }

                TextMeshProUGUI slot = Slot(drawn++);

                slot.gameObject.SetActive(true);
                slot.text = number.Caption;
                Place(slot, screen, Side.Centre);
                slot.fontSize = fontSize * ScaleAt(screen.z);

                // Faded through the face and glow alpha rather than by shrinking
                // or moving, so a number stays exactly as legible as the captions
                // beside it right up until it goes.
                Color tint = number.Tint;
                tint.a = (1f - (age / numberSeconds)) * clear;

                Paint(drawn - 1, tint);
            }

            return drawn;
        }

        /// <summary>
        /// 1 below the top band, falling to 0 across the band's lower edge, so
        /// a number fades as it rises into the bars rather than vanishing.
        /// </summary>
        private float TopBandFade(float screenY)
        {
            if (hudTopBand <= 0f)
            {
                return 1f;
            }

            float scale = CanvasScale();
            return BandFade(screenY, Screen.height - hudTopBand * scale, 24f * scale);
        }

        /// <summary>
        /// How visible something at a height is under a band that starts at
        /// <paramref name="bandFrom"/>: 1 a full <paramref name="fade"/>
        /// below it, 0 at it and above.
        /// </summary>
        public static float BandFade(float y, float bandFrom, float fade)
        {
            if (fade <= 0f)
            {
                return y < bandFrom ? 1f : 0f;
            }

            return Mathf.Clamp01((bandFrom - y) / fade);
        }

        private enum Side { Centre, Left, Right }

        /// <summary>An offer out of sight, waiting for the captions on screen to be placed first.</summary>
        private struct PendingEdge
        {
            public PickupLabel Label;
            public Vector3 Screen;
            public bool Behind;
        }

        private readonly List<PendingEdge> pendingEdges = new List<PendingEdge>();

        /// <summary>Screen rectangles the captions drawn this frame cover.</summary>
        private readonly List<Rect> occupied = new List<Rect>();

        /// <summary>
        /// The screen rectangle a caption covers at a point, from the width
        /// TextMeshPro would give its text at its current size.
        /// </summary>
        private Rect Footprint(TextMeshProUGUI slot, Vector3 point, Side side)
        {
            float scale = CanvasScale();
            Vector2 size = slot.GetPreferredValues(slot.text) * scale;
            float x = side == Side.Left ? point.x
                : side == Side.Right ? point.x - size.x
                : point.x - size.x * 0.5f;

            return new Rect(x, point.y - size.y * 0.5f, size.x, size.y);
        }

        /// <summary>Whether a rectangle overlaps any in a list.</summary>
        public static bool Overlaps(Rect rect, List<Rect> others)
        {
            for (int i = 0; i < others.Count; i++)
            {
                if (rect.Overlaps(others[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Which side of the screen points the shorter way round the ring to a
        /// pickup out of sight.
        ///
        /// The line from the camera through the volcano splits the ring in
        /// two, and each half projects onto its own side of the volcano on
        /// screen. So a pickup hidden behind it is on the left half when it
        /// projects left of the volcano's centre - even by a pixel - and the
        /// shorter way to it is left. Behind the camera the projection is
        /// mirrored, so the side flips.
        /// </summary>
        private bool EdgeIsLeft(Camera camera, Vector3 screen, bool behind)
        {
            float centre = Screen.width * 0.5f;
            Vector3 axis = arenaCenter != null ? arenaCenter.position : Vector3.zero;
            axis.y = camera.transform.position.y;
            Vector3 projected = camera.WorldToScreenPoint(axis);

            if (projected.z > 0f)
            {
                centre = projected.x;
            }

            float offset = screen.x - centre;
            return behind ? offset > 0f : offset < 0f;
        }

        /// <summary>
        /// Where an edge caption sits: just inside the screen's side, at the
        /// pickup's height on screen but kept within the middle band, and
        /// stepped down a line for each caption already on that side.
        /// </summary>
        private Vector3 EdgePoint(Vector3 screen, bool behind, bool left, int stacked)
        {
            float scale = CanvasScale();
            float lowest = Screen.height * edgeBand;
            float highest = Screen.height * (1f - edgeBand);
            float y = behind ? Screen.height * 0.5f : Mathf.Clamp(screen.y, lowest, highest);

            y -= stacked * fontSize * 1.2f * scale;

            float x = left ? edgeMargin * scale : Screen.width - edgeMargin * scale;
            return new Vector3(x, y, 0f);
        }

        private Canvas canvas;

        private float CanvasScale()
        {
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            return canvas != null ? canvas.scaleFactor : 1f;
        }

        /// <summary>
        /// Puts a slot at a screen point, centred on it or hung from it by one
        /// side. Slots change jobs every frame, so this writes the alignment
        /// and pivot each time, but only when they differ: either change
        /// makes TextMeshPro rebuild the caption.
        /// </summary>
        private static void Place(TextMeshProUGUI slot, Vector3 point, Side side)
        {
            TextAlignmentOptions align = side == Side.Left ? TextAlignmentOptions.Left
                : side == Side.Right ? TextAlignmentOptions.Right
                : TextAlignmentOptions.Center;

            Vector2 pivot = side == Side.Left ? new Vector2(0f, 0.5f)
                : side == Side.Right ? new Vector2(1f, 0.5f)
                : new Vector2(0.5f, 0.5f);

            if (slot.alignment != align)
            {
                slot.alignment = align;
            }

            if (slot.rectTransform.pivot != pivot)
            {
                slot.rectTransform.pivot = pivot;
            }

            slot.rectTransform.position = point;
        }

        /// <summary>
        /// How much to shrink a caption at a given distance.
        ///
        /// Screen-space text is the same size however far away its object is,
        /// which is the trade this board makes for staying sharp. Left alone, a
        /// pickup on the far side of the arena would announce itself as loudly as
        /// one an arm's length away. This gives back enough of the depth cue to
        /// read, without letting distant captions shrink to nothing.
        /// </summary>
        private float ScaleAt(float distance)
        {
            if (distance <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp(referenceDistance / distance, minimumScale, 1f);
        }

        /// <summary>
        /// Whether the volcano stands between the camera and a label.
        ///
        /// Done as arithmetic rather than with Physics.Linecast because the scene
        /// has no scenery colliders at all - not on the island, not on the ruins,
        /// not on the volcano. Giving the island a MeshCollider would put 40,000
        /// vertices of static geometry into the physics world for the first time,
        /// where bullets and enemies would then have to be kept from noticing it.
        /// That is a gameplay change to fix a caption.
        ///
        /// The test is flat because the arena is. Captioned pickups spawn at the
        /// player's own height (PickupSpawner passes player.position.y straight
        /// through for an upgrade offer; salvage has no caption) and the
        /// camera holds that height and looks along the horizontal, so every sight
        /// line that matters runs level at y = 7. A cone sliced at one height is a
        /// circle, which reduces the whole question to two dimensions.
        ///
        /// The radius came off the island mesh rather than out of the air. Sampling
        /// its 40,050 vertices in one-unit height bands gives a body of revolution
        /// reaching 4.988 out across the band around y = 7, tapering to 2.860 by
        /// y = 13. Below roughly y = 6.5 the reading jumps to 17.1+, which is the
        /// island's own rim rather than the cone, and is well under the sight line
        /// anyway.
        /// </summary>
        private bool Occluded(Vector3 eye, Vector3 target)
        {
            if (occluderRadius <= 0f)
            {
                return false;
            }

            Vector3 center = arenaCenter != null ? arenaCenter.position : Vector3.zero;

            Vector2 fromEye = new Vector2(eye.x - center.x, eye.z - center.z);
            Vector2 toTarget = new Vector2(target.x - center.x, target.z - center.z);
            Vector2 span = toTarget - fromEye;

            float lengthSquared = span.sqrMagnitude;
            if (lengthSquared < 0.000001f)
            {
                return false;
            }

            // Clamped to the segment, which is the whole point. Unclamped, the
            // nearest point on the infinite line can sit behind the camera or past
            // the pickup, and a caption in the clear would be hidden by a volcano
            // that is nowhere near the line of sight.
            float along = Mathf.Clamp01(-Vector2.Dot(fromEye, span) / lengthSquared);
            Vector2 nearest = fromEye + span * along;

            return nearest.sqrMagnitude < occluderRadius * occluderRadius;
        }

        /// <summary>
        /// Colours one slot's glyphs and their glow.
        ///
        /// Through the slot's own material instance rather than a
        /// MaterialPropertyBlock: property blocks are a MeshRenderer feature, and
        /// UI text draws through a CanvasRenderer, which ignores them. Reading
        /// fontMaterial clones the shared material - the very thing Pickup.Tint
        /// avoids - but here it clones once per pooled slot at startup and those
        /// slots live as long as the scene, rather than once per spawned object.
        ///
        /// The colour arrives HDR because the skill assets author it that way for
        /// bloom. Vertex colours clamp at 1, which costs nothing now: with no
        /// bloom to feed, all the values above 1 were ever going to do is
        /// saturate, and clamping keeps the hue while pinning it to full
        /// brightness.
        /// </summary>
        private void Paint(int index, Color color)
        {
            Material material = index < materials.Count ? materials[index] : null;

            if (material == null)
            {
                return;
            }

            material.SetColor(FaceColor, color);
            material.SetColor(GlowColor, color);
        }

        /// <summary>The slot at an index, building it the first time it is asked for.</summary>
        private TextMeshProUGUI Slot(int index)
        {
            while (slots.Count <= index)
            {
                slots.Add(Build(slots.Count));
            }

            return slots[index];
        }

        private TextMeshProUGUI Build(int index)
        {
            GameObject holder = new GameObject("Caption " + index, typeof(RectTransform));
            holder.transform.SetParent(transform, worldPositionStays: false);

            TextMeshProUGUI text = holder.AddComponent<TextMeshProUGUI>();

            if (font != null)
            {
                text.font = font;
            }

            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.UpperCase;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            // Sized generously and centred on the pickup. Nothing wraps, so the
            // box only has to be wider than the longest caption for centring to
            // have room to work.
            text.rectTransform.sizeDelta = new Vector2(600f, 60f);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // Reading fontMaterial here is what clones the shared material, so
            // every slot can carry its own colour. Doing it at build time keeps
            // the clone out of the per-frame path.
            Material material = text.fontMaterial;
            materials.Add(material);

            if (material != null)
            {
                material.EnableKeyword("GLOW_ON");
                material.SetFloat(GlowPower, glowPower);
                material.SetFloat(GlowOuter, 0.3f);
                material.SetFloat(GlowInner, 0.05f);
                material.SetFloat(GlowOffset, 0f);

                // Not optional, and not obvious. TextMeshPro sizes the quad it
                // builds for each glyph from the material's padding requirements,
                // and turning the glow on widens them. Without recomputing, every
                // glyph keeps a quad cut for a material with no glow, the distance
                // field saturates across the whole of it, and the caption renders
                // as a row of solid blocks - correct layout, correct colour,
                // no letters. That is exactly what this did the first time.
                text.UpdateMeshPadding();
            }

            return text;
        }

        private void HideFrom(int index)
        {
            for (int i = index; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].gameObject.activeSelf)
                {
                    slots[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// The camera to project through, re-found when it goes.
        ///
        /// Explicit null comparison rather than ??, because Unity overloads == on
        /// Object to report a destroyed object as null and the null-coalescing
        /// operator does not go through that overload.
        /// </summary>
        private Camera Resolve()
        {
            if (view == null)
            {
                view = Camera.main;
            }

            return view;
        }
    }
}
