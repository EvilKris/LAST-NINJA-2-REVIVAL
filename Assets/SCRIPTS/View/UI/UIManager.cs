using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central UI manager. Owns the in-game overlay, health bars, and charge meter.
/// Subscribes to <see cref="HealthComponent.OnHealthChanged"/> events from all entities
/// in the scene and routes updates to the correct UI widgets.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Tooltip("Root GameObject for the main in-game UI overlay.")]
    [SerializeField] private GameObject gameUIOverlay;

    [Tooltip("The health overlay UI element.")]
    [SerializeField] private GameObject healthOverlay;

    [Tooltip("Image component used to drive the player health shader's _FillAmount property.")]
    [SerializeField] private Image playerHealthUI;

    [Tooltip("GameObject that holds the EnemyHealthBarScript component.")]
    [SerializeField] private GameObject enemyHealthUI;

    [Tooltip("Charge meter display component.")]
    public UIChargeDisplay chargeMeter;

    // ── Private state ────────────────────────────────────────────────────────

    /// <summary>Per-instance material so the player health shader doesn't affect other users of the same asset.</summary>
    private Material _playerHealthMaterial;

    private EnemyHealthBarScript _enemyHealthBarScript;

    // ═══════════════════════════════════════════════════════════════════
    // Unity Lifecycle
    // ═══════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Instantiate a private material copy so we never mutate the shared asset
        if (playerHealthUI != null && playerHealthUI.material != null)
        {
            _playerHealthMaterial = new Material(playerHealthUI.material);
            playerHealthUI.material = _playerHealthMaterial;
        }

        if (enemyHealthUI != null)
            _enemyHealthBarScript = enemyHealthUI.GetComponent<EnemyHealthBarScript>();
    }

    private void Start()
    {
        SubscribeToAllHealthComponents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromAllHealthComponents();

        if (_playerHealthMaterial != null)
            Destroy(_playerHealthMaterial);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Public API
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Shows or hides the main in-game UI overlay.
    /// </summary>
    /// <param name="visible">True to show, false to hide.</param>
    public void ToggleInGameOverlay(bool visible)
    {
        if (gameUIOverlay != null)
            gameUIOverlay.SetActive(visible);
    }

    /// <summary>
    /// Triggers a DOTween anchor-position shake on the supplied <see cref="RectTransform"/>.
    /// Any in-progress shake is cancelled first to prevent compounding offsets.
    /// </summary>
    /// <param name="canvasRect">The RectTransform to shake.</param>
    /// <param name="duration">Total shake duration in seconds.</param>
    /// <param name="strength">Maximum displacement in pixels.</param>
    /// <param name="vibrato">Number of oscillations per second.</param>
    public void UICamShake(RectTransform canvasRect, float duration = 0.3f, float strength = 30f, int vibrato = 10)
    {
        canvasRect.DOKill();
        canvasRect.DOShakeAnchorPos(duration, strength, vibrato);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Health Event Handling
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Finds every <see cref="HealthComponent"/> in the scene and subscribes to its
    /// <see cref="HealthComponent.OnHealthChanged"/> event.
    /// Call once on Start; for dynamically spawned entities call this again or
    /// subscribe directly from the spawner.
    /// </summary>
    private void SubscribeToAllHealthComponents()
    {
        foreach (HealthComponent health in Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None))
            health.OnHealthChanged += OnHealthChanged;
    }

    /// <summary>Unsubscribes from all <see cref="HealthComponent"/> events still present in the scene.</summary>
    private void UnsubscribeFromAllHealthComponents()
    {
        foreach (HealthComponent health in Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None))
            health.OnHealthChanged -= OnHealthChanged;
    }

    /// <summary>
    /// Routes a health-change notification to the correct UI widget based on faction.
    /// </summary>
    /// <param name="currentHealth">Entity's current health value.</param>
    /// <param name="maxHealth">Entity's maximum health value.</param>
    /// <param name="faction">Faction of the entity that changed health.</param>
    private void OnHealthChanged(float currentHealth, float maxHealth, Faction faction)
    {
        float normalized = maxHealth > 0f ? currentHealth / maxHealth : 0f;

        if (faction == Faction.Player && _playerHealthMaterial != null)
            _playerHealthMaterial.SetFloat("_FillAmount", normalized);
        else if (faction == Faction.Enemy && _enemyHealthBarScript != null)
            _enemyHealthBarScript.UpdateHealthBar(normalized);
    }
}

