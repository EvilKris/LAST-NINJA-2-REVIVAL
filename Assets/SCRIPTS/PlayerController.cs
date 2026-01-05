using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private CombatHandler _combat;
    private PlayerControls _controls;
    private MovementComponent _movement;
    private HealthComponent _health;

    private float _attackHoldTimer;
    private bool _isHoldingAttack;

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

        _controls = new PlayerControls();
    }

    private void OnEnable()
    {
        _controls.Player.Enable();

        // Movement & Look (New C# Event Subscriptions)
        _controls.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _controls.Player.Move.canceled += ctx => _moveInput = Vector2.zero;

        _controls.Player.Look.performed += ctx => _mousePosition = ctx.ReadValue<Vector2>();

        // Combat
        _controls.Player.LightAttack.started += OnLightAttackStarted;
        _controls.Player.LightAttack.canceled += OnLightAttackCanceled;
        _controls.Player.HeavyAttack.started += OnHeavyAttackStarted;
        _controls.Player.Block.started += OnBlockInput;
        _controls.Player.Block.canceled += OnBlockInput;
        _controls.Player.KIButton.started += OnKIInput;
        _controls.Player.Acrobatics.started += OnAcrobatics;
    }

    private void OnDisable()
    {
        _controls.Player.Disable();
    }

    private void Update()
    {
        if (_isHoldingAttack)
            _attackHoldTimer += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (_health != null && _health.IsDead) return;

        _movement.ProcessMovement(_moveInput);
        HandleRotation();
    }

    private void HandleRotation()
    {
        // Using the stored _mousePosition from the C# event
        Ray ray = _mainCamera.ScreenPointToRay(_mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, _floorLayerMask))
        {
            _movement.RotateTowardsPoint(hit.point);
        }
    }

    // --- Combat Callbacks ---

    private void OnLightAttackStarted(InputAction.CallbackContext _)
    {
        _isHoldingAttack = true;
        _attackHoldTimer = 0f;
    }

    private void OnLightAttackCanceled(InputAction.CallbackContext _)
    {
        _isHoldingAttack = false;
        if (_attackHoldTimer >= 1.0f) _combat.ExecuteMediumAttack();
        else _combat.ExecuteLightAttack();
    }

    private void OnHeavyAttackStarted(InputAction.CallbackContext _)
    {
        // Design note: Heavy attacks reset the hold timer to prevent accidental Light releases
        _isHoldingAttack = false;
        _combat.ExecuteHeavyAttack();
    }

    private void OnBlockInput(InputAction.CallbackContext context)
    {
        _combat.SetBlocking(context.started);
    }

    public void OnKIInput(InputAction.CallbackContext _) => _combat.HandleKIInput();

    private void OnAcrobatics(InputAction.CallbackContext _)
    {
        // Next step: Implement directional flip logic
    }
}