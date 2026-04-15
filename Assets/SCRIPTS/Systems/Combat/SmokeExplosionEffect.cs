using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Placed on the smoke explosion VFX prefab. Uses the trigger collider already present on
/// the prefab to detect nearby entities and apply a <see cref="HitReactionType.Heavy_Stun"/>.
/// Each entity is only processed once per explosion — if the entity is already stunned the
/// countdown is simply reset via <see cref="HealthComponent.StartStun"/>.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SmokeExplosionEffect : MonoBehaviour
{
    [Tooltip("Damage dealt to each entity caught in the blast.")]
    public float damage = 5f;

    private readonly HashSet<Transform> _affected = new();

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Transform root = other.transform.root;

        if (_affected.Contains(root)) return;

        HealthComponent health = root.GetComponentInChildren<HealthComponent>();
        if (health == null || health.IsDead) return;

        _affected.Add(root);

        if (health.IsStunned)
        {
            // Already stunned — just restart the countdown
            health.StartStun();
        }
        else
        {
            health.TakeDamage(damage, HitReactionType.Heavy_Stun);
        }
    }
}
