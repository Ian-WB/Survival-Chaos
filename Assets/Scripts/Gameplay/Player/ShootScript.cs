using System.Collections.Generic;
using UnityEngine;

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
    /// </summary>
    private static readonly List<Transform> live = new List<Transform>();

    public static IReadOnlyList<Transform> Live => live;

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
        transform.rotation = Quaternion.Euler(0f, 0f, 90f);

        live.Add(transform);

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
        live.Remove(transform);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sharedCenter = null;
        warnedAboutMissingCenter = false;
        live.Clear();
    }
}
