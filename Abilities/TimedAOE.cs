using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimedAOE : Ability
{
    [SerializeField]
    private float _duration, _delay, _interval;
    [Header("End Effect Parameters")]
    [SerializeField]
    private GameObject _endEffect;
    [SerializeField]
    private float _endEffectDuration;

    private List<Collider> _collidersInRange = new List<Collider>();

    public override void Initialize(AbilityController abilityController, float lateBy)
    {
        base.Initialize(abilityController, lateBy);

        controller.SetDespawnTimer(_duration);

        InvokeRepeating(nameof(StartEffect), 0f, _interval);
        
        if(_endEffect != null)
            Invoke(nameof(ActivateEndEffect), _duration - _endEffectDuration);
    }

    protected override void OnTriggerEnter(Collider other)
    {
        _collidersInRange.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        _collidersInRange.Remove(other);
    }

    private void StartEffect()
    {
        foreach (var collider in _collidersInRange)
        {
            if(collider == null)
                continue;

            ActivateOnHitForCollider(collider);
        }
    }

    protected override void OnObjectReleased()
    {
        base.OnObjectReleased();

        _collidersInRange.Clear();
        CancelInvoke(nameof(StartEffect));
    }

    private void ActivateEndEffect()
    {
        _endEffect.SetActive(true);
    }
}
