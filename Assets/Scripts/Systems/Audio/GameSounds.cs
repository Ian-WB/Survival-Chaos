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
        [Tooltip("The dash burst. Keep it short: the burst itself is 0.22s, and anything with a " +
                 "longer tail outlasts the move it belongs to.")]
        private SoundDefinition playerDash;

        [SerializeField]
        [Tooltip("The deflector taking a hit for the ship. It has to read as the opposite of a hit: " +
                 "something struck, and nothing was lost.")]
        private SoundDefinition deflectorBlock;

        [SerializeField]
        [Tooltip("Slow Mo starting. On the Interface channel, so it is heard at its own pitch rather " +
                 "than slowed with the game it announces.")]
        private SoundDefinition slowMoStart;

        [SerializeField]
        [Tooltip("Slow Mo running out. Interface channel, like the start.")]
        private SoundDefinition slowMoEnd;

        [SerializeField]
        [Tooltip("A heartbeat, repeated while the ship is on its last hit point. HealthBar sets " +
                 "how often, and from how many hit points.")]
        private SoundDefinition lowHealth;

        [Header("Progression")]
        [SerializeField]
        private SoundDefinition levelUp;

        [SerializeField]
        [Tooltip("Confirming a skill from the level-up panel.")]
        private SoundDefinition skillPicked;

        [SerializeField]
        [Tooltip("Salvage repairing the ship. It used to borrow Skill Picked, so +1 HP sounded like " +
                 "a new upgrade.")]
        private SoundDefinition salvagePicked;

        [SerializeField]
        [Tooltip("An offer on the ring with three, two and one seconds left, once each, alongside " +
                 "the flashing.")]
        private SoundDefinition offerExpiring;

        [Header("Enemies and boss")]
        [SerializeField]
        [Tooltip("Fallback for an enemy whose definition names no death sound of its own.")]
        private SoundDefinition enemyDeath;

        [SerializeField]
        private SoundDefinition bossShot;

        [SerializeField]
        private SoundDefinition bossDeath;

        [SerializeField]
        [Tooltip("The lance winding up. A telegraph rather than an event: it runs for the whole " +
                 "of the charge and is cut short if the emplacement firing it dies first.")]
        private SoundDefinition bossChargeLance;

        [SerializeField]
        [Tooltip("The hull spooling up before it rams. Nothing can cancel this one - by the second " +
                 "act there is no emplacement left to shoot off.")]
        private SoundDefinition bossChargeRam;

        [SerializeField]
        [Tooltip("An act of the Leviathan fight ending: the last emplacement gone, or the hull " +
                 "worn down to the last act. Played over the hit-stop.")]
        private SoundDefinition actEnd;

        [SerializeField]
        [Tooltip("Far off, ten seconds before the Leviathan arrives.")]
        private SoundDefinition bossHornDistant;

        [SerializeField]
        [Tooltip("As the Leviathan arrives.")]
        private SoundDefinition bossHornArrival;

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
        public SoundDefinition DeflectorBlock => deflectorBlock;
        public SoundDefinition SlowMoStart => slowMoStart;
        public SoundDefinition SlowMoEnd => slowMoEnd;
        public SoundDefinition LowHealth => lowHealth;
        public SoundDefinition SalvagePicked => salvagePicked;
        public SoundDefinition OfferExpiring => offerExpiring;
        public SoundDefinition ActEnd => actEnd;
        public SoundDefinition BossHornDistant => bossHornDistant;
        public SoundDefinition BossHornArrival => bossHornArrival;
        public SoundDefinition LevelUp => levelUp;
        public SoundDefinition SkillPicked => skillPicked;
        public SoundDefinition EnemyDeath => enemyDeath;
        public SoundDefinition BossShot => bossShot;
        public SoundDefinition BossDeath => bossDeath;
        public SoundDefinition BossChargeLance => bossChargeLance;
        public SoundDefinition BossChargeRam => bossChargeRam;
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
        /// Forgets the lookup on entering play, failure included, so an asset
        /// created between two play sessions is found without a domain reload.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            cached = null;
            searched = false;
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
