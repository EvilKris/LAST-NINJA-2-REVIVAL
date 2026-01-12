using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MovementComponent : MonoBehaviour
{
    [Header("Movement Settings")]
    public float movementSpeed = 5f;
    public float rotationSpeed = 12f;

    private Rigidbody _rb;
    private Animator _animator;

    [HideInInspector] public float speedMultiplier = 1.0f;

    // Animator Parameter Names from image_6c3b66.png
    readonly string anim_isRunning = "isRunningBool";
    readonly string anim_xAxis = "Input_XFloat";
    readonly string anim_yAxis = "Input_YFloat";

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();

        // Constrain rotation so the Ninja doesn't tip over
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    // --- MODE 1: FREESTYLE (Gauntlet Style) ---
    // Used when exploring or moving without a specific target.
    public void ProcessMovement(Vector3 moveDir)
    {
        float magnitude = moveDir.magnitude;
        bool isMoving = magnitude > 0.01f;

        UpdateAnimatorBooleans(isMoving);

        if (speedMultiplier <= 0.01f) { StopVelocity(); return; }

        if (isMoving)
        {
            _rb.linearVelocity = moveDir * (movementSpeed * speedMultiplier);
            RotateTowardsDirection(moveDir);

            // Freestyle uses Y-axis for speed, X is ignored
            _animator.SetFloat(anim_yAxis, magnitude);
            _animator.SetFloat(anim_xAxis, 0f);
        }
        else
        {
            StopVelocity();
        }
    }

    // --- MODE 2: TARGETED (Dark Souls Style) ---
    // Used by CombatActorBrain or Player Lock-On.
    public void ProcessMovement(Vector3 moveDir, Vector3 lookAtPos)
    {
        bool isMoving = moveDir.sqrMagnitude > 0.01f;
        UpdateAnimatorBooleans(isMoving);

        if (speedMultiplier <= 0.01f) { StopVelocity(); return; }

        // 1. Move the Physics Body
        _rb.linearVelocity = moveDir * (movementSpeed * speedMultiplier);

        // 2. Always face the Target
        Vector3 dirToTarget = (lookAtPos - transform.position);
        dirToTarget.y = 0;
        RotateTowardsDirection(dirToTarget);

        // 3. Calculate Local Directions for Strafing
        // This maps world movement to your 2D Blend Tree nodes (Forward, Back, Left, Right)
        Vector3 localDir = transform.InverseTransformDirection(moveDir);

        _animator.SetFloat(anim_xAxis, localDir.x);
        _animator.SetFloat(anim_yAxis, localDir.z);
    }

    public void RotateTowardsDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));
    }

    private void UpdateAnimatorBooleans(bool isMoving)
    {
        _animator.SetBool(anim_isRunning, isMoving);
    }

    private void StopVelocity()
    {
        _rb.linearVelocity = Vector3.zero;
        _animator.SetFloat(anim_xAxis, 0f);
        _animator.SetFloat(anim_yAxis, 0f);
    }
}