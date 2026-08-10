using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalChaos
{
    /// <summary>
    /// The boss: health, and a list of attacks it fires.
    ///
    /// Replaces BossScript, which held 32 named pivot fields, four projectile
    /// fields, eight delay fields (six of them unread), and three near-identical
    /// firing methods. Its abandoned per-attack delay fields are what this
    /// class's per-attack timing actually delivers.
    /// </summary>
    public sealed class BossEmitter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Stats for the boss. Falls back to the health value below when unset.")]
        private EnemyDefinition definition;

        [SerializeField]
        private int healthPoints = 1;

        [SerializeField]
        [Tooltip("The object carrying EnemyMovement, used to tell which way the boss is travelling.")]
        private GameObject enemyShip;

        [SerializeField]
        private List<BossAttack> attacks = new List<BossAttack>();

        /// <summary>Set by the laser trigger volume as the player enters and leaves.</summary>
        public bool LaserActive { get; set; }

        private HealthState health;
        private EnemyMovement movement;
        private Slider hpBar;
        private VolleyTimer[] timers;

        private void Awake()
        {
            health = new HealthState(definition != null ? definition.MaxHealth : healthPoints);

            if (enemyShip != null)
            {
                enemyShip.TryGetComponent(out movement);
            }
        }

        private void Start()
        {
            GameObject bar = GameObject.FindGameObjectWithTag("bossHpBar");
            if (bar != null)
            {
                hpBar = bar.GetComponent<Slider>();
            }

            if (hpBar != null)
            {
                hpBar.maxValue = health.Max;
                hpBar.value = health.Current;
            }

            timers = new VolleyTimer[attacks.Count];
            for (int i = 0; i < attacks.Count; i++)
            {
                timers[i] = new VolleyTimer(Time.time, attacks[i].initialDelay);
            }

            WarmProjectilePools();
        }

        /// <summary>
        /// Builds one volley's worth of every projectile the boss can fire,
        /// during its entrance rather than during the fight.
        ///
        /// The opening volley is the largest simultaneous spawn in the game, and
        /// it lands at the climax. One volley per travel direction is the useful
        /// amount: it covers the first shot of each attack and lets the rest grow
        /// on its own, without stalling the entrance to build a pool six volleys
        /// deep. The size needs no tuning - an attack fires one projectile per
        /// pivot, so the pivots already say how many.
        /// </summary>
        private void WarmProjectilePools()
        {
            foreach (BossAttack attack in attacks)
            {
                if (attack == null || attack.pivots == null)
                {
                    continue;
                }

                ObjectPool.Warm(attack.projectileWhenLeft, attack.pivots.Length);
                ObjectPool.Warm(attack.projectileWhenRight, attack.pivots.Length);
            }
        }

        private void Update()
        {
            if (timers == null)
            {
                return;
            }

            float now = Time.time;

            for (int i = 0; i < attacks.Count; i++)
            {
                BossAttack attack = attacks[i];

                if (attack.trigger == AttackTrigger.WhileLaserActive && !LaserActive)
                {
                    continue;
                }

                if (timers[i].TryFire(now, attack.interval))
                {
                    Fire(attack);
                }
            }
        }

        private void Fire(BossAttack attack)
        {
            GameObject projectile = attack.ProjectileFor(TravellingLeft);
            if (projectile == null || attack.pivots == null)
            {
                return;
            }

            foreach (Transform pivot in attack.pivots)
            {
                if (pivot != null)
                {
                    ObjectPool.Spawn(projectile, pivot.position, Quaternion.identity);
                }
            }

            // One per attack rather than per muzzle - the volley alone has
            // sixteen, and the retrigger guard on the sound is a safety net, not
            // a licence to ask for sixteen copies of the same noise.
            if (GameSounds.Instance != null)
            {
                GameSounds.PlayAt(GameSounds.Instance.BossShot, transform.position);
            }
        }

        private bool TravellingLeft => movement != null && movement.leftOrRight;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Shoot"))
            {
                return;
            }

            ObjectPool.Despawn(other.gameObject);

            bool killed = health.TakeDamage(1);

            if (hpBar != null)
            {
                hpBar.value = health.Current;
            }

            if (killed)
            {
                Death();
                Destroy(gameObject);
            }
        }

        private void Death()
        {
            int reward = definition != null ? definition.ExperienceReward : 2;

            if (EXP.Instance != null)
            {
                EXP.Instance.AddEXP(reward);
            }

            // Not positional, and played before the object goes: this is the run
            // ending, not an event somewhere out on the ring.
            GameSounds sounds = GameSounds.Instance;
            if (sounds != null)
            {
                GameSounds.Play(sounds.BossDeath != null ? sounds.BossDeath : sounds.Victory);
            }

            RunOutcome.ReportBossDefeated();
        }
    }
}
