using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Plays a scene's music and puts it under the Music slider.
    ///
    /// The track can be given either as a Sound asset or straight on the
    /// AudioSource. The asset route exists because that is how the rest of the
    /// project is authored, and because a track is content — it should be
    /// swappable without opening a scene.
    ///
    /// Music deliberately does not go through <see cref="AudioDirector"/>'s voice
    /// pool. Those voices are short, recycled, and reclaimed the moment a clip
    /// ends; a looping track would either hold one forever or be cut off mid-bar
    /// when the pool ran dry during a firefight. It gets its own AudioSource.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Music Source")]
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class MusicSource : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The track. Leave empty to use whatever clip is already on the AudioSource.")]
        private SoundDefinition track;

        private AudioSource source;
        private float authoredVolume = 1f;
        private bool captured;

        private void Awake()
        {
            source = GetComponent<AudioSource>();

            // The AudioSource's own level is the balance against the rest of the
            // game - 0.15 here, chosen by the team. The asset's volume scales it
            // rather than replacing it, so pointing this at an asset cannot
            // suddenly make the music six times louder.
            authoredVolume = source.volume;

            if (track != null && track.HasClips)
            {
                int cursor = 0;
                source.clip = track.PickClip(ref cursor);
                source.outputAudioMixerGroup = track.Output;
                authoredVolume *= track.Volume;
            }

            captured = true;

            if (source.clip == null)
            {
                // The exact failure this component existed to have, and it used to
                // present as silence with nothing to look at.
                Debug.LogWarning(
                    "MusicSource on '" + name + "' has no track: neither the Track field nor the " +
                    "AudioSource's Audio Clip is set, so this scene will be silent.", this);
                return;
            }

            // Play On Awake is evaluated before this assigns the clip, so with an
            // asset-driven track it has already come and gone by now.
            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private void OnEnable()
        {
            if (AudioDirector.Instance != null)
            {
                AudioDirector.Instance.Register(this);
            }
        }

        private void OnDisable()
        {
            if (AudioDirector.Instance != null)
            {
                AudioDirector.Instance.Unregister(this);
            }
        }

        /// <summary>Applies the Music channel's level on top of the authored balance.</summary>
        internal void ApplyLevel(float channelAmplitude)
        {
            if (source == null)
            {
                return;
            }

            // Registration can arrive before Awake if the director is enabled
            // first; capturing here too stops an already-scaled level being read
            // back as the authored one.
            if (!captured)
            {
                authoredVolume = source.volume;
                captured = true;
            }

            source.volume = authoredVolume * channelAmplitude;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (track != null && track.Channel != AudioChannel.Music)
            {
                Debug.LogWarning(
                    "'" + track.name + "' is set to the " + track.Channel + " channel, but a " +
                    "MusicSource always follows the Music slider. Set the asset's channel to Music " +
                    "so the two agree.", this);
            }
        }
#endif
    }
}
