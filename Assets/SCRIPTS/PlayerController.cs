using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private CombatHandler _combat;
    private PlayerControls _controls;

    private float _attackHoldTimer;
    private bool _isHoldingAttack;

    private void Awake()
    {
        _combat = GetComponent<CombatHandler>();
        _controls = new PlayerControls();
    }

    private void OnEnable()
    {
        _controls.Player.Enable();

        // Fix 1: Use a specific Start method for Light/Mid to track the timer
        _controls.Player.LightAttack.started += OnLightAttackStarted;
        _controls.Player.LightAttack.canceled += OnLightAttackCanceled;

        // Heavy Attack is usually a tap, so we call it directly on started
        _controls.Player.HeavyAttack.started += OnHeavyAttackStarted;

        // Fix 2: Remove the (true/false). The method handles the logic.
        _controls.Player.Block.started += OnBlockInput;
        _controls.Player.Block.canceled += OnBlockInput;

        _controls.Player.KIButton.started += OnKIInput;
        _controls.Player.Acrobatics.started += OnAcrobatics;
    }

    private void OnAcrobatics(InputAction.CallbackContext _)
    {
        throw new NotImplementedException();
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

    private void OnHeavyAttackStarted(InputAction.CallbackContext _)
    {
        _isHoldingAttack = true;
        _attackHoldTimer = 0f;
        _combat.ExecuteHeavyAttack();
    }

    private void OnLightAttackStarted(InputAction.CallbackContext _)
    {
        _isHoldingAttack = true;
        _attackHoldTimer = 0f;
        // We don't call _combat yet! We wait for OnAttackCanceled to see if it's Light or Mid.
    }
    private void OnLightAttackCanceled(InputAction.CallbackContext _)
    {
        _isHoldingAttack = false;

        if (_attackHoldTimer >= 1.0f)
            _combat.ExecuteMediumAttack();
        else
            _combat.ExecuteLightAttack();
    }

    public void OnKIInput(InputAction.CallbackContext _)
    {
        // We only trigger on 'started' (initial press) to spend the bar once
        
            _combat.HandleKIInput();        
    }

    // Linked to the "Block" Action (O Key)
    public void OnBlockInput(InputAction.CallbackContext context)
    {
        // Fix 3: Read the 'started' state to determine if we are pressing or releasing
        _combat.SetBlocking(context.started);
    }
}
