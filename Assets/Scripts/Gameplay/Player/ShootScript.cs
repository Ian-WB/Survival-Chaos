using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    public class ShootScript : MonoBehaviour
    {

        /// <summary>
        /// Every projectile currently in flight, in no particular order.
        ///
        /// Exists so BulletLightPool can find the bullets nearest the camera without
        /// calling GameObject.FindGameObjectsWithTag, which allocates a fresh array
        /// on every call - roughly 200 entries, every frame, forever. That was the
        /// largest steady garbage source left in the game, and garbage is what shows
        /// up as frame time spikes against an otherwise flat graph.
        ///
        /// Projectiles add themselves as they appear and remove themselves as they
        /// die, so the list needs no sweeping. It can still hold a destroyed entry if
        /// something bypasses OnDisable - a scene unload - so readers check.
        ///
        /// Holds the component rather than the transform, so a reader can see which
        /// volley an entry belongs to without a GetComponent per bullet per frame.
        /// <see cref="Body"/> keeps the transform a lookup away for the callers that
        /// only want the position.
        /// </summary>
        private static readonly List<ShootScript> live = new List<ShootScript>();

        public static IReadOnlyList<ShootScript> Live => live;

        /// <summary>
        /// This projectile's transform, resolved once.
        ///
        /// The registry held transforms directly before it held components, for the
        /// reason above: this is read for every live projectile every frame, and the
        /// point of the list is that reading it costs nothing.
        /// </summary>
        public Transform Body { get; private set; }

        /// <summary>
        /// Which volley fired this projectile. Equal for every bullet of one shot,
        /// different for every shot.
        ///
        /// The frame number, because a volley *is* a frame: Player.FireLine spawns
        /// the whole pattern in a single loop, so bullets that share a shot share a
        /// frame and nothing else can. Two volleys in one frame would merge, which
        /// needs a fire interval below a frame to happen and costs a redundant
        /// shadow if it ever does.
        ///
        /// Stamped in OnEnable rather than Awake so a projectile taken back out of
        /// the pool belongs to the volley that fired it, not the one that first
        /// created it.
        /// </summary>
        public int Volley { get; private set; }

        // Resolved on enable - not serialized, so stale prefab references can't shadow it.
        private Transform center;

        // The arena centre never moves, and projectiles are now reused rather than
        // recreated, so without this every bullet would repeat the tag search on
        // every reuse. Cleared between play sessions below; a scene reload destroys
        // the old transform, which reads as null here and re-resolves on its own.
        private static Transform sharedCenter;
        private static bool warnedAboutMissingCenter;

        [SerializeField]
        private float speed;

        /// <summary>
        /// Runs on every spawn, including reuse from the pool. This was Start(),
        /// which only ever runs on an object's first life - a reused bullet would
        /// have kept whatever rotation it died with.
        /// </summary>
        private void OnEnable()
        {
            if (Body == null)
            {
                Body = transform;
            }

            Body.rotation = Quaternion.Euler(0f, 0f, 90f);

            Volley = Time.frameCount;
            live.Add(this);

            if (sharedCenter == null)
            {
                GameObject scenario = GameObject.FindWithTag("Scenario");

                if (scenario == null)
                {
                    if (!warnedAboutMissingCenter)
                    {
                        warnedAboutMissingCenter = true;
                        Debug.LogWarning(
                            "ShootScript found nothing tagged 'Scenario', so projectiles have no " +
                            "centre to orbit and will sit still.", this);
                    }

                    return;
                }

                sharedCenter = scenario.transform;
            }

            center = sharedCenter;
        }

        // Update is called once per frame
        void Update()
        {
            if (center == null)
            {
                return;
            }

            Vector3 pos =  center.position;
            pos.y = transform.position.y;
            transform.RotateAround(pos, Vector3.up, Time.deltaTime * speed);
            transform.LookAt(pos);
        }

        /// <summary>
        /// Pairs with the registration in OnEnable. Runs on every route out - going
        /// back to the pool, being destroyed, the scene unloading - so the list
        /// cannot accumulate entries across a run.
        /// </summary>
        private void OnDisable()
        {
            live.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sharedCenter = null;
            warnedAboutMissingCenter = false;
            live.Clear();
        }
    }
}
