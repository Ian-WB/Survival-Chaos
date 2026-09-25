using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Slows the whole game for a few seconds on a button, with a long wait
    /// between uses. The first pick grants it; later picks shorten the wait.
    /// See <see cref="PlayerSlowMo"/>.
    ///
    /// Took Piercing Rounds' place on 25 September 2026. That one made the
    /// player better at clearing lines of ships; this one buys time to read
    /// a moment that is going wrong, which is worth more now the dash comes
    /// round only every ten seconds.
    /// </summary>
    [CreateAssetMenu(fileName = "SlowMo", menuName = "Survival Chaos/Skills/Slow Mo")]
    public sealed class SlowMoSkill : StagedSkill
    {
        public override void Apply(ISkillTarget target)
        {
            target.UpgradeSlowMo();
        }
    }
}
