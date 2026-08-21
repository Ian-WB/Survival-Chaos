using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    public class EXP : MonoBehaviour
    {
        public static EXP Instance;

        public delegate void EXPChangeHandler(int amount);
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

        public void AddEXP(int amount)
        {
            OnEXPChange?.Invoke(amount);
        }
    }
}
