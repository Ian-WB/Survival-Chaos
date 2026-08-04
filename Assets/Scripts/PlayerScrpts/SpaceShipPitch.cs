using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SurvivalChaos;

/// <summary>
/// Animates the ship model.
///
/// The ship's nose already points along its direction of travel, so the two
/// inputs mean different things:
///   W / S  climb and dive  -> nose lifts or drops, with a little roll
///   A / D  throttle        -> the craft leans into the acceleration and drags
///                             back against it, the way a hovering craft does
///
/// Every angle and distance is signed. If one reads backwards on your model,
/// negate it in the inspector rather than editing this file - the ship's
/// authored orientation decides which way each axis goes.
/// </summary>
public class SpaceShipPitch : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Player whose flip state drives this model. Found in the parents when left empty.")]
    private Player player;

    [Header("Climb / dive (W and S)")]
    [SerializeField]
    [Tooltip("Degrees the nose lifts at full climb.")]
    private float climbPitchAngle = 22f;

    [SerializeField]
    [Tooltip("Degrees the ship rolls as it climbs. Keep this small; it is flavour, not the main cue.")]
    private float climbRollAngle = 10f;

    [SerializeField]
    [Tooltip("How quickly climb motion follows. Higher is snappier; 0 disables easing.")]
    private float climbResponse = 7f;

    [Header("Throttle (A and D)")]
    [SerializeField]
    [Tooltip("Degrees the craft leans into acceleration. Nose dips going forward, lifts reversing. " +
             "This is the main 'moving forward' cue - push it if you want it more pronounced.")]
    private float thrustLeanAngle = 18f;

    [SerializeField]
    [Tooltip("World units the ship drags back against acceleration, as inertia. Set 0 to disable.")]
    private float thrustDragDistance = 0.06f;

    [SerializeField]
    [Tooltip("How quickly throttle motion follows. Lower feels heavier.")]
    private float thrustResponse = 4f;

    [Header("Direction flip (Shift)")]
    [SerializeField]
    [Tooltip("Sweep speed of the 180 degree turn.")]
    private float flipDegreesPerSecond = 540f;

    [Header("Idle drift")]
    [SerializeField]
    [Tooltip("Gentle motion while no input is held. Fades out as the player takes control.")]
    private float idleRollAmplitude = 3f;

    [SerializeField]
    private float idlePitchAmplitude = 2f;

    [SerializeField]
    private float idleSpeed = 0.7f;

    private float climbPitch;
    private float climbRoll;
    private float thrustLean;
    private float thrustDrag;
    private float facing;
    private Vector3 restPosition;

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

        restPosition = transform.localPosition;
        facing = FacingTarget();
    }

    void Update()
    {
        float climb = GameInput.Vertical;
        float throttle = GameInput.Horizontal;

        // Flipping mirrors the model, so "forward" reverses with it.
        float mirror = IsFlipped ? -1f : 1f;
        float delta = Time.deltaTime;

        facing = ShipMotion.ApproachAngle(facing, FacingTarget(), flipDegreesPerSecond, delta);

        climbPitch = ShipMotion.Approach(climbPitch, -climb * climbPitchAngle * mirror, climbResponse, delta);
        climbRoll = ShipMotion.Approach(climbRoll, climb * climbRollAngle * mirror, climbResponse, delta);

        thrustLean = ShipMotion.Approach(thrustLean, throttle * thrustLeanAngle * mirror, thrustResponse, delta);
        thrustDrag = ShipMotion.Approach(thrustDrag, -throttle * thrustDragDistance * mirror, thrustResponse, delta);

        float idle = 1f - Mathf.Clamp01(Mathf.Abs(climb) + Mathf.Abs(throttle));
        float wave = Time.time * idleSpeed;
        float idleRoll = Mathf.Sin(wave) * idleRollAmplitude * idle;
        float idlePitch = Mathf.Sin(wave * 0.7f + 1.3f) * idlePitchAmplitude * idle;

        // Z carries the nose: climb pitch and throttle lean both tilt it along
        // the direction of travel, and combine when you climb while thrusting.
        transform.localRotation = Quaternion.Euler(
            climbRoll + idleRoll,
            facing,
            climbPitch + thrustLean + idlePitch);

        // X in the parent's space is the tangent - the direction the ship
        // travels - so offsetting it slides the ship along its own heading.
        transform.localPosition = restPosition + new Vector3(thrustDrag, 0f, 0f);
    }

    private bool IsFlipped => player != null && player.DirectionFlipped;

    private float FacingTarget()
    {
        return IsFlipped ? 180f : 0f;
    }
}
