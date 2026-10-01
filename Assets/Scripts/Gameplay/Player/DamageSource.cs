using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// What a hit on the ship came from, in the words the Ship Lost card uses:
    /// "Lost to a Leviathan torpedo".
    ///
    /// Until 30 September 2026 a hit arrived with no source at all, so the card
    /// could say how long you lasted but not what got you, and every playtest
    /// bot report said "damage cause unknown". Rounds are named by the prefab
    /// the pool made them from, which is the one thing every round carries;
    /// a ram by the enemy's own definition.
    /// </summary>
    public static class DamageSource
    {
        public const string LeviathanHull = "the Leviathan's hull";
        public const string LeviathanLance = "the Leviathan's lance";
        public const string LeviathanTorpedo = "a Leviathan torpedo";
        public const string HullPlate = "a falling hull plate";
        public const string UnknownRound = "a round";

        /// <summary>A hostile round, torpedoes told apart by how they fly rather than by their prefab.</summary>
        public static string OfRound(GameObject round)
        {
            if (round == null)
            {
                return UnknownRound;
            }

            if (round.TryGetComponent(out ShootScript shot) && shot.IsTorpedo)
            {
                return LeviathanTorpedo;
            }

            string prefab = round.TryGetComponent(out PooledInstance pooled) && pooled.Source != null
                ? pooled.Source.name
                : round.name;

            return OfRoundPrefab(prefab);
        }

        /// <summary>
        /// A round's source from its prefab's name. The curtain's discs, the
        /// Leviathan's plain rounds (the last act's volleys), the Fighter's and
        /// the Heavy's; <c>RoundSourceTests</c> checks the names still exist.
        /// </summary>
        public static string OfRoundPrefab(string prefab)
        {
            if (string.IsNullOrEmpty(prefab))
            {
                return UnknownRound;
            }

            if (prefab.StartsWith("boss_disc"))
            {
                return "the Leviathan's curtain";
            }

            if (prefab.StartsWith("boss_shoot"))
            {
                return "the Leviathan's guns";
            }

            if (prefab.StartsWith("enemy_shoot"))
            {
                return "a Fighter's round";
            }

            if (prefab.StartsWith("temp_shoot"))
            {
                return "a Heavy's round";
            }

            return UnknownRound;
        }

        /// <summary>Flying into an enemy, named by its definition: "ramming a Scout".</summary>
        public static string OfRam(GameObject enemy)
        {
            EnemyBase ship = enemy != null ? enemy.GetComponentInParent<EnemyBase>(includeInactive: true) : null;
            string name = ship != null && ship.Definition != null ? ship.Definition.DisplayName : null;
            return string.IsNullOrEmpty(name) ? "ramming an enemy" : "ramming a " + name;
        }

        /// <summary>Touching the Leviathan: its hull, or a plate it shed.</summary>
        public static string OfBoss(GameObject part)
        {
            return part != null && part.GetComponentInParent<BossWreckage>(includeInactive: true) != null
                ? HullPlate
                : LeviathanHull;
        }
    }
}
