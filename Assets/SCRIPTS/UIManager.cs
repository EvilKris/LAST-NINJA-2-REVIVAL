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

        if (faction == Faction.Player && playerHealthUI != null)
        {
            playerHealthUI.material.SetFloat("_FillAmount", normalizedHealth);
        }
        else if (faction == Faction.Enemy && enemyHealthUI != null)
        {
            //Debug.Log($"Updating enemy health UI: {normalizedHealth}");
            enemyHealthUI.material.SetFloat("_FillAmount", normalizedHealth);
        }
    }

    private void OnDestroy()
    {
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

