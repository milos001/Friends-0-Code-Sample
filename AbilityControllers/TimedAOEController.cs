using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimedAOEController : AbilityController
{
    [SerializeField]
    private float _duration, _delay, _interval;
    [Header("End Effect Parameters")]
    [SerializeField]
    private GameObject _endEffect;
    [SerializeField]
    private float _endEffectDuration;

    private List<Collider> _collidersInRange;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        _collidersInRange = new List<Collider>();

        Destroy(gameObject, _duration);

        InvokeRepeating(nameof(StartEffect), _delay, _interval);
        
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

    private void ActivateEndEffect()
    {
        _endEffect.SetActive(true);
    }
}
