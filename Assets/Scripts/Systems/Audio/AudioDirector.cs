using System;
using System.Collections.Generic;
using UnityEngine;

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
    /// throttling. That is not tidiness: the player fires up to ten projectiles
    /// per volley and the boss twenty-nine, so an AudioSource per shot would mean
    /// hundreds of them, and every copy starting in the same frame adds amplitude
    /// until the mix clips.
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
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void BuildVoices()
        {
            voices = new AudioSource[VoiceCount];
            voiceOwner = new SoundDefinition[VoiceCount];
            voiceFreeAt = new float[VoiceCount];

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
        /// Every channel starts open. Balance belongs in the sources — the music
        /// AudioSource is authored at 0.15, and that is the mix the team chose —
        /// so a slider at full should sound exactly like the game does today.
        /// Sliders attenuate from there; they are not a place to set balance.
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

            int cursor = clipCursor.TryGetValue(sound, out int stored) ? stored : 0;
            AudioClip clip = sound.PickClip(ref cursor);
            clipCursor[sound] = cursor;

            if (clip == null)
            {
                return;
            }

            AudioSource source = voices[slot];
            source.clip = clip;
            source.outputAudioMixerGroup = sound.Output;
            source.pitch = sound.PickPitch();
            source.spatialBlend = position.HasValue ? sound.SpatialBlend : 0f;
            source.transform.position = position ?? Vector3.zero;
            source.volume = sound.Volume * AudioLevels.ToAmplitude(GetLevel(sound.Channel));
            source.Play();

            voiceOwner[slot] = sound;
            // Pitch changes playback rate, so the clip's length is not how long
            // it will actually take.
            voiceFreeAt[slot] = now + clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch));
            lastStarted[sound] = now;
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
