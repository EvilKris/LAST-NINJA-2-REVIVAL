using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Placed on the shuriken explosion VFX prefab. Uses the trigger collider already present
/// on the prefab to detect nearby entities and apply damage with a
/// <see cref="HitReactionType.Light_Stun"/> reaction.
/// Each entity is only processed once per explosion.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ShurikenExplosionEffect : MonoBehaviour
{
    [Tooltip("Damage dealt to each entity caught in the blast.")]
    public float damage = 10f;

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

        health.TakeDamage(damage, HitReactionType.Light_Stun);
    }
}
