using FishNet.Connection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageEffect : Effect
{
    [SerializeField]
    protected int damage;

    protected NetworkConnection owner;

    public void Initialize(int damage, NetworkConnection owner = null)
    {
        this.damage = damage;
        this.owner = owner;
    }

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        
    }
}
