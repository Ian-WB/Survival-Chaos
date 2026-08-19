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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
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
                if (screen.z <= 0f)
                {
                    continue;
                }

                // In front of the camera is not the same as visible: the volcano
                // stands in the middle of the ring, and a pickup on the far side
                // projects to a perfectly good screen position behind it.
                if (Occluded(camera.transform.position, label.transform.position))
                {
                    continue;
                }

                TextMeshProUGUI slot = Slot(drawn++);

                slot.gameObject.SetActive(true);
                slot.text = label.Caption;
                slot.rectTransform.position = screen;
                slot.fontSize = fontSize * ScaleAt(screen.z);

                Paint(drawn - 1, label.Tint);
            }

            HideFrom(drawn);
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
        /// The test is flat because the arena is. Pickups spawn at the player's own
        /// height (PickupSpawner passes player.position.y straight through) and the
        /// camera holds that height and looks along the horizontal, so every sight
        /// line that matters runs level at y = 7. A cone sliced at one height is a
        /// circle, which reduces the whole question to two dimensions.
        ///
        /// The radius came off the island mesh rather than out of the air. Sampling
        /// its 40,050 vertices in one-unit height bands gives a body of revolution
        /// measuring 4.99 across the band around y = 7, tapering to 2.86 by y = 13.
        /// Below roughly y = 6.5 the reading jumps to 17+, which is the island's own
        /// rim rather than the cone, and is well under the sight line anyway.
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
