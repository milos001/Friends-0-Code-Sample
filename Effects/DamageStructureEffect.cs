using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageStructureEffect : Effect
{
    private int _damage;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        collider.GetComponent<ShopController>().TakeDamage(_damage);
    }
}
