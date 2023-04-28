using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBounds : MonoBehaviour
{

    [Header("Bounds")]
    [SerializeField]
    private BoxCollider2D playerBounds;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        ApplyBounds();
    }


     private void ApplyBounds()
    {
        var minX = -playerBounds.bounds.extents.x + playerBounds.offset.x + playerBounds.transform.position.x;
        var maxX = playerBounds.bounds.extents.x + playerBounds.offset.x + playerBounds.transform.position.x;

        var minY = -playerBounds.bounds.extents.y + playerBounds.offset.y + playerBounds.transform.position.y;
        var maxY = playerBounds.bounds.extents.y + playerBounds.offset.y + playerBounds.transform.position.y;

        transform.position = new Vector3(
            Mathf.Clamp(transform.position.x, minX, maxX),
            Mathf.Clamp(transform.position.y, minY, maxY),
            transform.position.z
        );
    }
}
