using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central UI manager. Owns the in-game overlay, health bars, and charge meter.
/// Entities call <see cref="RegisterHealthComponent"/> / <see cref="UnregisterHealthComponent"/>
/// when they spawn or are destroyed so the UI stays in sync regardless of scene timing.
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

    [Tooltip("Prefab for Inner Force icons in the UI, instantiated once per charge.")]
    [SerializeField] private RectTransform innerForceIconPrefabHolder;
    [SerializeField] private GameObject innerForceIconPrefabUI;


    [Tooltip("Prefab for life icons in the UI, instantiated once per life.")]
    [SerializeField] private RectTransform lifeIconPrefabHolder;
    [SerializeField] private GameObject lifeIconPrefabUI;

    [Header("Screen Effects")]

    [Tooltip("Blacken the screen out upon death")]
    [SerializeField] private GameObject screenFadePrefab;

    [Tooltip("Counters for Weapons and Items")]
    [SerializeField] private TextMeshProUGUI weaponCounter;
    [SerializeField] private TextMeshProUGUI itemCounter;




    /// <summary>The screen-fade prefab used by <see cref="DeathFadeCanvas"/> for death transitions.</summary>
    public GameObject ScreenFadePrefab => screenFadePrefab;

    // ── Private state ────────────────────────────────────────────────────────

    /// <summary>Per-instance material so the player health shader doesn't affect other users of the same asset.</summary>
    private Material _playerHealthMaterial;

    private EnemyHealthBarScript _enemyHealthBarScript;
    private readonly List<GameObject> _lifeIcons = new();
    private readonly List<GameObject> _innerForceIcons = new();
    private readonly List<Image> _innerForceFillImages = new();
    private readonly List<Image> _innerForcePulseImages = new();
    private bool[] _innerForcePulsing = System.Array.Empty<bool>();
    private HealthComponent _playerHealthComponent;
    private readonly HashSet<HealthComponent> _registeredHealthComponents = new();

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
        BuildLifeIcons();

        GameDataManager gdm = MasterSingleton.Instance.GameDataManager;
        gdm.OnLivesChanged += OnLivesChanged;

        InventoryManager inventory = MasterSingleton.Instance.InventoryManager;
        if (inventory != null)
        {
            inventory.OnWeaponChanged += OnWeaponChanged;
            inventory.OnItemChanged   += OnItemChanged;
        }

        SetCounterText(weaponCounter, 0);
        SetCounterText(itemCounter, 0);
    }

    private void OnDestroy()
    {
        // Unsubscribe from every component that is still alive
        foreach (HealthComponent health in _registeredHealthComponents)
        {
            if (health == null) continue;
            health.OnHealthChanged -= OnHealthChanged;
            if (health.faction == Faction.Player)
            {
                health.OnDeath -= OnPlayerDeath;
                health.OnInnerForceChanged -= OnInnerForceChanged;
            }
        }
        _registeredHealthComponents.Clear();

        if (_playerHealthMaterial != null)
            Destroy(_playerHealthMaterial);

        if (MasterSingleton.Instance != null)
        {
            MasterSingleton.Instance.GameDataManager.OnLivesChanged -= OnLivesChanged;

            InventoryManager inventory = MasterSingleton.Instance.InventoryManager;
            if (inventory != null)
            {
                inventory.OnWeaponChanged -= OnWeaponChanged;
                inventory.OnItemChanged   -= OnItemChanged;
            }
        }
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
    // Health Registration (called by HealthComponent)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registers a <see cref="HealthComponent"/> so its health changes are reflected in the UI.
    /// Call from <see cref="HealthComponent.Start"/> (or whenever the entity becomes active).
    /// </summary>
    public void RegisterHealthComponent(HealthComponent health)
    {
        if (health == null || !_registeredHealthComponents.Add(health)) return;

        health.OnHealthChanged += OnHealthChanged;

        if (health.faction == Faction.Player)
        {
            _playerHealthComponent = health;
            health.OnDeath += OnPlayerDeath;
            health.OnInnerForceChanged += OnInnerForceChanged;
        }

        Debug.Log($"[UIManager] Registered HealthComponent: '{health.gameObject.name}' (Faction: {health.faction})", health);
    }

    /// <summary>
    /// Unregisters a <see cref="HealthComponent"/> when it is destroyed or disabled.
    /// Call from <see cref="HealthComponent.OnDestroy"/>.
    /// </summary>
    public void UnregisterHealthComponent(HealthComponent health)
    {
        if (health == null || !_registeredHealthComponents.Remove(health)) return;

        health.OnHealthChanged -= OnHealthChanged;

        if (health.faction == Faction.Player)
        {
            health.OnDeath -= OnPlayerDeath;
            health.OnInnerForceChanged -= OnInnerForceChanged;
            if (_playerHealthComponent == health)
                _playerHealthComponent = null;
        }
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
        {
            _playerHealthMaterial.SetFloat("_FillAmount", normalized);
            playerHealthUI.SetMaterialDirty();
        }
        else if (faction == Faction.Enemy && _enemyHealthBarScript != null)
            _enemyHealthBarScript.UpdateHealthBar(normalized);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Life Icons
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Populates <see cref="lifeIconPrefabHolder"/> with one icon per life.
    /// Clears any existing icons first so it is safe to call on reset.
    /// </summary>
    private void BuildLifeIcons()
    {
        if (lifeIconPrefabHolder == null || lifeIconPrefabUI == null) return;

        foreach (GameObject icon in _lifeIcons)
            Destroy(icon);
        _lifeIcons.Clear();

        int lives = MasterSingleton.Instance.GameDataManager.Lives;
        for (int i = 0; i < lives; i++)
        {
            GameObject icon = Instantiate(lifeIconPrefabUI, lifeIconPrefabHolder);
            _lifeIcons.Add(icon);
        }
    }

    /// <summary>
    /// Called when <see cref="GameDataManager.Lives"/> changes.
    /// Removes the last icon to match the new count.
    /// </summary>
    private void OnLivesChanged(int newLives)
    {
        if (lifeIconPrefabHolder == null) return;

        while (_lifeIcons.Count > newLives)
        {
            int last = _lifeIcons.Count - 1;
            Destroy(_lifeIcons[last]);
            _lifeIcons.RemoveAt(last);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Inner Force Icons
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Populates <see cref="innerForceIconPrefabHolder"/> with one icon per Inner Force point.
    /// Clears any existing icons first so it is safe to call on reset.
    /// </summary>
    private void BuildInnerForceIcons(int count)
    {
        if (innerForceIconPrefabHolder == null || innerForceIconPrefabUI == null) return;

        // Kill any active colour tweens before destroying the icons
        foreach (Image img in _innerForcePulseImages)
            if (img != null) img.DOKill();

        foreach (GameObject icon in _innerForceIcons)
            Destroy(icon);
        _innerForceIcons.Clear();
        _innerForceFillImages.Clear();
        _innerForcePulseImages.Clear();
        _innerForcePulsing = new bool[count];

        for (int i = 0; i < count; i++)
        {
            GameObject icon = Instantiate(innerForceIconPrefabUI, innerForceIconPrefabHolder);
            _innerForceIcons.Add(icon);

            // The Image with fillAmount sits on the root of the prefab itself
            icon.TryGetComponent(out Image fillImage);
            _innerForceFillImages.Add(fillImage);

            // Fill-Graphic-FG is the overlay that pulses red while the bar is recharging
            Image pulseImage = null;
            Transform fgPulse = icon.transform.Find("Fill-Graphic-FG");
            if (fgPulse != null) fgPulse.TryGetComponent(out pulseImage);
            _innerForcePulseImages.Add(pulseImage);
        }
    }

    /// <summary>
    /// Called when the player's <see cref="HealthComponent.innerForcePoints"/> changes.
    /// Removes icons from the end to match the new count, or rebuilds if the count grew.
    /// </summary>
    private void OnInnerForceChanged(float[] fills)
    {
        if (fills == null || innerForceIconPrefabHolder == null) return;

        // Rebuild icons if the total bar count has changed
        if (fills.Length != _innerForceIcons.Count)
            BuildInnerForceIcons(fills.Length);

        for (int i = 0; i < fills.Length; i++)
        {
            // Update fill bar
            if (i < _innerForceFillImages.Count && _innerForceFillImages[i] != null)
                _innerForceFillImages[i].fillAmount = fills[i];

            // Drive the red pulse on Fill-Graphic-FG
            if (i >= _innerForcePulseImages.Count) continue;
            Image pulseImg = _innerForcePulseImages[i];
            if (pulseImg == null) continue;

            bool isFull     = fills[i] >= 1f;
            bool isPulsing  = i < _innerForcePulsing.Length && _innerForcePulsing[i];

            if (!isFull && !isPulsing)
            {
                // Bar was just spent or is mid-recharge — start red pulse loop
                if (i < _innerForcePulsing.Length) _innerForcePulsing[i] = true;
                pulseImg.DOKill();
                pulseImg.color = Color.white;
                pulseImg.DOColor(Color.red, 0.4f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
            else if (isFull && isPulsing)
            {
                // Bar fully recharged — stop pulsing and restore white
                if (i < _innerForcePulsing.Length) _innerForcePulsing[i] = false;
                pulseImg.DOKill();
                pulseImg.color = Color.white;
            }
        }
    }

    /// <summary>
    /// Called when the player entity dies. Triggers the full death sequence via <see cref="GameDataManager.HandlePlayerDeath"/>.
    /// </summary>
    private void OnPlayerDeath()
    {
        MovementComponent movement = _playerHealthComponent != null
            ? _playerHealthComponent.GetComponent<MovementComponent>()
            : null;
        MasterSingleton.Instance.GameDataManager.HandlePlayerDeath(movement);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Inventory Counters
    // ═══════════════════════════════════════════════════════════════════

    private void OnWeaponChanged(ItemData item)
    {
        int count = item != null ? item.count : 0;
        SetCounterText(weaponCounter, count);
    }

    /// <summary>
    /// Updates the weapon counter display with the current remaining ammo.
    /// Called by thrown-weapon handlers each time a projectile is consumed.
    /// </summary>
    public void UpdateWeaponCounter(int count)
    {
        SetCounterText(weaponCounter, count);
    }

    private void OnItemChanged(ItemData item)
    {
        int count = item != null ? item.count : 0;
        SetCounterText(itemCounter, count);
    }

    /// <summary>
    /// Updates a counter <see cref="TextMeshProUGUI"/> and shows or hides its parent
    /// <see cref="GameObject"/> depending on whether <paramref name="count"/> is greater than zero.
    /// </summary>
    private static void SetCounterText(TextMeshProUGUI label, int count)
    {
        if (label == null) return;
        label.text = count.ToString();
        label.transform.gameObject.SetActive(count > 0);
    }
}
