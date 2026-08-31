using System.Collections;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Lights every renderer under this object for a moment when it is hit, then
    /// puts them back.
    ///
    /// Enemy already argues that a shot which lands like a shot that missed is
    /// the worst thing a shooter can do, and answers it with a spark at the point
    /// of impact. The spark says a bullet arrived; it does not say what it
    /// arrived at. On the Drone, which takes three, the player is reading three
    /// identical sparks in front of an enemy that never changes - so the feedback
    /// confirms the shot and says nothing about the target. Lighting the hull is
    /// the half that was missing: the thing that was hit is the thing that
    /// reacts.
    ///
    /// Written through a MaterialPropertyBlock rather than a material. Touching
    /// Renderer.material clones the material on first access, which for a pooled
    /// enemy means a clone per instance that nothing ever frees - the trap
    /// PickupLabelBoard documents paying for at the one place it cannot avoid it.
    /// Pickup.Tint takes the same route for the same reason.
    ///
    /// Added in code rather than authored on the prefabs, so nothing has to be
    /// re-wired on five enemy prefabs and a boss for the feature to exist.
    /// ObjectPool.MotionReset reaches for the same get-or-add.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitFlash : MonoBehaviour
    {
        private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");

        /// <summary>
        /// The colour of a hit, and the intensity is as much of the decision as
        /// the hue is.
        ///
        /// _EmissiveColor feeds HDRP's lighting result directly, so this wants to
        /// be worth more than 1 - at 1 the hull is a pale surface rather than
        /// something that reads as a strike. But the scene tone maps through ACES,
        /// which desaturates its highlights, and a red bright enough to clip comes
        /// out the far side white: at 3 the hulls lost their silhouettes entirely
        /// and went to white starbursts, which is the bug this value fixes.
        ///
        /// Measured off the game view against an unlit control rather than judged
        /// by eye. The red channel lands on 226/238/244/247 for values of
        /// 1.35/1.5/1.6/1.8, so it is already flat by 1.5 - while green keeps
        /// climbing, 55/64/83/116, and green is what turns the flash orange. Above
        /// 1.5 buys no brightness and pays for it in hue. The lava this reads
        /// against is itself orange, so hue is the property worth protecting.
        ///
        /// The green and blue are not doing much at 0.02 and are here to keep the
        /// flash off a mathematically pure primary. A deliberate blue lift to bend
        /// the hue back under ACES was tried and dropped: it moved the result by
        /// 0.8 degrees and cost saturation.
        /// </summary>
        [SerializeField]
        [ColorUsage(showAlpha: false, hdr: true)]
        [Tooltip("Emissive colour held during the flash. HDR, but not unboundedly so: " +
                 "ACES turns anything bright enough to clip white, which is what this " +
                 "value is set just under.")]
        private Color color = new Color(1.5f, 0.02f, 0.02f);

        [SerializeField]
        [Range(0.01f, 0.5f)]
        [Tooltip("How long the flash is held, in seconds. Timed rather than counted in " +
                 "frames: two frames is a third as long on a 144Hz display as it is at 48, " +
                 "which would make the feedback quietly weaker on the better machine.")]
        private float seconds = 0.06f;

        /// <summary>
        /// Only the renderers that actually have an emissive channel.
        ///
        /// Every enemy hull in the project is HDRP/Lit, which has one. Trails,
        /// particles and anything else that finds its way under a prefab
        /// generally do not, and writing the property into a block for a shader
        /// with nowhere to put it is a silent no-op that still costs the call.
        /// </summary>
        private Renderer[] targets;

        private Color[] resting;
        private MaterialPropertyBlock block;
        private Coroutine running;

        /// <summary>The flash on an object, adding one the first time it is asked for.</summary>
        public static HitFlash On(GameObject host)
        {
            if (host == null)
            {
                return null;
            }

            HitFlash existing = host.GetComponent<HitFlash>();
            return existing != null ? existing : host.AddComponent<HitFlash>();
        }

        private void Awake()
        {
            var found = new System.Collections.Generic.List<Renderer>();
            var rest = new System.Collections.Generic.List<Color>();

            foreach (Renderer candidate in GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                // Renderers under a nested flash belong to it, not to this one.
                // The boss is the case: its emplacements carry their own, and a
                // hull flash that also lit them would make hitting the armour
                // look exactly like hitting the one part of the boss that can
                // actually be hurt - which is the single distinction the whole
                // first act of that fight rests on.
                if (candidate.GetComponentInParent<HitFlash>(includeInactive: true) != this)
                {
                    continue;
                }

                Material material = candidate.sharedMaterial;

                if (material == null || !material.HasProperty(EmissiveColor))
                {
                    continue;
                }

                found.Add(candidate);

                // What to put back afterwards, read from the shared material
                // rather than remembered from before the flash - a second hit
                // landing inside the first flash would otherwise record the lit
                // colour as the resting one and leave the enemy glowing forever.
                rest.Add(material.GetColor(EmissiveColor));
            }

            targets = found.ToArray();
            resting = rest.ToArray();
        }

        /// <summary>
        /// Puts the hull back at the start of every life.
        ///
        /// Despawning mid-flash stops the coroutine where it stands, and what it
        /// had already written lives on the Renderer rather than on the
        /// coroutine. Without this a pooled enemy killed while lit would come
        /// back still lit, and stay that way until something hit it again.
        /// </summary>
        private void OnEnable()
        {
            running = null;
            Paint(lit: false);
        }

        /// <summary>Flashes once, restarting the hold if one is already running.</summary>
        public void Strike()
        {
            if (targets == null || targets.Length == 0 || !isActiveAndEnabled)
            {
                return;
            }

            if (running != null)
            {
                StopCoroutine(running);
            }

            running = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Paint(lit: true);

            // Unscaled: both endings stop time outright, and a flash caught
            // half-finished by the death screen would sit lit behind it for as
            // long as the screen was up.
            yield return new WaitForSecondsRealtime(seconds);

            Paint(lit: false);
            running = null;
        }

        private void Paint(bool lit)
        {
            if (targets == null)
            {
                return;
            }

            block ??= new MaterialPropertyBlock();

            for (int i = 0; i < targets.Length; i++)
            {
                Renderer target = targets[i];

                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(block);
                block.SetColor(EmissiveColor, lit ? color : resting[i]);
                target.SetPropertyBlock(block);
            }
        }
    }
}
