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
    ///
    /// And the Leviathan has its own track since then. When the boss arrives,
    /// the run's track hands over to it, and it loops over the part of itself
    /// set below, so its quiet intro and its fade-out are heard once at most.
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

        [SerializeField]
        [Min(0f)]
        [Tooltip("Real seconds the track takes to fade in once the scene is on screen, after the " +
                 "loading screen has cleared. 0 starts it at full.")]
        private float fadeInSeconds = 1f;

        [Header("The Leviathan")]
        [SerializeField]
        [Tooltip("Played from the moment the Leviathan arrives, taking over from the track above. " +
                 "Leave empty in a scene without it, like the menu.")]
        private SoundDefinition bossTrack;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds into the Leviathan's track to start from, and where each loop comes back " +
                 "to. Put it on a bar line: 17.313 is bar 10 of Neon Hyperdrive, where its build starts.")]
        private float bossLoopStart = 17.313f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds into the Leviathan's track where it goes back to Boss Loop Start. 0 plays " +
                 "the whole clip. 111.599 is bar 65 of Neon Hyperdrive, just before its outro.")]
        private float bossLoopEnd = 111.599f;

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Real seconds the run's track takes to hand over to the Leviathan's.")]
        private float bossCrossfade = 3f;

        /// <summary>No filtering: past the top of hearing.</summary>
        private const float OpenCutoff = 22000f;

        private AudioSource source;
        private AudioLowPassFilter muffle;
        private float authoredVolume = 1f;

        /// <summary>
        /// The AudioSource's own level as the scene sets it, before the track's
        /// asset scales it. It places music against everything else, so the
        /// Leviathan's track starts from it too; the two assets' volumes only
        /// balance the tracks against each other.
        /// </summary>
        private float sceneLevel = 1f;

        private AudioSource bossSource;
        private AudioLowPassFilter bossMuffle;
        private float bossAuthoredVolume;

        /// <summary>Latched once the Leviathan is seen, so its death does not bring the run's track back.</summary>
        private bool bossCued;

        /// <summary>How far the handover has got: 0 is the run's track, 1 the Leviathan's.</summary>
        private float bossMix;

        /// <summary>The fade in as the scene arrives, 0 to 1.</summary>
        private float arrival;

        /// <summary>The fade out as the scene is left, 1 to 0. See <see cref="FadeAllOut"/>.</summary>
        private float departure = 1f;
        private float departureSeconds;
        private bool departing;
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
            sceneLevel = source.volume;

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

            PrepareBossTrack();

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
            ApplyVolumes();
        }

        /// <summary>
        /// Both tracks' levels: each one's own, the Music slider, the pause dip
        /// and the handover between them.
        /// </summary>
        private void ApplyVolumes()
        {
            MusicLoop.Crossfade(bossMix, out float runGain, out float bossGain);
            float fades = arrival * departure;
            source.volume = authoredVolume * channelAmplitude * duck * fades * runGain;

            if (bossSource != null)
            {
                bossSource.volume = bossAuthoredVolume * channelAmplitude * duck * fades * bossGain;
            }
        }

        /// <summary>
        /// Gives the Leviathan's track a source of its own, silent until the
        /// boss arrives.
        ///
        /// On a child object, not beside the run's source, so it can have its
        /// own low-pass filter: a filter works on the object it is on, and the
        /// pause dip muffles both tracks, but only one of them is ever heard.
        /// Made here rather than authored, like the filter, so the scene only
        /// needs the asset.
        /// </summary>
        private void PrepareBossTrack()
        {
            if (bossTrack == null || !bossTrack.HasClips)
            {
                return;
            }

            int cursor = -1;
            AudioClip clip = bossTrack.PickClip(ref cursor);
            if (clip == null)
            {
                return;
            }

            var holder = new GameObject("Leviathan Music");
            holder.transform.SetParent(transform, worldPositionStays: false);

            bossSource = holder.AddComponent<AudioSource>();
            bossSource.playOnAwake = false;
            bossSource.clip = clip;
            // Looping whole is the fallback; the loop points below take over
            // before the clip ends whenever they are set.
            bossSource.loop = true;
            bossSource.spatialBlend = source.spatialBlend;
            bossSource.priority = source.priority;
            bossSource.outputAudioMixerGroup = bossTrack.Output != null ? bossTrack.Output : source.outputAudioMixerGroup;
            bossSource.volume = 0f;
            bossAuthoredVolume = sceneLevel * bossTrack.Volume;

            bossMuffle = holder.AddComponent<AudioLowPassFilter>();
            bossMuffle.cutoffFrequency = OpenCutoff;
            bossMuffle.enabled = false;
        }

        /// <summary>
        /// Starts the Leviathan's track at its loop start. Played first and
        /// placed after: Play on a clip that is not yet playing starts it from
        /// the top, and the source is silent for the moment in between.
        /// </summary>
        private void CueBoss()
        {
            bossCued = true;
            bossSource.Play();
            bossSource.timeSamples = MusicLoop.ToSamples(bossLoopStart, bossSource.clip.frequency);
        }

        /// <summary>Sends the Leviathan's track back to its loop start once it reaches the loop end.</summary>
        private void KeepBossInLoop()
        {
            if (!bossSource.isPlaying || bossLoopEnd <= 0f)
            {
                return;
            }

            int frequency = bossSource.clip.frequency;
            int wrapped = MusicLoop.Wrap(bossSource.timeSamples,
                MusicLoop.ToSamples(bossLoopStart, frequency),
                MusicLoop.ToSamples(bossLoopEnd, frequency));

            if (wrapped >= 0)
            {
                bossSource.timeSamples = wrapped;
            }
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

            // A won run's beat keeps its music: only a lost one fades out.
            lost |= RunOutcome.Ending && !RunOutcome.Won;

            // Read every frame rather than told: the arrival, the debug menu's
            // Skip to boss and anything else that brings the boss all set
            // Active. The pool's warm-up sets and clears it inside one call, so
            // a frame never sees a boss that is not there.
            if (bossSource != null && !bossCued && !lost && BossEmitter.Active != null)
            {
                CueBoss();
            }

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

            if (bossCued)
            {
                bossMix = Mathf.MoveTowards(bossMix, 1f, Time.unscaledDeltaTime / bossCrossfade);
            }

            // In once the loading screen has gone, so the first thing heard of
            // a scene is not its slow first frames; out as it is left.
            if (!LoadingScreen.Busy)
            {
                arrival = fadeInSeconds > 0f
                    ? Mathf.MoveTowards(arrival, 1f, Time.unscaledDeltaTime / fadeInSeconds)
                    : 1f;
            }

            if (departing)
            {
                departure = departureSeconds > 0f
                    ? Mathf.MoveTowards(departure, 0f, Time.unscaledDeltaTime / departureSeconds)
                    : 0f;
            }

            ApplyVolumes();
            source.pitch = pitch;

            bool open = cutoff > OpenCutoff * 0.95f;
            Muffle(muffle, open);

            if (bossSource != null)
            {
                bossSource.pitch = pitch;
                Muffle(bossMuffle, open);
                KeepBossInLoop();

                // Handed over for good: nothing needs the run's stream any more.
                if (bossMix >= 1f && source.isPlaying)
                {
                    source.Stop();
                }
            }
        }

        /// <summary>
        /// Fades every track playing out over <paramref name="seconds"/> of real
        /// time. Called by the loading screen as it comes up: until 30 September
        /// 2026 the old scene's music stopped dead partway through a load and
        /// the new one's started at full volume on the same frame.
        /// </summary>
        public static void FadeAllOut(float seconds)
        {
            foreach (MusicSource music in FindObjectsByType<MusicSource>(FindObjectsInactive.Exclude))
            {
                music.departing = true;
                music.departureSeconds = seconds;
            }
        }

        private void Muffle(AudioLowPassFilter filter, bool open)
        {
            if (filter != null)
            {
                filter.enabled = !open;
                filter.cutoffFrequency = cutoff;
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

            if (bossTrack != null && bossTrack.Channel != AudioChannel.Music)
            {
                Debug.LogWarning(
                    "'" + bossTrack.name + "' is set to the " + bossTrack.Channel + " channel, but the " +
                    "Leviathan's track follows the Music slider. Set the asset's channel to Music.", this);
            }

            if (bossLoopEnd > 0f && bossLoopEnd <= bossLoopStart)
            {
                Debug.LogWarning(
                    "Boss Loop End is not after Boss Loop Start, so the Leviathan's track loops whole " +
                    "instead, intro and outro included.", this);
            }
        }
#endif
    }
}
