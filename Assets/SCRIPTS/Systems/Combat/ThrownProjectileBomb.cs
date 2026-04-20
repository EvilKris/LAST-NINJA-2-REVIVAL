using UnityEngine;
using JSAM;

/// <summary>
/// Attached at runtime to a thrown weapon instance. Handles collision detection,
/// smoke explosion VFX/SFX, and self-destruction on impact.
/// On hitting anything other than the floor layer the explosion fires immediately
/// and the projectile continues to fall under gravity until it lands on the floor,
/// at which point it is destroyed.
/// </summary>
public class ThrownProjectileBomb : MonoBehaviour
{
    private Transform _ownerRoot;
    private Collider[] _colliders;
    private float _colliderEnableTime;
    private bool _collidersEnabled;
    private bool _hasImpacted;
    private bool _hasExploded;
    private LayerMask _floorLayer;

    private const float COLLIDER_DELAY = 0.5f;

    public void Launch(Transform ownerRoot, Rigidbody rb, Collider[] colliders, float throwForce, LayerMask floorLayer)
    {
        _ownerRoot = ownerRoot;
        _colliders = colliders;
        _floorLayer = floorLayer;

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

        bool isFloor = (_floorLayer.value & (1 << collision.gameObject.layer)) != 0;

        if (isFloor)
        {
            // Landed on the floor — explode (if not already) and clean up
            _hasImpacted = true;
            if (!_hasExploded)
                SpawnExplosion();
            Destroy(gameObject);
        }
        else
        {
            // Hit an entity or non-floor surface — explode once, then keep falling
            if (!_hasExploded)
            {
                _hasExploded = true;
                SpawnExplosion();

                // Disable all colliders except for floor detection so the bomb
                // passes through everything else on its way down
                if (_colliders != null)
                {
                    foreach (Collider col in _colliders)
                    {
                        if (col != null) col.enabled = false;
                    }
                }

                // Re-enable only via a floor-layer-only trigger so we can detect landing
                Collider ownCollider = GetComponent<Collider>();
                if (ownCollider == null)
                    ownCollider = GetComponentInChildren<Collider>();
                if (ownCollider != null)
                {
                    ownCollider.enabled = true;
                    ownCollider.isTrigger = false;
                }
            }
        }
    }

    private void SpawnExplosion()
    {
        PrefabBankManager bank = MasterSingleton.Instance != null
            ? MasterSingleton.Instance.PrefabBankManager
            : null;

        if (bank == null) return;

        if (bank.smokeExplosionPrefab != null)
        {
            GameObject vfx = Instantiate(bank.smokeExplosionPrefab, transform.position, Quaternion.LookRotation(Vector3.up));
            ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
            if (ps == null || !ps.main.stopAction.Equals(ParticleSystemStopAction.Destroy))
                Destroy(vfx, 3f);
        }

        if (bank.smokeExplosionSfx != null)
        {
            AudioManager.PlaySound(bank.smokeExplosionSfx);
        }
    }
}
