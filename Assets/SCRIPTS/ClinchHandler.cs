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
    private const float MAX_CLINCH_DURATION = 20f;

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

        // 1. Logic Overrides
        _movement.canRotate = false;

        // Set Player Animator Parameters
        _animator.SetBool("b_IsClinching", true);
        _animator.SetTrigger("t_ClinchStateStarted"); // Match your transition trigger

        // 2. Alignment: Smoothly snap Ninja to the front of the Enemy
        Vector3 targetPos = target.position + (target.forward * 0.8f);
        Quaternion targetRot = Quaternion.LookRotation(-target.forward);

        // Grab the Enemy's Animator to sync them
        if (target.TryGetComponent<Animator>(out var enemyAnim))
        {
            enemyAnim.SetBool("b_IsBeingGrabbed", true);
            enemyAnim.SetTrigger("t_ClinchStateStarted");
        }

        float elapsed = 0;
        float duration = 0.15f;

        while (elapsed < duration)
        {
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, targetPos, elapsed / duration), Quaternion.Slerp(transform.rotation, targetRot, elapsed / duration));

            // Face the enemy and make them face you
            target.rotation = Quaternion.Slerp(target.rotation, Quaternion.LookRotation(-transform.forward), elapsed / duration);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. Parenting: Physically link them for the walk cycle
        _grabbedEnemy.SetParent(transform);

        Debug.Log("Clinch Synced! (Kurinchi dōki - クリンチ同期)");
    }

    private void UpdateClinchMovement()
    {
        // localDir.x is side-to-side, localDir.z is forward-back
        Vector3 localDir = transform.InverseTransformDirection(_movement.currentMoveDir);

        // Update Player Animator
        _animator.SetFloat("Input_XFloat", localDir.x);
        _animator.SetFloat("Input_YFloat", localDir.z);

        // Update Enemy Animator to match leg movement
        if (_grabbedEnemy != null)
        {
            if (_grabbedEnemy.TryGetComponent<Animator>(out var enemyAnim))
            {
                enemyAnim.SetFloat("Input_XFloat", localDir.x);
                enemyAnim.SetFloat("Input_YFloat", localDir.z);
            }
        }
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

        // 1. Clean up Enemy State before unparenting
        if (_grabbedEnemy != null)
        {
            if (_grabbedEnemy.TryGetComponent<Animator>(out var enemyAnim))
            {
                enemyAnim.SetBool("b_IsBeingGrabbed", false);
                // Optionally trigger a 'released' or 'pushed' state here
            }

            _grabbedEnemy.SetParent(null);
            _grabbedEnemy = null;
        }

        // 2. Clean up Player State
        _isClinching = false;
        _animator.SetBool("b_IsClinching", false);

        _movement.canRotate = true;
        // _movement.speedMultiplier = 1.0f; // Restore full speed

        _animator.Play("Idle"); // Return to neutral
    }
}