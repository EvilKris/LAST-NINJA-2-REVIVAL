using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main player controller that handles input from Unity's new Input System.
/// Coordinates between movement, combat, and the global Inventory system via MasterSingleton.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    // Component references
    private CombatHandler _combat;
    private PlayerControls _controls; // Auto-generated Input Actions class
    private MovementComponent _movement;
    private HealthComponent _health;

    // Attack hold mechanic (hold for medium attack, tap for light)
    private float _attackHoldTimer;
    private bool _isHoldingAttack;

    // Camera and world interaction
    private Camera _mainCamera;
    private int _floorLayerMask;

    // Cached input values from Input System events
    private Vector2 _moveInput;
    private float _cachedCameraYaw; // Camera rotation locked while moving
    private bool _wasMovingLastFrame; // Track movement state changes

    private ITargetable _currentTarget; // Our current focus
    [SerializeField] private float lockBreakDistance = 1.0f;
    [SerializeField] private float searchRadius = 3.5f;

    /// <summary>
    /// Initialize component references and create Input System instance.
    /// </summary>
    private void Awake()
    {
        _movement = GetComponent<MovementComponent>();
        _health = GetComponent<HealthComponent>();
        _combat = GetComponent<CombatHandler>();
        _mainCamera = Camera.main;
        _floorLayerMask = LayerMask.GetMask("Floor");

        _controls = new PlayerControls();
    }

    /// <summary>
    /// Enable input actions and subscribe to Input System events.
    /// </summary>
    private void OnEnable()
    {
        // Enable both the Player and UI action maps
        _controls.Player.Enable();
        _controls.UI.Enable();

        // Movement input caching
        _controls.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _controls.Player.Move.canceled += ctx => _moveInput = Vector2.zero;

        // Combat - Subscribe to attack and ability inputs
        _controls.Player.LightAttack.started += OnLightAttackStarted;
        _controls.Player.LightAttack.canceled += OnLightAttackCanceled;
        _controls.Player.HeavyAttack.started += OnHeavyAttackStarted;
        _controls.Player.Block.started += OnBlockInput;
        _controls.Player.Block.canceled += OnBlockInput;
        _controls.Player.KIButton.started += OnKIInput;
        _controls.Player.Acrobatics.started += OnAcrobatics;

        // Inventory Switching - Accessing via MasterSingleton
        // This coordinates with your _controls.UI.SwitchWeapons and SwitchItems inputs
        _controls.UI.SwitchWeapons.started += ctx => MasterSingleton.Instance.InventoryManager.CycleWeapon();
        _controls.UI.SwitchItems.started += ctx => MasterSingleton.Instance.InventoryManager.CycleItem();
    }

    private void OnDisable()
    {
        _controls.Player.Disable();
        _controls.UI.Disable();
    }

    private void Update()
    {
        if (_isHoldingAttack)
            _attackHoldTimer += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (_health != null && _health.IsDead) return;

        bool isMoving = _moveInput.sqrMagnitude > 0.01f;

        // Update camera yaw only when transitioning state
        if (_wasMovingLastFrame && !isMoving)
            _cachedCameraYaw = GetCurrentCameraYaw();
        else if (!_wasMovingLastFrame && isMoving)
            _cachedCameraYaw = GetCurrentCameraYaw();

        _wasMovingLastFrame = isMoving;

        // Convert input to camera-relative direction
        Vector3 moveDir = GetCameraRelativeDirection(_moveInput);

        // Lock-On Logic: Distance Check
        if (_currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, _currentTarget.GetLockOnPoint().position);
            if (dist > lockBreakDistance || !_currentTarget.IsValidTarget())
            {
                _currentTarget = null;
            }
        }

        // Drive the MovementComponent
        if (_currentTarget != null)
        {
            _movement.ProcessMovement(moveDir, _currentTarget.GetLockOnPoint().position);
        }
        else
        {
            if (_combat.IsAttacking)
            {
                if (_combat.CanRotateDuringAttack && isMoving)
                    _movement.RotateTowardsDirection(moveDir);

                _movement.ProcessMovement(Vector3.zero);
            }
            else if (isMoving)
            {
                _movement.ProcessMovement(moveDir);
                _movement.RotateTowardsDirection(moveDir);
            }
            else
            {
                _movement.ProcessMovement(Vector3.zero);
            }
        }
    }

    private void TryLockOn()
    {
        if (_currentTarget != null) return;

        Collider[] enemies = Physics.OverlapSphere(transform.position, searchRadius);
        foreach (var col in enemies)
        {
            if (col.TryGetComponent<ITargetable>(out var target))
            {
                if (target.GetFaction() == Faction.Enemy && target.IsValidTarget())
                {
                    _currentTarget = target;
                    break;
                }
            }
        }
    }

    private float GetCurrentCameraYaw()
    {
        if (CameraZoneManager.Instance != null && CameraZoneManager.Instance.GetCurrentCamera() != null)
        {
            return CameraZoneManager.Instance.GetCurrentCamera().transform.eulerAngles.y;
        }
        return _mainCamera.transform.eulerAngles.y;
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Quaternion yRotation = Quaternion.Euler(0f, _cachedCameraYaw, 0f);
        Vector3 camForward = yRotation * Vector3.forward;
        Vector3 camRight = yRotation * Vector3.right;
        return (camForward * input.y + camRight * input.x).normalized;
    }

    // --- Combat Callbacks ---

    private void OnLightAttackStarted(InputAction.CallbackContext _)
    {
        TryLockOn();
        _isHoldingAttack = true;
        _attackHoldTimer = 0f;
    }

    private void OnLightAttackCanceled(InputAction.CallbackContext _)
    {
        _isHoldingAttack = false;
        if (_attackHoldTimer >= 1.0f)
            _combat.ExecuteMediumAttack();
        else
            _combat.ExecuteLightAttack();
    }

    private void OnHeavyAttackStarted(InputAction.CallbackContext _)
    {
        TryLockOn();
        _isHoldingAttack = false;
        _combat.ExecuteHeavyAttack();
    }

    private void OnBlockInput(InputAction.CallbackContext context) => _combat.SetBlocking(context.started);

    private void OnAcrobatics(InputAction.CallbackContext context)
    {
        if (!context.started) return;
        if (_moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 dir = GetCameraRelativeDirection(_moveInput);
            _movement.RotateTowardsDirection(dir);
        }
        _combat.ExecuteAcrobatics();
    }

    public void OnKIInput(InputAction.CallbackContext _) => _combat.HandleKIInput();
}