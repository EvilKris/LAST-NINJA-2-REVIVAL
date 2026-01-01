using UnityEngine;

public class CombatHitbox : MonoBehaviour
{
    public HitboxType hitboxType;

    // 1. Change this from string to HitboxType (Rekkyō-gata no shūsei - 列挙型の修正)
   
    public HitboxType HitboxType => hitboxType;

    [SerializeField] private GameObject hitEffectPrefab;

    private CombatHandler _owner;
    private float _currentDamage;
    private Collider _collider;

    private void Awake()
    {
        _owner = GetComponentInParent<CombatHandler>();
        _collider = GetComponent<Collider>();

        if (_collider)
        {
            _collider.isTrigger = true;
            _collider.enabled = false;
        }
    }

    public void SetDamage(float damage) => _currentDamage = damage;

    public void Activate()
    {
        if (_owner != null) _owner.ClearHitCache();
        if (_collider) _collider.enabled = true;
    }

    public void Deactivate()
    {
        if (_collider) _collider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_owner == null) return;

        IDamageable victim = other.GetComponentInParent<IDamageable>();

        if (victim != null && !victim.IsDead)
        {
            Transform victimRoot = other.transform.root;

            if (victimRoot == _owner.transform) return;

            if (!_owner.HasHitTarget(victimRoot))
            {
                victim.TakeDamage(_currentDamage);
                _owner.RegisterHit(victimRoot);

                if (hitEffectPrefab != null)
                {
                    Vector3 contactPoint = other.ClosestPoint(transform.position);
                    Instantiate(hitEffectPrefab, contactPoint, Quaternion.identity);
                }

                Debug.Log($"{_owner.name} hit {victimRoot.name}!");
            }
        }
    }
}