using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockEnemyEffect : Effect
{
    [SerializeField]
    private float _pushBackForce = 10f;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        Enemy enemy = collider.GetComponent<Enemy>();
        enemy.Push((enemy.transform.position - transform.position).normalized, _pushBackForce);
    }
}
