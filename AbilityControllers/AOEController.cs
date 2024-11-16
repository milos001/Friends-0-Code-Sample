using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AOEController : AbilityController
{
    [SerializeField]
    private float _duration;
    [SerializeField]
    private bool _affectedByAttackSpeed;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if(_duration > 0f)
        {
            if (_affectedByAttackSpeed)
                _duration *= GameManager.Instance.Players[Owner].AttackSpeed;

            Destroy(gameObject, _duration);
        }
    }

    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        hitATarget = false;
    }
}
