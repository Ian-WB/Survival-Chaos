using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// Carries kill rewards to whoever is keeping the experience bar - the Player.
    ///
    /// Wakes before every other script. Player subscribes from OnEnable, and
    /// Unity only orders Awake and OnEnable across objects by execution order:
    /// at the default of 0 for both, whether this Awake had run yet came down
    /// to the order the scene happened to load its objects in, and when it had
    /// not, every kill of the run earned nothing and nothing said so. Player
    /// tries again from Start as well - see Player.BindExperience.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class EXP : MonoBehaviour
    {
        public static EXP Instance;

        /// <summary>
        /// A kill's reward, before the player's multiplier, and where the kill
        /// was - the player scales the one and shows the result at the other.
        /// </summary>
        public delegate void EXPChangeHandler(int amount, Vector3 where);
        public event EXPChangeHandler OnEXPChange;

        //Check to see if there's more than one instance, if there is, destroy it, it's just a safety check but it's good to have one. - Luis Fernando
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }
        }

        /// <summary>
        /// Drops the static so it does not outlive the object it points at.
        ///
        /// Nothing is broken without this today. Unity overloads == on Object to
        /// report a destroyed object as null, so the next scene's Awake takes the
        /// else branch and reassigns correctly either way. What it removes is the
        /// window in between, where Instance holds a corpse that reads as null
        /// through that operator and not through every other route to it - and
        /// statics outlive play mode entirely when domain reload is disabled.
        ///
        /// Guarded on this rather than cleared outright, because Awake destroys the
        /// duplicate that arrives second. That duplicate's OnDestroy runs too, and
        /// an unguarded clear there would null out the instance that won.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void AddEXP(int amount, Vector3 where)
        {
            OnEXPChange?.Invoke(amount, where);
        }
    }
}
