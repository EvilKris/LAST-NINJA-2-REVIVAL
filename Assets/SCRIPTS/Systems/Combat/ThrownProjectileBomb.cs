using UnityEngine;
using JSAM;

/// <summary>
/// Attached at runtime to a thrown weapon instance. Handles collision detection,
/// smoke explosion VFX/SFX, and self-destruction on impact.
/// </summary>
public class ThrownProjectileBomb : MonoBehaviour
{
    private Transform _ownerRoot;
    private Collider[] _colliders;
    private float _colliderEnableTime;
    private bool _collidersEnabled;
    private bool _hasImpacted;

    private const float COLLIDER_DELAY = 0.5f;

    public void Launch(Transform ownerRoot, Rigidbody rb, Collider[] colliders, float throwForce)
    {
        _ownerRoot = ownerRoot;
        _colliders = colliders;

        transform.SetParent(null);

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.mass = 1f;

        // Calculate a lobbed velocity that lands ~throwForce metres ahead
        float gravity = Physics.gravity.magnitude;
        float arcHeight = 1.5f;
        float vy = Mathf.Sqrt(2f * gravity * arcHeight);
        float airTime = 1.1f * vy / gravity;
        float vz = throwForce / airTime;

        Vector3 velocity = ownerRoot.forward * vz + Vector3.up * vy;
        rb.linearVelocity = velocity;

        _colliderEnableTime = Time.time + COLLIDER_DELAY;
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

        if (bank.smokeExplosion != null)
        {
            GameObject vfx = Instantiate(bank.smokeExplosion, transform.position, Quaternion.LookRotation(Vector3.up));
            ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
            if (ps == null || !ps.main.stopAction.Equals(ParticleSystemStopAction.Destroy))
                Destroy(vfx, 3f);
        }

        if (bank.smokeExplosion_sfx != null)
        {
            AudioManager.PlaySound(bank.smokeExplosion_sfx);
        }
    }
}
