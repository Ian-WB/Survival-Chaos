using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The sounds the game asks for by name, in one asset.
    ///
    /// The alternative — a <see cref="SoundDefinition"/> field on each script that
    /// makes a noise — would mean wiring the player prefab, the boss prefab, every
    /// button and every menu by hand, and an unassigned field on any one of them
    /// is a silence nobody notices. One asset is one place to look, and one place
    /// to see what is still missing.
    ///
    /// Per-enemy death sounds are the deliberate exception: they live on
    /// <see cref="EnemyDefinition"/> beside that enemy's health and reward,
    /// because they vary per enemy and that asset already exists.
    ///
    /// Loaded from Resources because the things that need it — a self-creating
    /// AudioDirector, a pooled bullet, a button built by an editor tool — have no
    /// Inspector to be wired through.
    /// </summary>
    [CreateAssetMenu(menuName = "Survival Chaos/Game Sounds", fileName = "GameSounds")]
    public sealed class GameSounds : ScriptableObject
    {
        /// <summary>Path within a Resources folder. The asset must be named to match.</summary>
        public const string ResourcePath = "GameSounds";

        private static GameSounds cached;
        private static bool searched;

        [Header("Player")]
        [SerializeField]
        [Tooltip("One volley, not one bullet. A full spread fires ten at once and should still read as a single shot.")]
        private SoundDefinition playerShot;

        [SerializeField]
        [Tooltip("The player taking a hit, from a bullet or a collision.")]
        private SoundDefinition playerHit;

        [SerializeField]
        private SoundDefinition playerDeath;

        [SerializeField]
        [Tooltip("The dash burst. Short: it fires on a 1.2s cycle at the busiest moments in the " +
                 "run, so anything with a tail will overlap itself.")]
        private SoundDefinition playerDash;

        [Header("Progression")]
        [SerializeField]
        private SoundDefinition levelUp;

        [SerializeField]
        [Tooltip("Confirming a skill from the level-up panel.")]
        private SoundDefinition skillPicked;

        [Header("Enemies and boss")]
        [SerializeField]
        [Tooltip("Fallback for an enemy whose definition names no death sound of its own.")]
        private SoundDefinition enemyDeath;

        [SerializeField]
        private SoundDefinition bossShot;

        [SerializeField]
        private SoundDefinition bossDeath;

        [Header("Run")]
        [SerializeField]
        private SoundDefinition victory;

        [Header("Interface")]
        [SerializeField]
        [Tooltip("Answers to the Interface slider rather than Effects, so turning combat down does not mute the menus.")]
        private SoundDefinition uiClick;

        [SerializeField]
        private SoundDefinition uiHover;

        public SoundDefinition PlayerShot => playerShot;
        public SoundDefinition PlayerHit => playerHit;
        public SoundDefinition PlayerDeath => playerDeath;
        public SoundDefinition PlayerDash => playerDash;
        public SoundDefinition LevelUp => levelUp;
        public SoundDefinition SkillPicked => skillPicked;
        public SoundDefinition EnemyDeath => enemyDeath;
        public SoundDefinition BossShot => bossShot;
        public SoundDefinition BossDeath => bossDeath;
        public SoundDefinition Victory => victory;
        public SoundDefinition UiClick => uiClick;
        public SoundDefinition UiHover => uiHover;

        /// <summary>
        /// The asset, or null if it has not been created yet.
        ///
        /// Looked up once and remembered, including the failure: a game that fires
        /// hundreds of projectiles a second must not hit the Resources system on
        /// every one of them just to rediscover that the asset is absent.
        /// </summary>
        public static GameSounds Instance
        {
            get
            {
                if (!searched)
                {
                    searched = true;
                    cached = Resources.Load<GameSounds>(ResourcePath);
                }

                return cached;
            }
        }

        /// <summary>
        /// Plays one of these sounds, given no asset, no sound, or no clip.
        ///
        /// Every call site is a line inside gameplay code that already had a job,
        /// so the null handling belongs here rather than repeated at each of them.
        /// </summary>
        public static void Play(SoundDefinition sound)
        {
            AudioDirector.Play(sound);
        }

        /// <summary>As <see cref="Play"/>, for sounds authored as positional.</summary>
        public static void PlayAt(SoundDefinition sound, Vector3 position)
        {
            AudioDirector.PlayAt(sound, position);
        }

        /// <summary>
        /// Drops the remembered lookup. Called by the editor tool after creating
        /// the asset, so the first play does not keep reporting it missing.
        /// </summary>
        public static void Forget()
        {
            cached = null;
            searched = false;
        }
    }
}
