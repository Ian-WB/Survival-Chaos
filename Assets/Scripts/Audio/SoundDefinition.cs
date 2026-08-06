using UnityEngine;
using UnityEngine.Audio;

namespace SurvivalChaos
{
    /// <summary>
    /// One sound the game can make, authored as an asset rather than wired to a
    /// particular AudioSource — the same shape as SkillDefinition and
    /// EnemyDefinition, so tuning a sound never means editing a prefab.
    ///
    /// The variation and throttling fields exist because of what this game
    /// actually does. It fires hundreds of projectiles a second: the same clip at
    /// the same pitch that often stops reading as a weapon and starts reading as
    /// a buzz, and every copy that starts in the same frame adds amplitude until
    /// the mix clips.
    /// </summary>
    [CreateAssetMenu(menuName = "Survival Chaos/Sound", fileName = "NewSound")]
    public sealed class SoundDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("One or more clips. When there is more than one, a different one is chosen each " +
                 "time, which is the cheapest way to stop a repeated sound sounding mechanical.")]
        private AudioClip[] clips = new AudioClip[0];

        [SerializeField]
        [Tooltip("Which volume slider this answers to.")]
        private AudioChannel channel = AudioChannel.Sfx;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Level for this sound before any channel volume is applied.")]
        private float volume = 1f;

        [SerializeField]
        [Tooltip("Pitch is picked from this range each time. A little spread does most of the work " +
                 "of making a repeated sound tolerable; 1 to 1 disables it.")]
        private Vector2 pitchRange = new Vector2(0.94f, 1.06f);

        [SerializeField]
        [Tooltip("Seconds before this sound may start again. Thirty bullets leaving in one frame " +
                 "should be one shot, not thirty stacked on top of each other.")]
        private float minRetrigger = 0.05f;

        [SerializeField]
        [Tooltip("How many copies may be alive at once. Beyond this, new requests are dropped " +
                 "rather than queued - a late sound is worse than a missing one.")]
        private int maxVoices = 4;

        [SerializeField]
        [Tooltip("0 is plain stereo. 1 positions the sound in the world, which costs more and only " +
                 "matters for things the player can locate.")]
        [Range(0f, 1f)]
        private float spatialBlend;

        [SerializeField]
        [Tooltip("Optional. Leave empty and the sound plays straight to the listener at the level " +
                 "its channel dictates. Assign a group once a mixer exists and it routes there " +
                 "instead, which is what effects and ducking need.")]
        private AudioMixerGroup output;

        public AudioChannel Channel => channel;

        public AudioMixerGroup Output => output;

        public float Volume => volume;

        public float MinRetrigger => Mathf.Max(0f, minRetrigger);

        public int MaxVoices => Mathf.Max(1, maxVoices);

        public float SpatialBlend => spatialBlend;

        public bool HasClips => clips != null && clips.Length > 0;

        /// <summary>A clip to play, avoiding an immediate repeat where possible.</summary>
        public AudioClip PickClip(ref int lastIndex)
        {
            if (!HasClips)
            {
                return null;
            }

            if (clips.Length == 1)
            {
                return clips[0];
            }

            // Choose from the others, so the same clip never lands twice running.
            // Straight randomness produces audible repeats surprisingly often.
            int index = Random.Range(0, clips.Length - 1);
            if (index >= lastIndex)
            {
                index++;
            }

            lastIndex = index;
            return clips[index];
        }

        public float PickPitch()
        {
            return Random.Range(pitchRange.x, pitchRange.y);
        }
    }
}
