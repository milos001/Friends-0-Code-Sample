using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageTerrainEffect : Effect
{
    [SerializeField]
    private int _damage;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        collider.GetComponent<DestroyableTerrain>().TakeDamage(_damage);
    }
}
