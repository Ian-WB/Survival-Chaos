using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Stats for one kind of enemy. These previously lived in two places at
    /// once: health was serialized per prefab, while the experience reward was
    /// a hardcoded field shared by every prefab using the same script - so two
    /// prefabs could not differ in reward without a new script.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "Survival Chaos/Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField]
        private string displayName = "Enemy";

        [SerializeField]
        [Tooltip("Hits required to destroy this enemy.")]
        private int maxHealth = 1;

        [SerializeField]
        [Tooltip("Experience awarded to the player on death.")]
        private int experienceReward = 5;

        [SerializeField]
        [Tooltip("Played where this enemy dies. Leave empty to use the shared one on GameSounds - " +
                 "a heavy is worth its own sound, a scout probably is not.")]
        private SoundDefinition deathSound;

        public string DisplayName => displayName;

        public int MaxHealth => maxHealth;

        public int ExperienceReward => experienceReward;

        public SoundDefinition DeathSound => deathSound;
    }
}
