using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageEnemyEffect : DamageEffect
{
    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        if (owner == null)
            owner = Owner;

        collider.GetComponent<Enemy>().TakeDamage(damage, transform.position, GameManager.Instance.Players[owner]);
    }
}
