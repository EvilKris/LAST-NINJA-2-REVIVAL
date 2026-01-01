using UnityEngine;
using System;

public class HealthComponent : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    public bool isInvulnerable = false;
    public bool IsDead => currentHealth <= 0;

    // Events let other scripts "react" to health changes (Kinō - 機能)
    public event Action OnHit;
    public event Action OnDeath;
    public event Action<float> OnHealthChanged;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || isInvulnerable) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnHealthChanged?.Invoke(currentHealth);

        if (IsDead)
        {
            OnDeath?.Invoke();
            Debug.Log($"{gameObject.name} has died. (Shibō - 死亡)");
        }
        else
        {
            OnHit?.Invoke();
        }
    }
}