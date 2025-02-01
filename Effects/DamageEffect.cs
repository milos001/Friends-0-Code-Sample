using FishNet.Connection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageEffect : Effect
{
    [SerializeField]
    protected float damageModifier = 1;

    protected int damage;

    protected NetworkConnection owner;
    public int GetDamage() => damage;

    public void Initialize(float damage)
    {
        this.damage = (int) damage;
        this.damage = (int) (damage * damageModifier);
    }

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        
    }
}
