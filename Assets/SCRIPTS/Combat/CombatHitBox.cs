using UnityEngine;

public class CombatHitbox : MonoBehaviour
{
    [SerializeField] private string hitboxID; // Set this in Inspector (e.g., "Katana")
    public string HitboxID => hitboxID;

    private BaseCreature _owner;
    private float _currentDamage;
    private Collider _collider;

    private void Awake()
    {
        _owner = GetComponentInParent<BaseCreature>();
        _collider = GetComponent<Collider>();
        if (_collider) _collider.enabled = false;
    }

    public void SetDamage(float damage) => _currentDamage = damage;

    // These are now called by the Owner, not directly by the Animation Event
    public void Activate()
    {
        _owner.ClearHitCache();
        if (_collider) _collider.enabled = true;
    }

    public void Deactivate()
    {
        if (_collider) _collider.enabled = false;
    }


    private void OnTriggerEnter(Collider other)
    {
        IDamageable victim = other.GetComponentInParent<IDamageable>();

        if (victim != null && !victim.IsDead)
        {
            Transform victimRoot = other.transform.root;

            // Prevent self-hitting
            if (victimRoot == _owner.transform) return;

            // Cache check (Ichido kiri - 一度きり)
            if (!_owner.HasHitTarget(victimRoot))
            {
                victim.TakeDamage(_currentDamage);
                _owner.RegisterHit(victimRoot);

                // Feedback: You can trigger a Hit-Stop or Particle effect here
                Debug.Log($"{_owner.name} landed a hit on {victimRoot.name}!");
            }
        }
    }
}