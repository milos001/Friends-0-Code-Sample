using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackSpeedBuffEffect : Effect
{
    [SerializeField]
    private float _attackSpeed = .5f, _buffDuration = 5f;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        PlayerManager player = collider.GetComponent<PlayerManager>();

        player.SetTempAttackSpeedAsync(_attackSpeed, _buffDuration);
    }
}
