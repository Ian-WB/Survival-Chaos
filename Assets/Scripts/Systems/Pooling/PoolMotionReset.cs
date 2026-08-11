using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Restores motion vectors on instances the pool suppressed them for, one
    /// frame after they reappeared.
    ///
    /// One component doing this for everything, rather than a LateUpdate on each
    /// pooled object. At peak there are around 200 projectiles alive; 200 empty
    /// per-frame callbacks to undo a flag is exactly the kind of cost the pool
    /// exists to avoid, and it would be paid every frame rather than only on the
    /// frames something spawned.
    ///
    /// Lives on the pool's own root, created on demand, so nothing has to be
    /// placed in a scene for it to work.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class PoolMotionReset : MonoBehaviour
    {
        private readonly List<PooledInstance> pending = new List<PooledInstance>();
        private readonly List<PooledInstance> restoring = new List<PooledInstance>();

        internal void Register(PooledInstance instance)
        {
            if (instance != null && !pending.Contains(instance))
            {
                pending.Add(instance);
            }
        }

        /// <summary>
        /// Two lists rather than one: an instance registered this frame must be
        /// rendered once before its motion vectors mean anything, so it is only
        /// restored on the frame after. Swapping the lists gives that one-frame
        /// delay without storing a frame number per instance.
        /// </summary>
        private void LateUpdate()
        {
            for (int i = 0; i < restoring.Count; i++)
            {
                if (restoring[i] != null)
                {
                    restoring[i].RestoreMotion();
                }
            }

            restoring.Clear();

            // Swap, so this frame's registrations are restored on the next one.
            restoring.AddRange(pending);
            pending.Clear();
        }
    }
}
