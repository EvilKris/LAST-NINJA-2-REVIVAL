using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public abstract class BaseCreature : MonoBehaviour
{
    // Base class for characters with the main stats and methods to handle damage

    public float hp;
    public float movementSpeed=5f;
    [HideInInspector] public bool isAbleToMove;
    [HideInInspector] public bool isAlive;
    [HideInInspector] public Animator animator;
    [HideInInspector] public Rigidbody rb;

    protected private List<Transform> enemyHitCache; //keeps track of enemies that have been hit by this agent
    protected private Texture icon; //for the UI (profile pics) 

    //probably first of many, right? :-)   


    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    protected virtual void Start()
    {
        SetUpStats();      
    }

    private void SetUpStats()
    {
        hp = 100;
        isAbleToMove = true;//
        isAlive = true;        
    }

    public virtual void TakeDamage(float damage)
    {
        hp -= damage;

        if (hp < 0) hp = 0;
    }

    public abstract void TakeHit();
    public abstract void OnDeath();
}
