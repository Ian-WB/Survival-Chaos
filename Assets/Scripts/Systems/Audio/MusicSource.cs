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
    ///
    /// Since 30 September 2026 it answers the game as well: it drops and goes
    /// muffled under the pause menu, bends down a little in pitch under Slow
    /// Mo and comes back up as the slowdown runs out, and fades away in the
    /// beat between the killing hit and the Ship Lost card. Each reaction has
    /// its dial below.
    /// </summary>
    [AddComponentMenu("Survival Chaos/Music Source")]
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class MusicSource : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The track. Leave empty to use whatever clip is already on the AudioSource.")]
        private SoundDefinition track;

        [Header("Answering the game")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("The music's level under the pause menu, against normal. The dial for how far it dips.")]
        private float pausedLevel = 0.45f;

        [SerializeField]
        [Range(300f, 22000f)]
        [Tooltip("How muffled the music is under the pause menu: a low-pass cutoff in Hz. Lower is " +
                 "more muffled; 22000 is not at all.")]
        private float pausedCutoff = 1000f;

        [SerializeField]
        [Range(0f, 0.3f)]
        [Tooltip("How far the pitch dips as Slow Mo starts, as a fraction: 0.08 is about a semitone " +
                 "and a half. It comes back up as the slowdown runs out.")]
        private float slowMoPitchDip = 0.08f;

        [SerializeField]
        [Min(0.5f)]
        [Tooltip("How quickly the level, the muffling and the pitch settle, per real second.")]
        private float response = 6f;

        /// <summary>No filtering: past the top of hearing.</summary>
        private const float OpenCutoff = 22000f;

        private AudioSource source;
        private AudioLowPassFilter muffle;
        private float authoredVolume = 1f;
        private float channelAmplitude = 1f;
        private bool captured;

        private float duck = 1f;
        private float cutoff = OpenCutoff;
        private float pitch = 1f;

        /// <summary>Latched by the ending beat: a lost run's music stays gone under the card.</summary>
        private bool lost;

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
                int cursor = -1;
                source.clip = track.PickClip(ref cursor);
                source.outputAudioMixerGroup = track.Output;
                authoredVolume *= track.Volume;
            }

            captured = true;

            // Added here rather than authored, so both scenes' music has one
            // without anyone wiring it. Off until something muffles it.
            muffle = GetComponent<AudioLowPassFilter>();
            if (muffle == null)
            {
                muffle = gameObject.AddComponent<AudioLowPassFilter>();
            }

            muffle.cutoffFrequency = OpenCutoff;
            muffle.enabled = false;

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

            this.channelAmplitude = channelAmplitude;
            source.volume = authoredVolume * channelAmplitude * duck;
        }

        /// <summary>
        /// Eases toward what the game wants of the music now. Real time: the
        /// pause menu stops the game's clock, and that is one of the things
        /// being answered.
        /// </summary>
        private void Update()
        {
            if (source == null)
            {
                return;
            }

            lost |= RunOutcome.Ending;

            float wantLevel = lost ? 0f : PauseMenu.GameIsPaused ? pausedLevel : 1f;
            float wantCutoff = PauseMenu.GameIsPaused && !lost ? pausedCutoff : OpenCutoff;

            PlayerSlowMo slowMo = PlayerSlowMo.Current;
            float slowing = slowMo != null && !lost ? slowMo.SlowRemaining : 0f;
            float wantPitch = 1f - slowMoPitchDip * slowing;

            float k = 1f - Mathf.Exp(-response * Time.unscaledDeltaTime);
            duck = Mathf.Lerp(duck, wantLevel, k);
            pitch = Mathf.Lerp(pitch, wantPitch, k);

            // Eased on a log scale, which is how a sweep of a filter is heard.
            cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(cutoff), Mathf.Log(wantCutoff), k));

            source.volume = authoredVolume * channelAmplitude * duck;
            source.pitch = pitch;

            if (muffle != null)
            {
                bool open = cutoff > OpenCutoff * 0.95f;
                muffle.enabled = !open;
                muffle.cutoffFrequency = cutoff;
            }
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
