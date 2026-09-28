using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalChaos
{
    /// <summary>
    /// The one place sound is played and volume is decided.
    ///
    /// It creates itself and survives scene loads, so a level set in the title
    /// screen is still in force in the game — the previous arrangement, a slider
    /// per scene wired to nothing, could not have carried a setting anywhere.
    ///
    /// Playback goes through a fixed pool of AudioSources with per-sound
    /// throttling. That is not tidiness: the player fires up to six projectiles
    /// per volley and the boss twenty-nine, so an AudioSource per shot would mean
    /// hundreds of them, and every copy starting in the same frame adds amplitude
    /// until the mix clips.
    ///
    /// Gameplay sounds run on game time: they pause with the pause menu and
    /// slow down with Slow Mo, as the attacks they describe do. Menu sounds and
    /// music do neither. See <see cref="GameSoundRate"/>.
    ///
    /// Define SURVIVAL_CHAOS_NO_AUDIO_DIRECTOR to leave it out of a build.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        /// <summary>
        /// Simultaneous sounds. Well past what the game asks for, and far below
        /// what one-source-per-shot would need.
        /// </summary>
        private const int VoiceCount = 24;

        private const string PrefsPrefix = "SurvivalChaos.Audio.";

        /// <summary>The highest pitch an AudioSource takes.</summary>
        private const float MaxPitch = 3f;

        public static AudioDirector Instance { get; private set; }

        /// <summary>Raised when any level changes, so controls can follow along.</summary>
        public static event Action LevelsChanged;

        private readonly Dictionary<AudioChannel, float> levels = new Dictionary<AudioChannel, float>();
        private readonly Dictionary<SoundDefinition, float> lastStarted = new Dictionary<SoundDefinition, float>();
        private readonly Dictionary<SoundDefinition, int> clipCursor = new Dictionary<SoundDefinition, int>();
        private readonly List<MusicSource> music = new List<MusicSource>();

        private AudioSource[] voices;
        private SoundDefinition[] voiceOwner;
        private float[] voiceFreeAt;

        /// <summary>The pitch each voice was started at, before game time is applied.</summary>
        private float[] voicePitch;

        /// <summary>How much of its clip each voice has still to play, in the clip's own seconds.</summary>
        private float[] voiceRemaining;

        /// <summary>How fast each voice has been playing its clip, 1 being as recorded and 0 paused.</summary>
        private float[] voiceRate;

#if !SURVIVAL_CHAOS_NO_AUDIO_DIRECTOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            // Before the scene loads, so anything that plays a sound in Awake or
            // Start finds it already there.
            GameObject host = new GameObject("Audio Director");
            host.AddComponent<AudioDirector>();
            DontDestroyOnLoad(host);
        }
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            LoadLevels();
            BuildVoices();
            ApplyLevels();

            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Drops every subscriber to the static event.
        ///
        /// Controls do unsubscribe in OnDisable, so nothing leaks today - but the
        /// event is static, static state outlives play mode with domain reload
        /// disabled, and every other static in the project is reset this way.
        /// Leaving one exception is how the next subscriber that forgets finds
        /// out the hard way.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            LevelsChanged = null;
            Instance = null;
        }

        private void BuildVoices()
        {
            voices = new AudioSource[VoiceCount];
            voiceOwner = new SoundDefinition[VoiceCount];
            voiceFreeAt = new float[VoiceCount];
            voicePitch = new float[VoiceCount];
            voiceRemaining = new float[VoiceCount];
            voiceRate = new float[VoiceCount];

            for (int i = 0; i < VoiceCount; i++)
            {
                GameObject host = new GameObject("Voice " + (i + 1));
                host.transform.SetParent(transform, false);

                AudioSource source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                // Unity's own rolloff would silence sounds spawned far from the
                // listener; anything positional sets this per sound instead.
                source.spatialBlend = 0f;
                // Rolloff is measured in world units, so it moved with the arena
                // twice: up when the world went to 10x, and back down when it
                // returned. These are Unity's own defaults again, and they are
                // still written out rather than left implicit - the next person to
                // rescale the world needs to find them here rather than discover
                // by ear that every positional sound is attenuating wrongly.
                // Distances are from the ship, which carries the listener since
                // 24 September 2026, not from the camera 10 behind it.
                source.minDistance = 1f;
                source.maxDistance = 500f;
                voices[i] = source;
            }
        }

        // ---------- levels ----------

        private void LoadLevels()
        {
            foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
            {
                levels[channel] = PlayerPrefs.GetFloat(PrefsPrefix + channel, DefaultFor(channel));
            }
        }

        /// <summary>
        /// Every channel starts open. Balance belongs in the sources, so a slider
        /// at full should sound exactly like the game does today; sliders
        /// attenuate from there and are not a place to set balance.
        ///
        /// This used to say the music AudioSource was authored at 0.15. It was
        /// not - both scenes had it at 1, and had for long enough that the note
        /// was being read as fact. Music was therefore the loudest thing in the
        /// game by about nine decibels, with every effect fighting underneath it,
        /// and no amount of work on the effects' own levels could have fixed
        /// that. It is 0.12 in Game and 0.162 in Menu now.
        ///
        /// The lesson is the note, not the number: this comment described a value
        /// living in two scene files that nothing here can see. If it drifts
        /// again, measure it - do not trust this sentence either.
        /// </summary>
        private static float DefaultFor(AudioChannel channel)
        {
            return 1f;
        }

        public float GetLevel(AudioChannel channel)
        {
            return levels.TryGetValue(channel, out float value) ? value : DefaultFor(channel);
        }

        public void SetLevel(AudioChannel channel, float value)
        {
            value = Mathf.Clamp01(value);

            if (levels.TryGetValue(channel, out float existing) && Mathf.Approximately(existing, value))
            {
                return;
            }

            levels[channel] = value;
            PlayerPrefs.SetFloat(PrefsPrefix + channel, value);

            // PlayerPrefs only reaches disk on a graceful quit, and this game can
            // crash. Debounced rather than saved here, because a slider drag
            // writes on every frame it moves.
            SettingsStore.MarkDirty();

            ApplyLevels();
            LevelsChanged?.Invoke();
        }

        private void ApplyLevels()
        {
            // Master rides on the listener rather than on each source, so it also
            // covers any AudioSource added later that never registers here.
            AudioListener.volume = AudioLevels.ToAmplitude(GetLevel(AudioChannel.Master));

            for (int i = music.Count - 1; i >= 0; i--)
            {
                if (music[i] == null)
                {
                    music.RemoveAt(i);
                    continue;
                }

                music[i].ApplyLevel(AudioLevels.ToAmplitude(GetLevel(AudioChannel.Music)));
            }

            // Voices still sounding take the new level too. A voice's volume is
            // set when it starts, so moving the Effects slider under the pause
            // menu left a long sound - the lance's wind-up - at the old level
            // until it ended (scan of 28 September 2026).
            if (voices == null)
            {
                return;
            }

            float now = Time.unscaledTime;

            for (int i = 0; i < voices.Length; i++)
            {
                SoundDefinition owner = voiceOwner[i];
                if (owner != null && voiceFreeAt[i] > now && voices[i] != null)
                {
                    voices[i].volume = owner.Volume * AudioLevels.ToAmplitude(GetLevel(owner.Channel));
                }
            }
        }

        // ---------- music ----------

        internal void Register(MusicSource source)
        {
            if (source != null && !music.Contains(source))
            {
                music.Add(source);
                source.ApplyLevel(AudioLevels.ToAmplitude(GetLevel(AudioChannel.Music)));
            }
        }

        internal void Unregister(MusicSource source)
        {
            music.Remove(source);
        }

        // ---------- playback ----------

        /// <summary>Plays a sound without a position. Safe to call before anything exists.</summary>
        public static void Play(SoundDefinition sound)
        {
            if (Instance != null)
            {
                Instance.PlayInternal(sound, null);
            }
        }

        /// <summary>Plays a sound at a world position, for sounds authored as positional.</summary>
        public static void PlayAt(SoundDefinition sound, Vector3 position)
        {
            if (Instance != null)
            {
                Instance.PlayInternal(sound, position);
            }
        }

        private void PlayInternal(SoundDefinition sound, Vector3? position)
        {
            if (sound == null || !sound.HasClips)
            {
                return;
            }

            // Unscaled throughout: menus stop time, and a menu that goes silent
            // when opened would seem broken.
            float now = Time.unscaledTime;

            if (lastStarted.TryGetValue(sound, out float last) && now - last < sound.MinRetrigger)
            {
                return;
            }

            if (CountActive(sound, now) >= sound.MaxVoices)
            {
                return;
            }

            int slot = FindFreeVoice(now);
            if (slot < 0)
            {
                // Everything is busy. Dropping beats interrupting something
                // already audible, and beats queueing a sound that would arrive
                // after the thing it describes.
                return;
            }

            int cursor = clipCursor.TryGetValue(sound, out int stored) ? stored : -1;
            AudioClip clip = sound.PickClip(ref cursor);
            clipCursor[sound] = cursor;

            if (clip == null)
            {
                return;
            }

            float pitch = sound.PickPitch();
            float rate = RateNow(sound, pitch);

            AudioSource source = voices[slot];
            source.clip = clip;
            source.outputAudioMixerGroup = sound.Output;
            source.pitch = rate > 0f ? rate : pitch;
            source.spatialBlend = position.HasValue ? sound.SpatialBlend : 0f;
            source.transform.position = position ?? Vector3.zero;
            source.volume = sound.Volume * AudioLevels.ToAmplitude(GetLevel(sound.Channel));
            source.Play();

            // Started under the pause menu. Nothing in the game should, but a
            // gameplay sound that did would otherwise play through the pause.
            if (rate <= 0f)
            {
                source.Pause();
            }

            voiceOwner[slot] = sound;
            voicePitch[slot] = pitch;
            voiceRemaining[slot] = clip.length;
            voiceRate[slot] = rate;
            voiceFreeAt[slot] = FreeAt(slot, now);
            lastStarted[sound] = now;
        }

        // ---------- game time ----------

        /// <summary>
        /// How fast gameplay sounds play, against how they were recorded.
        ///
        /// The game's own speed, so a sound lasts as long as the thing it is
        /// about. The boss's lance charge is the case that matters: it is the
        /// warning, it is exactly as long as the wind-up, and the wind-up runs on
        /// game time. At full speed under Slow Mo's 0.4 it finished at 40% of the
        /// charge, so the warning went quiet with more than half the wind-up
        /// still to come, and under the pause menu it carried on and ended
        /// before the game resumed (audit of 26 September 2026). Slowed with the
        /// game, a sound also drops in pitch, the usual sound of slow motion.
        ///
        /// Stopped under the pause menu. Not when the run has ended, though that
        /// stops time as well: the last explosion is what the death and victory
        /// screens open over, and freezing it would cut it off. It plays out at
        /// its own speed.
        /// </summary>
        public static float GameSoundRate(bool paused, bool runEnded, float timeScale)
        {
            if (runEnded)
            {
                return 1f;
            }

            if (paused)
            {
                return 0f;
            }

            return timeScale > 0f ? timeScale : 1f;
        }

        private static bool FollowsGameTime(SoundDefinition sound)
        {
            return sound != null && sound.Channel == AudioChannel.Sfx;
        }

        /// <summary>The pitch a voice playing <paramref name="sound"/> should have now, 0 when paused.</summary>
        private static float RateNow(SoundDefinition sound, float pitch)
        {
            float game = FollowsGameTime(sound)
                ? GameSoundRate(PauseMenu.GameIsPaused, RunOutcome.RunEnded, Time.timeScale)
                : 1f;

            return Mathf.Min(MaxPitch, Mathf.Abs(pitch) * game);
        }

        /// <summary>
        /// When a voice will be done, at the rate it is playing. Never while it
        /// is paused: a paused voice still holds the rest of its clip.
        /// </summary>
        private float FreeAt(int slot, float now)
        {
            if (voiceRemaining[slot] <= 0f)
            {
                return now;
            }

            return voiceRate[slot] > 0f ? now + voiceRemaining[slot] / voiceRate[slot] : float.MaxValue;
        }

        /// <summary>
        /// Brings each gameplay voice onto the game's current speed, and keeps
        /// every voice's account of how much it has left to play. The pool's
        /// idea of which voices are free is worked out from that, so a voice
        /// slowed or paused is not handed to another sound while it is still
        /// playing.
        /// </summary>
        private void Update()
        {
            if (voices == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            float elapsed = Time.unscaledDeltaTime;

            for (int i = 0; i < voices.Length; i++)
            {
                if (voiceOwner[i] == null || voiceRemaining[i] <= 0f)
                {
                    continue;
                }

                voiceRemaining[i] -= elapsed * voiceRate[i];

                if (voiceRemaining[i] <= 0f)
                {
                    voiceFreeAt[i] = now;
                    continue;
                }

                if (FollowsGameTime(voiceOwner[i]))
                {
                    float rate = RateNow(voiceOwner[i], voicePitch[i]);
                    AudioSource source = voices[i];

                    if (rate <= 0f)
                    {
                        if (voiceRate[i] > 0f)
                        {
                            source.Pause();
                        }
                    }
                    else
                    {
                        if (voiceRate[i] <= 0f)
                        {
                            source.UnPause();
                        }

                        source.pitch = rate;
                    }

                    voiceRate[i] = rate;
                }

                voiceFreeAt[i] = FreeAt(i, now);
            }
        }

        /// <summary>
        /// A run left from the pause menu leaves its gameplay sounds paused, and
        /// the next scene is not paused, so they would pick up where they
        /// stopped over the title screen. They belong to the run that is gone.
        /// </summary>
        private void OnSceneUnloaded(Scene scene)
        {
            if (voices == null)
            {
                return;
            }

            float now = Time.unscaledTime;

            for (int i = 0; i < voices.Length; i++)
            {
                if (voiceOwner[i] != null && voiceRemaining[i] > 0f && voiceRate[i] <= 0f)
                {
                    Release(i, now);
                }
            }
        }

        private void Release(int slot, float now)
        {
            voices[slot].Stop();
            voiceFreeAt[slot] = now;
            voiceRemaining[slot] = 0f;
            voiceOwner[slot] = null;
        }

        private int CountActive(SoundDefinition sound, float now)
        {
            int count = 0;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voiceOwner[i] == sound && voiceFreeAt[i] > now)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Silences whatever copies of <paramref name="sound"/> are still
        /// sounding, and frees their voices.
        ///
        /// Everything else this class plays is short enough that it is over
        /// before anyone could want it back. A telegraph is not: it runs for the
        /// length of a wind-up precisely so the player can act on it, which means
        /// there is a window in which the thing being announced can stop being
        /// true. The boss's lance is the case - shoot its emplacement part way
        /// through and the shot never comes, and a charge that kept sounding
        /// would be telling the player to dodge something that no longer exists.
        ///
        /// Only sounds that are still running are touched, so calling this on a
        /// telegraph that already finished does nothing.
        /// </summary>
        public static void Stop(SoundDefinition sound)
        {
            if (Instance != null)
            {
                Instance.StopInternal(sound);
            }
        }

        private void StopInternal(SoundDefinition sound)
        {
            if (sound == null || voices == null)
            {
                return;
            }

            float now = Time.unscaledTime;

            for (int i = 0; i < voices.Length; i++)
            {
                if (voiceOwner[i] != sound || voiceFreeAt[i] <= now)
                {
                    continue;
                }

                // Free the slot as well as the source. Without this the voice
                // stays reserved until the clip it is no longer playing would
                // have ended, and MaxVoices 1 - which every telegraph wants -
                // would silently refuse the next charge.
                Release(i, now);
            }

            // The retrigger guard is keyed on when a sound last started, and a
            // cancelled telegraph should not be made to wait out the gap meant to
            // stop it stacking on itself.
            lastStarted.Remove(sound);
        }

        private int FindFreeVoice(float now)
        {
            for (int i = 0; i < voices.Length; i++)
            {
                if (voiceFreeAt[i] <= now)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
