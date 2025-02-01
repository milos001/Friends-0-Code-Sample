using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamagePlayerEffect : DamageEffect
{
    [SerializeField]
    private bool _destroyOnHit;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        collider.GetComponent<Entity>().TakeDamage(damage, entityInterface);
    }
}
