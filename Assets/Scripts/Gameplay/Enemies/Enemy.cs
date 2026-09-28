namespace SurvivalChaos
{
    /// <summary>
    /// An enemy that does not shoot: the Enemy 2 and Enemy 3 prefabs. Everything
    /// it does is shared with the gunship and lives in <see cref="EnemyBase"/>.
    /// </summary>
    public class Enemy : EnemyBase
    {
        protected override int FallbackReward => 5;
    }
}
