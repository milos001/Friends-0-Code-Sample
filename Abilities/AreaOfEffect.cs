using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AreaOfEffect : Ability
{
    [SerializeField]
    private float _duration;
    [SerializeField]
    private bool _affectedByAttackSpeed;

    private float _initialDuration;

    public override void Initialize(AbilityController abilityController, float lateBy)
    {
        base.Initialize(abilityController, lateBy);

        _initialDuration = _duration;

        if(_duration > 0f)
        {
            if (_affectedByAttackSpeed)
                _duration *= controller.Stats.GetStat(Stat.AttackSpeed);

            controller.SetDespawnTimer(_duration);
        }
    }

    protected override void OnObjectReleased()
    {
        base.OnObjectReleased();

        _duration = _initialDuration;
    }
}
