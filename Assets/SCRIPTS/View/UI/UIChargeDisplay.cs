using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.ComponentModel;

/// <summary>
/// Manages the UI display for combat charges, dynamically creating and updating charge pips
/// based on the target CombatHandler's current charge state and fighting style.
/// Optimized with event-based updates and object pooling to minimize per-frame overhead.
/// </summary>
public class UIChargeDisplay : MonoBehaviour
{
    [Header("Target")]
    /// <summary>The CombatHandler to monitor for charge state changes.</summary>
    private CombatHandler targetCombatHandler;

    [Header("Setup")]
    /// <summary>Template GameObject for charge pips (should contain an 'fg' child with an Image component).</summary>
    [SerializeField] private GameObject pipTemplate; // Drag the 'RadialSlider' here
    private static readonly List<GameObject> gameObjects = new();

    /// <summary>List of pip GameObjects for pooling and reuse.</summary>
    private List<GameObject> _pipPool = gameObjects;
    
    /// <summary>List of Image components representing each charge tier's fill visualization.</summary>
    private List<Image> _chargeFills = new();

    private void OnEnable()
    {
        // Subscribe to events when enabled
        if (targetCombatHandler != null)
        {
            SubscribeToEvents();
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from events when disabled to prevent memory leaks
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// Subscribe to CombatHandler events for charge state changes.
    /// </summary>
    private void SubscribeToEvents()
    {
        if (targetCombatHandler == null) return;

        targetCombatHandler.OnMaxChargesChanged += HandleMaxChargesChanged;
        targetCombatHandler.OnChargeStateChanged += HandleChargeStateChanged;
    }

    /// <summary>
    /// Unsubscribe from CombatHandler events.
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (targetCombatHandler == null) return;

        targetCombatHandler.OnMaxChargesChanged -= HandleMaxChargesChanged;
        targetCombatHandler.OnChargeStateChanged -= HandleChargeStateChanged;
    }

    /// <summary>
    /// Event handler called when the maximum number of charges changes (e.g., weapon switch).
    /// </summary>
    /// <param name="maxCharges">The new maximum charge count.</param>
    private void HandleMaxChargesChanged(int maxCharges)
    {
        RebuildPips(maxCharges);
    }

    /// <summary>
    /// Event handler called when the charge state changes.
    /// Only updates the affected pips instead of all pips every frame.
    /// </summary>
    /// <param name="currentTier">The current charge tier.</param>
    /// <param name="chargeProgress">The progress within the current tier (0-1).</param>
    private void HandleChargeStateChanged(int currentTier, float chargeProgress)
    {
        // Update fill amounts only for changed pips
        for (int i = 0; i < _chargeFills.Count; i++)
        {
            float newFillAmount;

            if (i < currentTier)
            {
                // Tiers below the current tier are fully charged
                newFillAmount = 1f;
            }
            else if (i == currentTier)
            {
                // Current tier shows partial fill based on charge progress
                newFillAmount = chargeProgress;
            }
            else
            {
                // Tiers above the current tier are empty
                newFillAmount = 0f;
            }

            // Only update if the value actually changed to minimize UI updates
            if (_chargeFills[i].fillAmount != newFillAmount)
            {
                _chargeFills[i].fillAmount = newFillAmount;
            }
        }
    }

    /// <summary>
    /// Rebuilds the charge pip UI elements to match the specified count using object pooling.
    /// Reuses existing pips when possible instead of destroying and creating new ones.
    /// </summary>
    /// <param name="count">The number of charge pips to display.</param>
    private void RebuildPips(int count)
    {
        // Deactivate all existing pips first
        foreach (var pip in _pipPool)
        {
            pip.SetActive(false);
        }
        _chargeFills.Clear();

        // Hide template and return if no charges needed
        pipTemplate.SetActive(count > 0);
        if (count <= 0) return;

        // Ensure we have enough pips in the pool
        while (_pipPool.Count < count)
        {
            GameObject newPip = Instantiate(pipTemplate, transform);
            _pipPool.Add(newPip);
        }

        // Activate and cache the required number of pips
        for (int i = 0; i < count; i++)
        {
            _pipPool[i].SetActive(true);
            
            // Cache the Image component reference
            Transform fgTransform = _pipPool[i].transform.Find("fg");
            if (fgTransform != null)
            {
                if (fgTransform.TryGetComponent<Image>(out var fillImage))
                {
                    fillImage.fillAmount = 0f; // Reset fill
                    _chargeFills.Add(fillImage);
                }
                else
                {
                    Debug.LogWarning($"Pip at index {i} has an 'fg' child but no Image component!");
                }
            }
            else
            {
                Debug.LogWarning($"Pip at index {i} is missing an 'fg' child!");
            }
        }
    }

    /// <summary>
    /// Sets a new target CombatHandler and subscribes to its events.
    /// </summary>
    /// <param name="newTarget">The CombatHandler to monitor for charge updates.</param>
    public void SetTarget(CombatHandler newTarget)
    {
        // Unsubscribe from old target
        UnsubscribeFromEvents();

        // Set new target
        targetCombatHandler = newTarget;

        // Subscribe to new target
        if (targetCombatHandler != null)
        {
            SubscribeToEvents();
            RebuildPips(targetCombatHandler.MaxCharges);
        }
        else
        {
            RebuildPips(0);
        }
    }
}