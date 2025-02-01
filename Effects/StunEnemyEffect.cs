using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StunEnemyEffect : Effect
{
    [SerializeField]
    private float _stunDuration;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        collider.GetComponent<Enemy>().Stun(_stunDuration);
    }
}
