using System.Text;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Checks the open scene's wave against the height the player can actually
    /// fly at, without entering play mode.
    ///
    /// The same sweep runs from <see cref="WaveDirector"/> at Start, which is the
    /// one that will catch this in practice. This exists for the other half of
    /// the loop: after moving the bounds box, when the question is whether the
    /// wave still fits and pressing play to find out costs a domain reload.
    /// </summary>
    public static class SpawnHeightCheck
    {
        [MenuItem("Survival Chaos/Waves/Check Spawn Heights", priority = 61)]
        public static void Check()
        {
            WaveDirector director = Object.FindAnyObjectByType<WaveDirector>();

            if (director == null || director.Wave == null)
            {
                Debug.LogWarning(
                    "No WaveDirector with a wave assigned in the open scene, so there is "
                    + "nothing to check. Open the Game scene and try again.");
                return;
            }

            ApplyBounds bounds = Object.FindAnyObjectByType<ApplyBounds>();

            if (bounds == null || !bounds.TryGetBand(out float floor, out float ceiling))
            {
                Debug.LogWarning(
                    "No ApplyBounds with a bounds box in the open scene, so there is no "
                    + "player band to check the wave against.");
                return;
            }

            var report = new StringBuilder();
            int outside = director.Wave.DescribeStreamsOutside(floor, ceiling, report);

            string band = floor.ToString("0.###") + " to " + ceiling.ToString("0.###");

            if (outside == 0)
            {
                Debug.Log(
                    director.Wave.name + ": every stream spawns inside the player's band of "
                    + band + ".");
                return;
            }

            Debug.LogWarning(
                director.Wave.name + ": " + outside + " stream(s) spawn outside the player's "
                + "band of " + band + ":\n" + report
                + "Prefabs with EnemyMovement climb back into reach once the player is inside "
                + "their chase radius. Enemy 3 carries ObstacleScript, which has no chase "
                + "branch and never changes height, so those stay where they spawned.",
                director.Wave);
        }
    }
}
