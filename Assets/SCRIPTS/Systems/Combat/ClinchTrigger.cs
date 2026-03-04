using UnityEngine;

/// <summary>
/// Automatically triggers clinch grabs when the player is in close proximity to enemies.
/// Performs proximity checks each frame and initiates a clinch if conditions are met.
/// Works in conjunction with ClinchHandler to manage Muay Thai-style grappling mechanics.
/// </summary>
public class ClinchTrigger : MonoBehaviour
{
    #region Fields
    
    /// <summary>
    /// Cached reference to the ClinchHandler component.
    /// Lazy-loaded in Update() on first use.
    /// </summary>
    private ClinchHandler _clinchHandler;
    
    /// <summary>
    /// Maximum distance (in meters) at which a clinch can be automatically triggered.
    /// The sphere cast is offset forward from the player's position.
    /// </summary>
    [Header("Detection Settings")]
    [Tooltip("Maximum distance for automatic clinch detection")]
    public float grabDistance = 1.2f;
    
    /// <summary>
    /// Forward offset distance for the sphere cast origin point.
    /// Prevents accidentally grabbing enemies behind the player.
    /// </summary>
    [Tooltip("Forward offset for detection sphere (prevents grabbing enemies behind)")]
    public float detectionOffset = 0.5f;
    
    /// <summary>
    /// Minimum dot product for directional check (0.7 ≈ 45° cone).
    /// Higher values = narrower detection cone (more precise aiming required).
    /// </summary>
    [Tooltip("Direction threshold (0.7 = 45° cone, 0.9 = 25° cone)")]
    [Range(0f, 1f)]
    public float directionThreshold = 0.7f;
    
    /// <summary>
    /// Cooldown time in seconds between clinch attempts.
    /// Prevents rapid clinch spam if conditions flicker.
    /// </summary>
    [Tooltip("Cooldown between clinch attempts (0 = no cooldown)")]
    public float clinchCooldown = 0.2f;
    
    /// <summary>
    /// Check proximity every N frames (1 = every frame, 2 = every other frame).
    /// Reduces CPU overhead for proximity checks.
    /// </summary>
    [Tooltip("Check proximity every N frames (higher = better performance, lower responsiveness)")]
    [Range(1, 5)]
    public int checkInterval = 1;
    
    #endregion
    
    #region Performance Optimization
    
    /// <summary>
    /// Pre-allocated array for physics queries to avoid GC allocations.
    /// </summary>
    private Collider[] _colliderBuffer = new Collider[10];
    
    /// <summary>
    /// Cached layer mask for enemy detection.
    /// Initialized lazily on first use.
    /// </summary>
    private static int _enemyLayerMask = -1;
    private static int EnemyLayerMask
    {
        get
        {
            if (_enemyLayerMask == -1)
            {
                // Attempt to use "Enemy" layer if it exists, otherwise use everything
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                _enemyLayerMask = enemyLayer >= 0 ? LayerMask.GetMask("Enemy") : ~0;
            }
            return _enemyLayerMask;
        }
    }
    
    /// <summary>
    /// Frame counter for check interval optimization.
    /// </summary>
    private int _frameCounter = 0;
    
    /// <summary>
    /// Timestamp of last clinch attempt for cooldown tracking.
    /// </summary>
    private float _lastClinchAttemptTime = -999f;
    
    #endregion

    #region Unity Lifecycle
    
    /// <summary>
    /// Called every frame to check for nearby enemies that can be clinched.
    /// Lazy-loads the ClinchHandler reference and only performs proximity checks
    /// when the player is not already engaged in clinch-related activities.
    /// Uses frame-rate limiting for improved performance.
    /// </summary>
    void Update()
    {
        // Lazy-load ClinchHandler reference on first use
        if (_clinchHandler == null)
        {
            _clinchHandler = GetComponent<ClinchHandler>();
            
            // Early exit if ClinchHandler doesn't exist (e.g., fighting style doesn't support clinching)
            if (_clinchHandler == null)
                return;
        }

        // Only scan for nearby enemies if we're not currently in any clinch state or recovery
        if (!_clinchHandler.IsClinching && !_clinchHandler.IsInClinchRecovery)
        {
            // Frame-rate limiting: only check every N frames
            _frameCounter++;
            if (_frameCounter >= checkInterval)
            {
                _frameCounter = 0;
                CheckProximity();
            }
        }
    }
    
    #endregion

    #region Proximity Detection
    
    /// <summary>
    /// Performs a sphere overlap check to find nearby enemies that can be clinched.
    /// Uses a non-allocating overlap sphere for performance, positioned slightly in front of the player.
    /// Uses layer masking to only check enemy colliders.
    /// 
    /// Clinch Conditions:
    /// 1. Target must have a HealthComponent
    /// 2. Target must be faction: Enemy
    /// 3. Player must be facing the target (configurable cone angle)
    /// 4. Cooldown period must have elapsed since last attempt
    /// 
    /// Only the first valid target found will be clinched (breaks after first match).
    /// </summary>
    private void CheckProximity()
    {
        // Check cooldown to prevent rapid clinch spam
        if (Time.time - _lastClinchAttemptTime < clinchCooldown)
            return;
        
        // Perform sphere cast slightly in front of the player with layer mask
        // This prevents accidentally grabbing enemies behind the player
        // and only checks relevant layers for better performance
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position + transform.forward * detectionOffset, 
            grabDistance, 
            _colliderBuffer,
            EnemyLayerMask
        );

        // Iterate through all colliders found in the sphere
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _colliderBuffer[i];
            
            // Skip self
            if (hitCollider.transform == transform)
                continue;
            
            // Validate target has health component and is an enemy
            HealthComponent health = hitCollider.GetComponent<HealthComponent>();
            if (health == null || health.GetFaction() != Faction.Enemy)
                continue;

            // Check if player is facing the target (cone-based detection)
            Vector3 dirToEnemy = (hitCollider.transform.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, dirToEnemy) > directionThreshold)
            {
                // Valid target found - attempt to initiate clinch
                _clinchHandler.AttemptClinch(hitCollider.transform);
                
                // Update cooldown timestamp
                _lastClinchAttemptTime = Time.time;
                
                // Only clinch one enemy at a time
                break;
            }
        }
    }
    
    #endregion

    #region Editor Visualization
    
    /// <summary>
    /// Draws a yellow wire sphere in the Scene view when this GameObject is selected.
    /// Visualizes the grab detection range to aid in level design and gameplay tuning.
    /// The sphere is positioned forward from the player (same as the actual detection sphere).
    /// Also draws a direction cone to visualize the facing requirement.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Draw detection sphere
        Gizmos.color = Color.yellow;
        Vector3 detectionCenter = transform.position + transform.forward * detectionOffset;
        Gizmos.DrawWireSphere(detectionCenter, grabDistance);
        
        // Draw direction cone visualization
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f); // Semi-transparent yellow
        float coneAngle = Mathf.Acos(directionThreshold) * Mathf.Rad2Deg;
        Vector3 coneRight = Quaternion.Euler(0, coneAngle, 0) * transform.forward * (grabDistance + detectionOffset);
        Vector3 coneLeft = Quaternion.Euler(0, -coneAngle, 0) * transform.forward * (grabDistance + detectionOffset);
        
        Gizmos.DrawLine(transform.position, transform.position + coneRight);
        Gizmos.DrawLine(transform.position, transform.position + coneLeft);
    }
    
    #endregion
}