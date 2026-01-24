using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField]private GameObject inGameUIOverlay; //the main in-game UI overlay

    [Tooltip("The health overlay UI element")]
    [SerializeField]private GameObject healthOverlay;
    [SerializeField]private Image playerHealthUI;
    [SerializeField]private Image enemyHealthUI;

    private Material playerHealthMaterial;
    private Material enemyHealthMaterial;

    private void Awake()
    {
        // Create material instances to avoid shared material issues
        if (playerHealthUI != null && playerHealthUI.material != null)
        {
            playerHealthMaterial = new Material(playerHealthUI.material);
            playerHealthUI.material = playerHealthMaterial;
        }

        if (enemyHealthUI != null && enemyHealthUI.material != null)
        {
            enemyHealthMaterial = new Material(enemyHealthUI.material);
            enemyHealthUI.material = enemyHealthMaterial;
        }
    }

    private void Start()
    {
        ActivateInGameOverlay();
        SubscribeToAllHealthComponents();
    }

    private void ActivateInGameOverlay()
    {
     //   if(inGameUIOverlay != null)
     //inGameUIOverlay.SetActive(true);
        
        if(healthOverlay != null)
            healthOverlay.SetActive(true);  
    }

    private void SubscribeToAllHealthComponents()
    {
        HealthComponent[] allHealthComponents = Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
        foreach (HealthComponent health in allHealthComponents)
        {
            health.OnHealthChanged += OnHealthChanged;
        }
    }

    private void OnHealthChanged(float currentHealth, float maxHealth, Faction faction)
    {
        float normalizedHealth = maxHealth > 0 ? currentHealth / maxHealth : 0;

        if (faction == Faction.Player && playerHealthMaterial != null)
        {
            playerHealthMaterial.SetFloat("_FillAmount", normalizedHealth);
        }
        else if (faction == Faction.Enemy && enemyHealthMaterial != null)
        {
            //Debug.Log($"Updating enemy health UI: {normalizedHealth}");
            enemyHealthMaterial.SetFloat("_FillAmount", normalizedHealth);
        }
    }

    private void OnDestroy()
    {
        // Clean up material instances
        if (playerHealthMaterial != null)
        {
            Destroy(playerHealthMaterial);
        }
        if (enemyHealthMaterial != null)
        {
            Destroy(enemyHealthMaterial);
        }

        HealthComponent[] allHealthComponents = Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
        foreach (HealthComponent health in allHealthComponents)
        {
            health.OnHealthChanged -= OnHealthChanged;
        }
    }

    public void UICamShake(RectTransform canvasRect, float duration = 0.3f, float strength = 30f, int vibrato = 10)
    {
        // Kill any existing shake to prevent overlapping
        canvasRect.DOKill();

        // Shake it!
        canvasRect.DOShakeAnchorPos(duration, strength, vibrato);
    }
}

