using UnityEngine;

public class ClinchTrigger : MonoBehaviour
{
    private ClinchHandler _clinchHandler;
    public float grabDistance = 1.2f;

    void Update()
    {
        // Check for ClinchHandler if not assigned yet
        if (_clinchHandler == null)
        {
            _clinchHandler = GetComponent<ClinchHandler>();
            if (_clinchHandler == null)
                return;
        }

        // Only look for a grab if we aren't already clinching
        if (!_clinchHandler.IsClinching)
        {
            CheckProximity();
        }
    }

    private void CheckProximity()
    {
        Collider[] hits = new Collider[10];
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position + transform.forward * 0.5f, grabDistance, hits);

        for (int i = 0; i < hitCount; i++)
        {
            // Check if the collider has a HealthComponent and is an enemy
            HealthComponent health = hits[i].GetComponent<HealthComponent>();
            if (health == null || health.GetFaction() != Faction.Enemy)
                continue;

            // Check if we are facing them
            Vector3 dirToEnemy = (hits[i].transform.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, dirToEnemy) > 0.7f)
            {
                _clinchHandler.AttemptClinch(hits[i].transform);
                break;
            }
        }
    }

    // Visual aid in the editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + transform.forward * 0.5f, grabDistance);
    }
}