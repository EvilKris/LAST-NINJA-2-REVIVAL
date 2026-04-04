
using System.Collections;
using UnityEngine;
using UnityEngine.Events;



[RequireComponent(typeof(Collider))]
public class TriggerDetectorManager : MonoBehaviour
{

    [System.Serializable]
    public enum TriggerEventType
    {
        None = 0,
        Worship_At_Altar_Trigger = 10,
        Close_To_Dragon = 20,
        Death_By_Drowning = 50,
        CustomB = 60
    }

    [Header("Trigger Configuration")]
    [Tooltip("Choose which named event this trigger represents. Used by handlers to determine behavior.")]
    [SerializeField]
    private TriggerEventType triggerEventType = TriggerEventType.None;


/// <summary>
/// Interface for components that respond to player trigger events
/// </summary>


/// <summary>
/// Handles player detection in triggers and notifies handlers via composition.
/// Add this component alongside your custom trigger behavior components.
/// </summary>

    [Header("Player Detection")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private string playerTag = "Player";
    [Header("Cooldown")]
    [Tooltip("Cooldown duration in seconds after worship before the trigger reactivates.")]
    [SerializeField] private float worshipCooldownSeconds = 20f;

    [Header("Events")]
    public UnityEvent<GameObject> onEntityEnter;
    public UnityEvent<GameObject> onEntityExit;
    public UnityEvent<GameObject> onEntityStay;

    private GameObject currentPlayer;
    private bool playerInTrigger = false;
    private IPlayerTriggerHandler[] handlers;
    private bool isInWorship;
    private Collider _triggerCollider;
    private Coroutine _cooldownCoroutine;

    private void Awake()
    {
        // Ensure the collider is set to trigger
        _triggerCollider = GetComponent<Collider>();
        Collider col = _triggerCollider;
        if (!col.isTrigger)
        {
            Debug.LogWarning($"{gameObject.name}: Collider is not set as trigger. Setting it now.");
            col.isTrigger = true;
        }

        // Find all handlers on this GameObject
        handlers = GetComponents<IPlayerTriggerHandler>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other.gameObject))
        {
            currentPlayer = other.gameObject;
            playerInTrigger = true;

            // Notify all handlers
            foreach (var handler in handlers)
            {
                handler?.OnPlayerEnter(other.gameObject);
            }

            onEntityEnter?.Invoke(other.gameObject);

            switch(triggerEventType)
            {
                case TriggerEventType.Worship_At_Altar_Trigger:
                    if (isInWorship)
                        return;
                    // Disable collider immediately to prevent re-entry
                    if (_triggerCollider != null)
                        _triggerCollider.enabled = false;
                    BeginWorshipSequence(other.gameObject);
                    break;
                    // Handle other trigger types as needed
            }

        }
        else
        {             // Optionally, handle non-player entities here if needed
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other.gameObject) && other.gameObject == currentPlayer)
        {
            playerInTrigger = false;

            // Notify all handlers
            foreach (var handler in handlers)
            {
                handler?.OnPlayerExit(other.gameObject);
            }

            onEntityExit?.Invoke(other.gameObject);

            currentPlayer = null;
        }
        else
        {             // Optionally, handle non-player entities here if needed
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsPlayer(other.gameObject))
        {
            // Notify all handlers
            foreach (var handler in handlers)
            {
                handler?.OnPlayerStay(other.gameObject);
            }

            onEntityStay?.Invoke(other.gameObject);
        }
        else
        {             // Optionally, handle non-player entities here if needed
        }
    }

    /// <summary>
    /// Check if the GameObject is the player based on layer and tag
    /// </summary>
    private bool IsPlayer(GameObject obj)
    {
        bool isOnPlayerLayer = ((1 << obj.layer) & playerLayer) != 0;
        bool hasPlayerTag = obj.CompareTag(playerTag);

        return isOnPlayerLayer && hasPlayerTag;
    }

    /// <summary>
    /// Get the current player in the trigger (null if none)
    /// </summary>
    public GameObject GetCurrentPlayer()
    {
        return currentPlayer;
    }

    /// <summary>
    /// Check if player is currently in trigger
    /// </summary>
    public bool IsPlayerInTrigger()
    {
        return playerInTrigger;
    }

    /// <summary>
    /// Execute the configured trigger action. Currently only the altar worship
    /// action is implemented; other types can be handled here as needed.
    /// </summary>
    public void BeginWorshipSequence(GameObject player)
    {
        if (triggerEventType != TriggerEventType.Worship_At_Altar_Trigger)
        {
            Debug.LogWarning($"BeginWorshipSequence called on trigger of type {triggerEventType}. Ignoring.");
            return;
        }
        
        if (isInWorship)
            return;

        isInWorship = true;
        if (player != null)
        {
            // Trigger the worship sequence on the player and pass this trigger's forward
            // direction so the player can be rotated to face the same way.
            if (player.TryGetComponent<MovementComponent>(out var movement))
            {
                movement.BeginWorshipSequence(this, transform.forward);
            }
        }
    }

    /// <summary>
    /// Start a cooldown timer and re-enable the trigger collider after the given seconds.
    /// If seconds <= 0, uses the configured worshipCooldownSeconds.
    /// </summary>
    public void StartCooldown(float seconds = -1f)
    {
        if (seconds <= 0f) seconds = worshipCooldownSeconds;
        if (_cooldownCoroutine != null)
            StopCoroutine(_cooldownCoroutine);
        _cooldownCoroutine = StartCoroutine(CooldownRoutine(seconds));
    }

    private IEnumerator CooldownRoutine(float seconds)
    {
        // ensure collider remains disabled during cooldown
        if (_triggerCollider != null)
            _triggerCollider.enabled = false;

        isInWorship = true;

        yield return new WaitForSeconds(seconds);

        if (_triggerCollider != null)
            _triggerCollider.enabled = true;

        isInWorship = false;
        _cooldownCoroutine = null;
    }

    /// <summary>
    /// Returns the configured TriggerEventType for this trigger.
    /// </summary>
    public TriggerEventType GetTriggerEventType() => triggerEventType;


}