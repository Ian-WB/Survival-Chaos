using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

public class SpaceShipPitch : MonoBehaviour
{

    public float rotationSpeed;

    [SerializeField]
    [Tooltip("Player whose flip state drives this model. Found in the parents when left empty.")]
    private Player player;

    void Start()
    {
        if (player == null)
        {
            player = GetComponentInParent<Player>();
        }

        if (player == null)
        {
            Debug.LogWarning("SpaceShipPitch found no Player; the model will not flip.", this);
        }
    }

    // Update is called once per frame
    void Update()
    {
        float v = GameInput.Vertical;
        float h = GameInput.Horizontal;

        // Reads the player's flip state rather than toggling a second copy of
        // it. The two used to be toggled independently by the same key, so any
        // frame where one saw the input and the other did not left the model
        // facing the wrong way for the rest of the run.
        bool flipped = player != null && player.DirectionFlipped;

        if(flipped)
        {
            transform.localRotation = Quaternion.Euler(0, 180, -Mathf.Sign(h) * v * 25);
        }

        else
        {
            transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sign(h) * v * 25);
        }
    }
}
