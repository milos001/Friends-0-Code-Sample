using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveSpeedBuffEffect : Effect
{
    [SerializeField]
    private float _speedModifier = 1.5f, _buffDuration = 5f;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        PlayerManager player = collider.GetComponent<PlayerManager>();

        player.SetTempMoveSpeed(_speedModifier, _buffDuration);
    }
}
