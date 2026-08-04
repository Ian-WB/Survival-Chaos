using UnityEngine;

public class ShootScript : MonoBehaviour
{

    // Resolved on enable - not serialized, so stale prefab references can't shadow it.
    private Transform center;

    // The arena centre never moves, and projectiles are now reused rather than
    // recreated, so without this every bullet would repeat the tag search on
    // every reuse. Cleared between play sessions below; a scene reload destroys
    // the old transform, which reads as null here and re-resolves on its own.
    private static Transform sharedCenter;
    private static bool warnedAboutMissingCenter;

    public float speed;

    /// <summary>
    /// Runs on every spawn, including reuse from the pool. This was Start(),
    /// which only ever runs on an object's first life - a reused bullet would
    /// have kept whatever rotation it died with.
    /// </summary>
    private void OnEnable()
    {
        transform.rotation = Quaternion.Euler(0f, 0f, 90f);

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sharedCenter = null;
        warnedAboutMissingCenter = false;
    }
}
