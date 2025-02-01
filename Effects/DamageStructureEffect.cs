using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageStructureEffect : DamageEffect
{
    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        ShopController shop = collider.GetComponentInChildren<ShopController>();
        shop.TakeDamage(damage);
    }
}
