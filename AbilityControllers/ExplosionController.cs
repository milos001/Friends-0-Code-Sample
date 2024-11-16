using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class ExplosionController : AbilityController
{
    [Header("Explosion Parameters")]
    [SerializeField]
    private bool _initializeItself;
    [SerializeField]
    private bool _mine;
    [SerializeField]
    private float _delay;
    [SerializeField]
    private GameObject _explosionEffect;
    [SerializeField]
    private float _explosionRadius, _explosionDuration;
 
    private bool _initialized;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (_initializeItself && !_mine)
            Invoke(nameof(Initialize), _delay);
    }

    public void Initialize()
    {
        SphereCollider explosionCollider = collider as SphereCollider;
        explosionCollider.radius = _explosionRadius;
        collider.enabled = true;
        
        if(_explosionEffect != null)
            _explosionEffect.SetActive(true);

        Destroy(gameObject, _explosionDuration);
        
        ResetCollider();

        _initialized = true;
    }

    public void ExpandCollider()
    {
        SphereCollider explosionCollider = collider as SphereCollider;
        explosionCollider.radius = _explosionRadius;
    }

    private async void ResetCollider()
    {
        collider.enabled = false;
        await Task.Delay(100);
        collider.enabled = true;
    }

    protected override void OnTriggerEnter(Collider other)
    {
        if (!_initialized)
        {
            if(_mine && CheckForCollider(other))
                Initialize();

            return;
        }

        base.OnTriggerEnter(other);

        hitATarget = false;
    }
}