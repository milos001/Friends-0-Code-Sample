using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyOnProjectileCountEffect : Effect
{
    [SerializeField]
    private int _limit;
    [SerializeField]
    private GameObject _destroyEffect;
    [SerializeField]
    private float _destroyEffectDuration;

    private int _counter;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        _counter++;

        if (_counter == _limit)
        {
            if(_destroyEffect != null)
            {
                Destroy(gameObject, _destroyEffectDuration);
                _destroyEffect.SetActive(true);
            }
            else
                Destroy(gameObject);
        }
    }
}
