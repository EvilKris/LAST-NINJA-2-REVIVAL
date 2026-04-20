using UnityEngine;

using JSAM;

/// <summary>
/// Attached at runtime to a thrown shuriken instance. Flies straight forward
/// (no gravity arc), spawns the shuriken explosion VFX/SFX on impact, and
/// self-destructs. If nothing is hit within <see cref="LIFETIME"/> seconds
/// the projectile is destroyed automatically.
/// </summary>
public class ThrownProjectileShuriken : MonoBehaviour
{
    private Transform _ownerRoot;
    private Collider[] _colliders;
    private float _colliderEnableTime;
    private bool _collidersEnabled;
    private bool _hasImpacted;

    private const float COLLIDER_DELAY = 0.5f;
    private const float LIFETIME = 5f;

    public void Launch(Transform ownerRoot, Rigidbody rb, Collider[] colliders, float throwForce)
    {
        _ownerRoot = ownerRoot;
        _colliders = colliders;

        transform.SetParent(null);

        rb.isKinematic = false;
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.mass = 1f;

        rb.linearVelocity = ownerRoot.forward * throwForce;

        _colliderEnableTime = Time.time + COLLIDER_DELAY;

        Destroy(gameObject, LIFETIME);
    }

    private void Update()
    {
        if (!_collidersEnabled && Time.time >= _colliderEnableTime)
        {
            _collidersEnabled = true;
            if (_colliders != null)
            {
                foreach (Collider col in _colliders)
                {
                    if (col != null) col.enabled = true;
                }
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasImpacted) return;
        if (collision.transform.root == _ownerRoot) return;

        _hasImpacted = true;
        SpawnExplosion();
        Destroy(gameObject);
    }

    private void SpawnExplosion()
    {
        PrefabBankManager bank = MasterSingleton.Instance != null
            ? MasterSingleton.Instance.PrefabBankManager
            : null;

        if (bank == null) return;

        if (bank.shurikenHitPrefab != null)
        {
            GameObject vfx = Instantiate(bank.shurikenHitPrefab, transform.position, Quaternion.LookRotation(Vector3.up));
            ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
            if (ps == null || !ps.main.stopAction.Equals(ParticleSystemStopAction.Destroy))
                Destroy(vfx, 3f);
        }

        if (bank.shurikenHitSfx != null)
        {
            AudioManager.PlaySound(bank.shurikenHitSfx);
        }
    }
}
