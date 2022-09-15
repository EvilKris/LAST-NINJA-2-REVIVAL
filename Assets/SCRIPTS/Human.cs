using System.Collections;
using UnityEngine;


public class Human : BaseCreature
{
    //Note - HumanOrMaggotProfile declaration at base  
    [Tooltip("Hit this for instakill - useful for debugging")]
    public bool killSwitch;   
    
    protected override void Awake()
    {
        base.Awake();    

       
    }

    protected override void Start()
    {
        base.Start();
       
        //Whatever else you need
    }

    protected void Update()
    {
        if (killSwitch) OnDeath();
    } 

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);

        // TODO add SFX, sounds,etc
    }
    
    IEnumerator RegainHP(float amntPerSeconds) 
    {
        //Going to need this for hp increase while unconscious
        yield return null; 
    }

    public override void OnDeath()
    {
    }

    public override void TakeHit()
    {        
    }
}

