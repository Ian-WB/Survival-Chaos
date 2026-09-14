using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Places this object on the enemy orbit circle at startup, keeping its
    /// bearing and height. On the player this puts it in the same lane the
    /// enemies fly in; on the camera, a radiusOffset keeps it trailing behind
    /// by a fixed distance.
    ///
    /// One of them - the Player's - defines the lane rather than sitting on it.
    /// Its radiusOffset moves <see cref="ArenaGeometry.LaneRadius"/>, which is
    /// what the enemies, the boss and its fire, and the pickups all fly to, and
    /// every other SnapToOrbit measures its own offset from that moved lane. So
    /// the camera's offset keeps meaning "this far behind the ship" wherever the
    /// lane is.
    /// </summary>
    public sealed class SnapToOrbit : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Arena axis to orbit. Leave empty to use the object tagged Scenario.")]
        private Transform center;

        [SerializeField]
        [Tooltip("This object's radius offset moves the lane itself, and everything that flies in " +
                 "it follows: enemies, the boss, its rounds and lance, pickups, and every other " +
                 "SnapToOrbit. Only the Player should have this on.")]
        private bool definesLane;

        [SerializeField]
        [Tooltip("On the object that defines the lane: how far the lane sits from the arena's " +
                 "authored radius of 13.72. On anything else: how far outside the lane this " +
                 "object sits, which is what the camera wants.")]
        private float radiusOffset;

        /// <summary>The lane-defining instance, registered while it is enabled.</summary>
        private static SnapToOrbit lane;

        /// <summary>
        /// Whether a search for the lane has already come up empty this session,
        /// so a scene without one costs a single search rather than one per
        /// enemy per frame.
        /// </summary>
        private static bool searchedForLane;

        /// <summary>
        /// How far the lane has been moved from <see cref="ArenaGeometry.OrbitRadius"/>.
        /// Zero when nothing defines it, which is the lane every test and editor
        /// tool is written against.
        /// </summary>
        public static float LaneOffset
        {
            get
            {
                SnapToOrbit definer = FindLane();
                return definer != null ? definer.radiusOffset : 0f;
            }
        }

        /// <summary>The radius this object will be placed at.</summary>
        public float TargetRadius => definesLane
            ? ArenaGeometry.LaneRadius
            : Mathf.Max(0f, ArenaGeometry.LaneRadius + radiusOffset);

        private void Awake()
        {
            // Before Snap, so the lane is known even when this is the first
            // object in the scene to wake.
            if (definesLane)
            {
                Register();
            }

            Snap();
        }

        private void OnEnable()
        {
            if (definesLane)
            {
                Register();
            }
        }

        private void OnDisable()
        {
            if (lane == this)
            {
                lane = null;
                searchedForLane = false;
            }
        }

        /// <summary>
        /// Lets the offset be tuned during play. Changing it in the Inspector puts
        /// the player and the camera back on their circles at once; enemies read
        /// the lane every frame, so they close on the new one by themselves.
        /// Rounds already in flight keep the radius they were fired at.
        /// </summary>
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            foreach (SnapToOrbit snap in FindObjectsByType<SnapToOrbit>(FindObjectsInactive.Exclude))
            {
                snap.Snap();
            }
        }

        [ContextMenu("Snap Now")]
        public void Snap()
        {
            Transform pivot = ResolveCenter();
            if (pivot == null)
            {
                Debug.LogWarning("SnapToOrbit found no centre: assign one, or tag the arena Scenario.", this);
                return;
            }

            transform.position = ArenaGeometry.ProjectOntoOrbit(
                transform.position,
                pivot.position,
                TargetRadius);
        }

        private void Register()
        {
            if (lane != null && lane != this && lane.definesLane && lane.isActiveAndEnabled)
            {
                Debug.LogWarning(
                    $"Both {lane.name} and {name} define the lane. {name} wins; only one should.", this);
            }

            lane = this;
            searchedForLane = false;
        }

        /// <summary>
        /// The registered lane, or a search for one. The search covers objects
        /// asking before the Player has woken, and edit mode, where nothing
        /// registers - there it runs every time so the gizmos follow an Inspector
        /// change straight away.
        /// </summary>
        private static SnapToOrbit FindLane()
        {
            if (lane != null && lane.definesLane)
            {
                return lane;
            }

            if (Application.isPlaying && searchedForLane)
            {
                return null;
            }

            lane = null;

            foreach (SnapToOrbit snap in FindObjectsByType<SnapToOrbit>(FindObjectsInactive.Include))
            {
                if (snap.definesLane)
                {
                    lane = snap;
                    break;
                }
            }

            searchedForLane = true;
            return lane;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lane = null;
            searchedForLane = false;
        }

        private Transform ResolveCenter()
        {
            if (center != null)
            {
                return center;
            }

            GameObject scenario = GameObject.FindWithTag("Scenario");
            return scenario != null ? scenario.transform : null;
        }

        private void OnDrawGizmosSelected()
        {
            Transform pivot = ResolveCenter();
            if (pivot == null)
            {
                return;
            }

            Vector3 origin = pivot.position;
            origin.y = transform.position.y;

            Gizmos.color = Color.cyan;

            const int segments = 64;
            float radius = TargetRadius;
            Vector3 previous = origin + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = origin + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);

                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
