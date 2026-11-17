using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// A soft red flare on the screen edge a hit came from, with a small
    /// chevron pointing out toward it (17 November 2026 HUD redesign). It
    /// replaces any marker around the ship: nothing circles the ship.
    ///
    /// The direction is the source's screen position seen from the ship's,
    /// carried out to the screen edge. A source behind the camera maps to the
    /// bottom edge. The lance gives no place, so its hits point at the boss if
    /// it is up, and otherwise show nothing rather than a guess.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Hit Flare")]
    [RequireComponent(typeof(Graphic))]
    public sealed class HitFlare : MonoBehaviour
    {
        /// <summary>Which screen edge, in the Edge Glow shader's order.</summary>
        public enum Side { Left, Right, Bottom, Top }

        [SerializeField]
        [Tooltip("The small arrow on the flare's edge. Optional.")]
        private RectTransform chevron;

        [SerializeField]
        [Tooltip("How far in from the edge the chevron sits, in canvas units.")]
        private float chevronInset = 26f;

        [SerializeField]
        [Range(0.1f, 0.5f)]
        [Tooltip("Half the flare's length along its edge, as a fraction of that edge.")]
        private float spread = 0.32f;

        [SerializeField]
        private float fadeIn = 0.02f;

        [SerializeField]
        private float hold = 0.2f;

        [SerializeField]
        private float fadeOut = 0.35f;

        [SerializeField]
        [Range(0f, 1f)]
        private float strength = 0.85f;

        private static readonly int SidesId = Shader.PropertyToID("_Sides");
        private static readonly int CentreId = Shader.PropertyToID("_Centre");
        private static readonly int SpreadId = Shader.PropertyToID("_Spread");

        [SerializeField]
        [Tooltip("The ship the flare is measured from. Found if left empty.")]
        private Player player;

        private Graphic graphic;
        private Graphic chevronGraphic;
        private Material instance;
        private float started = -100f;

        /// <summary>
        /// The edge a hit from <paramref name="source"/> lands on for a ship
        /// at <paramref name="ship"/>, both in screen pixels, and where along
        /// that edge (0 at its left or bottom end). Null when the two points
        /// coincide. A source behind the camera (z below 0) is the bottom edge.
        /// </summary>
        public static (Side side, float along)? EdgeFor(Vector3 ship, Vector3 source, Vector2 screen)
        {
            if (source.z < 0f)
            {
                return (Side.Bottom, 0.5f);
            }

            Vector2 d = (Vector2)source - (Vector2)ship;
            if (d.sqrMagnitude < 1e-4f || screen.x <= 0f || screen.y <= 0f)
            {
                return null;
            }

            // Out from the middle of the screen along the hit's direction,
            // to whichever edge comes first.
            float toSide = Mathf.Abs(d.x) > 1e-5f ? (screen.x * 0.5f) / Mathf.Abs(d.x) : float.MaxValue;
            float toEnd = Mathf.Abs(d.y) > 1e-5f ? (screen.y * 0.5f) / Mathf.Abs(d.y) : float.MaxValue;

            if (toSide <= toEnd)
            {
                float along = Mathf.Clamp01(0.5f + d.y * toSide / screen.y);
                return (d.x < 0f ? Side.Left : Side.Right, along);
            }

            float across = Mathf.Clamp01(0.5f + d.x * toEnd / screen.x);
            return (d.y < 0f ? Side.Bottom : Side.Top, across);
        }

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
            if (graphic.material != null)
            {
                instance = new Material(graphic.material);
                graphic.material = instance;
            }

            chevronGraphic = chevron != null ? chevron.GetComponent<Graphic>() : null;
            SetAlpha(0f);
        }

        private void OnEnable() => Player.Hurt += OnHurt;

        private void OnDisable() => Player.Hurt -= OnHurt;

        private void OnDestroy()
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        private void OnHurt(Vector3? from)
        {
            Camera view = Camera.main;
            if (player == null)
            {
                player = FindAnyObjectByType<Player>();
            }
            if (view == null || player == null || instance == null)
            {
                return;
            }

            Vector3? source = from;
            if (source == null)
            {
                // The lance gives no place: point at the middle of the boss as
                // drawn, while it is up. Not the lance's own transform, which
                // can sit behind the camera (17 November 2026, in play).
                BossEmitter boss = FindAnyObjectByType<BossEmitter>();
                if (boss != null && boss.isActiveAndEnabled)
                {
                    source = DrawnCentre(boss.transform);
                }
                else
                {
                    return;
                }
            }

            var edge = EdgeFor(view.WorldToScreenPoint(player.transform.position),
                view.WorldToScreenPoint(source.Value), new Vector2(Screen.width, Screen.height));
            if (edge == null)
            {
                return;
            }

            Show(edge.Value.side, edge.Value.along);
        }

        /// <summary>
        /// The middle of what is drawn under a transform: the boss's origin is
        /// not where its hull is on screen (17 November 2026, in play).
        /// </summary>
        private static Vector3 DrawnCentre(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return root.position;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.center;
        }

        /// <summary>Lights the flare on one edge, at a point along it.</summary>
        public void Show(Side side, float along)
        {
            Vector4 sides = Vector4.zero;
            sides[(int)side] = 1f;
            instance.SetVector(SidesId, sides);
            instance.SetFloat(CentreId, along);
            instance.SetFloat(SpreadId, spread);

            PlaceChevron(side, along);
            started = Time.unscaledTime;
        }

        private void PlaceChevron(Side side, float along)
        {
            if (chevron == null)
            {
                return;
            }

            bool vertical = side == Side.Left || side == Side.Right;
            Vector2 anchor = vertical
                ? new Vector2(side == Side.Left ? 0f : 1f, along)
                : new Vector2(along, side == Side.Bottom ? 0f : 1f);

            chevron.anchorMin = anchor;
            chevron.anchorMax = anchor;
            chevron.pivot = new Vector2(0.5f, 0.5f);

            Vector2 inward = side == Side.Left ? Vector2.right
                : side == Side.Right ? Vector2.left
                : side == Side.Bottom ? Vector2.up
                : Vector2.down;
            chevron.anchoredPosition = inward * chevronInset;

            // The glyph is ">", pointing right: turned to point out of the
            // screen, toward what fired.
            float angle = side == Side.Right ? 0f : side == Side.Left ? 180f : side == Side.Top ? 90f : -90f;
            chevron.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        private void LateUpdate()
        {
            float t = Time.unscaledTime - started;
            float a;
            if (t < fadeIn)
            {
                a = t / Mathf.Max(0.001f, fadeIn);
            }
            else if (t < fadeIn + hold)
            {
                a = 1f;
            }
            else
            {
                a = 1f - (t - fadeIn - hold) / Mathf.Max(0.001f, fadeOut);
            }

            SetAlpha(Mathf.Clamp01(a) * strength);
        }

        private void SetAlpha(float a)
        {
            bool on = a > 0.001f;
            if (graphic != null)
            {
                if (graphic.enabled != on)
                {
                    graphic.enabled = on;
                }

                Color c = graphic.color;
                if (!Mathf.Approximately(c.a, a))
                {
                    c.a = a;
                    graphic.color = c;
                }
            }

            if (chevronGraphic != null)
            {
                if (chevronGraphic.enabled != on)
                {
                    chevronGraphic.enabled = on;
                }

                Color c = chevronGraphic.color;
                if (!Mathf.Approximately(c.a, a))
                {
                    c.a = a;
                    chevronGraphic.color = c;
                }
            }
        }
    }
}
