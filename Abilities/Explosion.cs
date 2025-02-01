using System.Threading.Tasks;
using UnityEngine;

public class Explosion : Ability
{
    [Header("Explosion Parameters")]
    [SerializeField]
    private bool _mine;
    [SerializeField]
    private GameObject _mineEffect;
    [SerializeField]
    private float _delay;
    [SerializeField]
    private GameObject _explosionEffect;
    [SerializeField]
    private float _explosionRadius, _explosionDuration, _initialRadius;
 
    private bool _exploded, _colliderExpanded;

    public override void Initialize(AbilityController abilityController, float lateBy)
    {
        base.Initialize(abilityController, lateBy);

        if (!_mine)
            Explode();
        else
            ExpandCollider();
    }

    private void Explode()
    {
        if(!_colliderExpanded)
            ExpandCollider();
            
        if (_explosionEffect != null)
            _explosionEffect.SetActive(true);

        controller.SetDespawnTimer(_explosionDuration);

        ResetCollider();

        _exploded = true;
    }

    public void ExpandCollider()
    {
        if(_mine)
            _mineEffect.SetActive(true);
            
        SphereCollider explosionCollider = collider as SphereCollider;
        _initialRadius = explosionCollider.radius;
        explosionCollider.radius = _explosionRadius;
        collider.enabled = true;
        _colliderExpanded = true;
    }

    private async void ResetCollider()
    {
        if(collider != null)
            collider.enabled = false;

        await Task.Delay(10);

        if (collider != null)
            collider.enabled = true;
    }

    protected override void OnTriggerEnter(Collider other)
    {
        if (!_exploded)
        {
            if(_mine && CheckForCollider(other))
            {
                Explode();
                _mineEffect.SetActive(false);
            }

            return;
        }

        base.OnTriggerEnter(other);
    }

    protected override void OnObjectReleased()
    {
        if (_explosionEffect != null)
            _explosionEffect.SetActive(false);

        collider.enabled = false;
        SphereCollider explosionCollider = collider as SphereCollider;
        explosionCollider.radius = _initialRadius;

        _exploded = false;
        _colliderExpanded = false;

        base.OnObjectReleased();
    }
}