using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Speeds the player around the ring.
    ///
    /// The one upgrade that improves dodging rather than damage. Everything else
    /// on offer makes the player better at clearing what is in front of them;
    /// this makes them better at not being where the enemy is, which is the other
    /// half of a survival game and had nothing to buy.
    /// </summary>
    [CreateAssetMenu(fileName = "MoveSpeed", menuName = "Survival Chaos/Skills/Move Speed")]
    public sealed class MoveSpeedSkill : SkillDefinition
    {
        public override void Apply(ISkillTarget target)
        {
            target.IncreaseMoveSpeed();
        }
    }
}
