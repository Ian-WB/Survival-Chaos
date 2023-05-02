using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shot : MonoBehaviour
{
    [HideInInspector] public Enemy enemyHp;
    [SerializeField] private int damage = 1;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {

            Destroy(gameObject);

            // Update Health Points

            enemyHp = other.gameObject.GetComponent<Enemy>();
            enemyHp.adjustHealth(damage);
        }
    }
}

    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
