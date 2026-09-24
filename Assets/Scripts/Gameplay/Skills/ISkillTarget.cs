namespace SurvivalChaos
{
    /// <summary>
    /// Everything a skill is allowed to do to the player. Skills depend on this
    /// rather than on the Player MonoBehaviour, which is what makes skill
    /// selection testable without a scene.
    /// </summary>
    public interface ISkillTarget
    {
        /// <summary>Advances the shot pattern one step (double, triple, sextuple).</summary>
        void UpgradeShotPattern();

        /// <summary>Raises the health ceiling and grants the same amount as current health.</summary>
        void AddMaxHealth(int amount);

        /// <summary>Restores health, capped at the current maximum.</summary>
        void Heal(int amount);

        /// <summary>Reduces the delay between shots.</summary>
        void IncreaseAttackSpeed();

        /// <summary>Raises how fast the player travels around the ring.</summary>
        void IncreaseMoveSpeed();

        /// <summary>Lets every round pass through one more enemy.</summary>
        void AddPierce();

        /// <summary>Shortens the wait between dashes.</summary>
        void QuickenDash();

        /// <summary>Grants the deflector, or shortens its recharge once it is held.</summary>
        void UpgradeDeflector();

        /// <summary>Widens the distance pickups drift to the player from.</summary>
        void ExtendMagnet();
    }
}
