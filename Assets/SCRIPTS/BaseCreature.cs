using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public abstract class BaseCreature : MonoBehaviour, IDamageable
{
    // Base class for characters with the main stats and methods to handle damage
    [Header("Stats")]
    public float hp;
    public float movementSpeed = 5f;

    [Header("State")]
    public bool isAbleToMove;
    public bool isAlive;
    public bool isInvulnerable = false; // Useful for modern dodge rolls

    [HideInInspector] public Animator animator;
    [HideInInspector] public Rigidbody rb;

    protected List<Transform> enemyHitCache = new List<Transform>();
    protected Texture icon;

    // IDamageable implementation property (IsDead - 死亡状態)
    public bool IsDead => !isAlive || hp <= 0;
    // Add this to your BaseCreature script

    private CombatHitbox[] _allHitboxes;



    // THE SMART ANIMATION EVENT RECEIVERS
    // In Unity's Animation Window, use these functions and type the ID in the String box

    // THE SMART ANIMATION EVENT RECEIVERS
    // In Unity's Animation Window, use these functions and type the ID in the String box

    public void OpenHitbox(string id)
    {
        foreach (var hb in _allHitboxes)
        {
            if (hb.HitboxID == id) hb.Activate();
        }
    }

    public void CloseHitbox(string id)
    {
        foreach (var hb in _allHitboxes)
        {
            if (hb.HitboxID == id) hb.Deactivate();
        }
    }
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        _allHitboxes = GetComponentsInChildren<CombatHitbox>();
    }

    protected virtual void Start()
    {
        SetUpStats();
    }

    private void SetUpStats()
    {
        hp = 100;
        isAbleToMove = true;
        isAlive = true;
    }

    /// <summary>
    /// IDamageable implementation (Damēji - ダメージ)
    /// </summary>
    public virtual void TakeDamage(float damage)
    {
        if (IsDead || isInvulnerable) return;

        hp -= damage;
        hp = Mathf.Max(hp, 0); // Ensure HP doesn't go negative

        if (hp <= 0 && isAlive)
        {
            isAlive = false;
            OnDeath(); // Call your abstract method
        }
        else
        {
            TakeHit(); // Call your abstract method for flinching
        }
    }

    // Add these methods inside your BaseCreature class

    public void ClearHitCache()
    {
        enemyHitCache.Clear();
    }

    public void RegisterHit(Transform target)
    {
        if (!enemyHitCache.Contains(target))
        {
            enemyHitCache.Add(target);
        }
    }

    public bool HasHitTarget(Transform target)
    {
        return enemyHitCache.Contains(target);
    }

    // Abstract hooks for your specific character logic
    public abstract void TakeHit(); // Hirumi (怯み)
    public abstract void OnDeath(); // Shibō (死亡)
}