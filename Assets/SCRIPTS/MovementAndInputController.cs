using UnityEngine;
using UnityEngine.InputSystem;

public class MovementAndInputController : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 60f;

    private PlayerControls _input; // The generated C# class 
    private Vector2 _mouseScreenPosition = Vector2.zero;
    private Vector2 _userVector = Vector2.zero;

    private BaseCreature _baseCreature;
    private Rigidbody _rb;
    private Animator _animator;
    private Camera _mainCamera;
    private int _layerMask;

    readonly string isRunning = "isRunningBool";
    readonly string anim_xAxis = "Input_XFloat";
    readonly string anim_yAxis = "Input_YFloat";

    // Inside MovementAndInputController
    private CombatHandler _combat;

    private void Awake()
    {
        _baseCreature = GetComponentInParent<BaseCreature>();
        Debug.Assert(_baseCreature != null, $"You need BaseCreature on {this.name}!");

        _rb = _baseCreature.rb;
        _animator = _baseCreature.animator;

        // Initialize the input class
        _input = new PlayerControls();
        _combat = GetComponent<CombatHandler>();

    }

    private void OnEnable()
    {
        // Subscribe to events 
        _input.Player.Move.performed += OnMoveCallBack;
        _input.Player.Move.canceled += OnMoveCallBack;
        _input.Player.Look.performed += OnLookCallBack;
        _input.Player.Attack.performed += OnAttackCallBack;
        _input.Player.SubWeapon.performed += OnSubWeaponCallBack;

        _input.Enable();
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        _input.Player.Move.performed -= OnMoveCallBack;
        _input.Player.Move.canceled -= OnMoveCallBack;
        _input.Player.Look.performed -= OnLookCallBack;
        _input.Player.Attack.performed -= OnAttackCallBack;
        _input.Player.SubWeapon.performed -= OnSubWeaponCallBack;

        _input.Disable();
    }

    private void Start()
    {
        _layerMask = LayerMask.GetMask("Floor");
        _mainCamera = Camera.main;
    }

    void FixedUpdate()
    {
        if (!_baseCreature.isAbleToMove) return;

        RotateThisObject();
        Move();
    }

    #region Input Event Handlers
    private void OnMoveCallBack(InputAction.CallbackContext context)
    {
        _userVector = context.ReadValue<Vector2>();
        _animator.SetFloat(anim_xAxis, _userVector.x);
        _animator.SetFloat(anim_yAxis, _userVector.y);
    }

    private void OnLookCallBack(InputAction.CallbackContext context)
    {
        _mouseScreenPosition = context.ReadValue<Vector2>();
    }



    public void OnAttackCallBack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _combat.ExecuteLightAttack(); // Player triggers it
        }
    }
    /*
    private void OnAttackCallBack(InputAction.CallbackContext context)
    {

        // Only trigger on "performed" (the press)
        _animator.SetTrigger("AttackTrigger");
        Debug.Log("Katana Slash! ");
    }*/

    private void OnSubWeaponCallBack(InputAction.CallbackContext context)
    {
        _animator.SetTrigger("ThrowTrigger");
        Debug.Log("Shuriken Throw!");
    }
    #endregion

    #region Movement Logic
    private void Move()
    {
        bool isMoving = _userVector.sqrMagnitude > 0.01f;

        if (isMoving && _mainCamera != null)
        {
            Vector3 forward = _mainCamera.transform.forward;
            Vector3 right = _mainCamera.transform.right;
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDirection = (forward * _userVector.y + right * _userVector.x).normalized;
            _rb.linearVelocity = moveDirection * _baseCreature.movementSpeed;
        }
        else
        {
            _rb.linearVelocity = Vector3.zero;
        }

        _animator.SetBool(isRunning, isMoving);
    }

    private void RotateThisObject()
    {
        if (_mainCamera == null) return;

        Ray ray = _mainCamera.ScreenPointToRay(_mouseScreenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, _layerMask))
        {
            Vector3 targetDir = hit.point - transform.position;
            targetDir.y = 0;

            if (targetDir.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDir);
                _rb.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime * 10f);
            }
        }
    }
    #endregion
}