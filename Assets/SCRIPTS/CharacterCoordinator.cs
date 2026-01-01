using UnityEngine;

public class CharacterCoordinator : MonoBehaviour
{
    private HealthComponent _health;
    private Animator _animator;

    private void Awake()
    {
        _health = GetComponent<HealthComponent>();
        _animator = GetComponent<Animator>();

        // Subscribe to events (Kōdoku - 購読)
        _health.OnHit += () => _animator.SetTrigger("GetHitTrigger");
        _health.OnDeath += () => _animator.SetTrigger("DieTrigger");
    }
}