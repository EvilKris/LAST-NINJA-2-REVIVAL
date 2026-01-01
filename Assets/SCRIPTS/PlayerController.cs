using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private MovementComponent _movement;
    private HealthComponent _health;
    private CombatHandler _combat;
    private Camera _mainCamera;
    private int _floorLayerMask;

    private Vector2 _moveInput;
    private Vector2 _mousePosition;

    private void Awake()
    {
        _movement = GetComponent<MovementComponent>();
        _health = GetComponent<HealthComponent>();
        _combat = GetComponent<CombatHandler>();

        _mainCamera = Camera.main;
        _floorLayerMask = LayerMask.GetMask("Floor");
    }

    private void FixedUpdate()
    {
        // Safety check for Health (Hoken - 保険)
        if (_health != null && _health.IsDead)
        {
            _movement.ProcessMovement(Vector2.zero);
            return;
        }

        // 1. Handle Movement
        _movement.ProcessMovement(_moveInput);

        // 2. Handle Rotation
        HandleRotation();
    }

    private void HandleRotation()
    {
        Ray ray = _mainCamera.ScreenPointToRay(_mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, _floorLayerMask))
        {
            _movement.RotateTowardsPoint(hit.point);
        }
    }

    // --- SendMessages Callbacks (Jidō Nyūryoku - 自動入力) ---

    // Matches "Move" action in Input Asset
    private void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();
    }

    // Matches "Look" action (Mouse Position)
    private void OnLook(InputValue value)
    {
        _mousePosition = value.Get<Vector2>();
    }

    // Matches "Attack" action (Light Attack)
    private void OnAttack()
    {
        if (_health != null && _health.IsDead) return;
        if (_combat != null) _combat.ExecuteLightAttack();
    }

    // Matches "HeavyAttack" action
    private void OnHeavyAttack()
    {
        if (_health != null && _health.IsDead) return;
        if (_combat != null) _combat.ExecuteHeavyAttack();
    }
}