using System.Collections;
using UnityEngine;

public class ClinchHandler : MonoBehaviour
{
    private CombatHandler _combat;
    private Animator _animator;
    private MovementComponent _movement;

    [Header("Clinch State")]
    private Transform _grabbedEnemy;
    private bool _isClinching;
    private float _clinchTimer;
    private const float MAX_CLINCH_DURATION = 3.0f;

    public bool IsClinching => _isClinching;

    public void Initialize(CombatHandler combat)
    {
        _combat = combat;
        _animator = GetComponent<Animator>();
        _movement = GetComponent<MovementComponent>();
    }

    private void Update()
    {
        if (!_isClinching) return;

        _clinchTimer += Time.deltaTime;
        if (_clinchTimer >= MAX_CLINCH_DURATION)
        {
            EndClinch();
            return;
        }

        // Handle Clinch Movement (Strafing at reduced speed)
        UpdateClinchMovement();
    }

    public void AttemptClinch(Transform target)
    {
        if (_isClinching || _combat.IsAttacking) return;
        StartCoroutine(ClinchSequence(target));
    }

    private IEnumerator ClinchSequence(Transform target)
    {
        _isClinching = true;
        _grabbedEnemy = target;
        _clinchTimer = 0f;

        // 1. Disable standard rotation and slow down
        _movement.canRotate = false;
        // _movement.speedMultiplier = 0.4f; // Optional: Apply speed penalty here

        // 2. Alignment: Smoothly snap Ninja to the front of the Enemy
        Vector3 targetPos = target.position + (target.forward * 0.8f);
        Quaternion targetRot = Quaternion.LookRotation(-target.forward);

        float elapsed = 0;
        float duration = 0.15f;

        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, elapsed / duration);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, elapsed / duration);

            // Keep enemy facing the player during the struggle
            target.rotation = Quaternion.Slerp(target.rotation, Quaternion.LookRotation(-transform.forward), elapsed / duration);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. Parenting/Constraint: Ensure enemy stays with player
        _grabbedEnemy.SetParent(transform);

        // 4. Play struggle animation
        _animator.Play("Clinch_Idle");
        Debug.Log("Clinch Start! (Tsukami-kaishi - 掴み開始)");
    }

    private void UpdateClinchMovement()
    {
        // Use your blend tree parameters for strafing while grabbing
        // Assuming InputX/Z are handled by your MovementComponent
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        _animator.SetFloat("ClinchInputX", x);
        _animator.SetFloat("ClinchInputZ", z);
    }

    public void ExecuteClinchLight()
    {
        if (!_isClinching || _combat.currentStyle.clinchKnee == null) return;

        // Reset timer on attack to allow for a few hits
        _clinchTimer = Mathf.Max(0, _clinchTimer - 0.5f);
        _combat.ExecuteCustomMove(_combat.currentStyle.clinchKnee);
    }

    public void ExecuteClinchThrow()
    {
        if (!_isClinching || _combat.currentStyle.clinchThrow == null) return;

        // Play the throw move and clean up state
        _combat.ExecuteCustomMove(_combat.currentStyle.clinchThrow);
        Invoke(nameof(EndClinch), 0.1f); // Brief delay for animation to start
    }

    public void EndClinch()
    {
        if (!_isClinching) return;

        if (_grabbedEnemy != null)
        {
            _grabbedEnemy.SetParent(null);
            _grabbedEnemy = null;
        }

        _isClinching = false;
        _movement.canRotate = true;
        // _movement.speedMultiplier = 1.0f;

        _animator.Play("Idle"); // Or your transition out
    }
}