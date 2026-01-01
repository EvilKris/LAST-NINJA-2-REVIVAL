using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MovementComponent : MonoBehaviour
{
    [Header("Movement Settings")]
    public float movementSpeed = 5f;
    public float rotationSpeed = 60f;

    private Rigidbody _rb;
    private Animator _animator;

    [HideInInspector] public float speedMultiplier = 1.0f;

    // Animation Parameter Names
    readonly string anim_isRunning = "isRunningBool";
    readonly string anim_xAxis = "Input_XFloat";
    readonly string anim_yAxis = "Input_YFloat";

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
    }

    public void ProcessMovement(Vector2 input)
    {
        bool isMoving = input.sqrMagnitude > 0.01f;

        if (isMoving)
        {
            // Apply the speedMultiplier here (Kakezan - 掛け算)
            float finalSpeed = movementSpeed * speedMultiplier;
            _rb.linearVelocity = transform.TransformDirection(finalSpeed * new Vector3(input.x, 0, input.y));
        }
        else
        {
            _rb.linearVelocity = Vector3.zero;
        }

        // Update Animator
        _animator.SetBool(anim_isRunning, isMoving);
        _animator.SetFloat(anim_xAxis, input.x);
        _animator.SetFloat(anim_yAxis, input.y);
    }

    public void RotateTowardsPoint(Vector3 targetPoint)
    {
        if (Vector3.Distance(transform.position, targetPoint) < 0.1f) return;

        Vector3 eulerRotation = Quaternion.LookRotation(targetPoint - transform.position).eulerAngles;
        eulerRotation.x = eulerRotation.z = 0;

        float distance = Vector3.Distance(transform.position, targetPoint);
        _rb.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(eulerRotation), distance * rotationSpeed * Time.fixedDeltaTime);
    }
}