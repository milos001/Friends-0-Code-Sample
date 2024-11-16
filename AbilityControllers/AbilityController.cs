using FischlWorks_FogWar;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AbilityController : NetworkBehaviour
{
    [SerializeField]
    protected CollisionTarget[] collisionTargets;
    [SerializeField]
    private float _colliderResetTimer = .25f;
    [SerializeField]
    protected bool enemyAbility;
    [SerializeField]
    private AudioSource _onAwakeSound; 
    [Header("Delay Parameters")]
    [SerializeField]
    private float _delayToEnableCollider;
    [SerializeField]
    protected Collider collider;

    private List<Collider> _hitColliders;
    private List<Collider> _collidersToExclude;

    protected bool hitATarget;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        _hitColliders = new List<Collider>();

        gameObject.AddComponent<csFogVisibilityAgent>();

        if (_onAwakeSound != null)
            _onAwakeSound.Play();

        if (_delayToEnableCollider > 0f)
        {
            collider.enabled = false;
            Invoke(nameof(EnableCollider), _delayToEnableCollider);
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!IsServerInitialized || _hitColliders.Contains(other) || (_collidersToExclude != null && _collidersToExclude.Contains(other)))
            return;

        _hitColliders.Add(other);
        Invoke(nameof(ResetCollider), _colliderResetTimer);

        foreach (var target in collisionTargets)
        {
            switch (target.CollisionType)
            {
                case CollisionType.Layer:
                    if (other.gameObject.layer == LayerMask.NameToLayer(target.CollisionIdentifier))
                    {
                        foreach (var effect in target.Effects)
                        {
                            effect.OnHitHandler(other, target.CollisionIdentifier);
                        }
                        hitATarget = true;
                        if (target.DestroyOnHit)
                            Destroy(gameObject);
                    }
                    break;
                case CollisionType.Tag:
                    if (other.CompareTag(target.CollisionIdentifier))
                    {
                        foreach (var effect in target.Effects)
                        {
                            effect.OnHitHandler(other, target.CollisionIdentifier);
                        }
                        hitATarget = true;
                        if (target.DestroyOnHit)
                            Destroy(gameObject);
                    }
                    break;
            }
        }

    }

    protected void ActivateOnHitForCollider(Collider collider)
    {
        foreach (var target in collisionTargets)
        {
            switch (target.CollisionType)
            {
                case CollisionType.Layer:
                    if (collider.gameObject.layer == LayerMask.NameToLayer(target.CollisionIdentifier))
                    {
                        foreach (var effect in target.Effects)
                        {
                            effect.OnHitHandler(collider, target.CollisionIdentifier);
                        }
                        hitATarget = true;
                        if (target.DestroyOnHit)
                            Destroy(gameObject);
                    }
                    break;
                case CollisionType.Tag:
                    if (collider.CompareTag(target.CollisionIdentifier))
                    {
                        foreach (var effect in target.Effects)
                        {
                            effect.OnHitHandler(collider, target.CollisionIdentifier);
                        }
                        hitATarget = true;
                        if (target.DestroyOnHit)
                            Destroy(gameObject);
                    }
                    break;
            }
        }
    }

    protected bool CheckForCollider(Collider collider)
    {
        foreach (var target in collisionTargets)
        {
            switch (target.CollisionType)
            {
                case CollisionType.Layer:
                    if (collider.gameObject.layer == LayerMask.NameToLayer(target.CollisionIdentifier))
                        return true;
                    break;
                case CollisionType.Tag:
                    if (collider.CompareTag(target.CollisionIdentifier))
                        return true;
                    break;
            }
        }

        return false;
    }

    private void ResetCollider()
    {
        _hitColliders.RemoveAt(0);
    }

    private void EnableCollider()
    {
        collider.enabled = true;
    }

    public void AddColliderToExclude(Collider collider)
    {
        if (_collidersToExclude == null)
            _collidersToExclude = new List<Collider>();

        _collidersToExclude.Add(collider);
    }

    public void SetDestroyTimer(float time)
    {
        Destroy(gameObject, time);
    }

    public void AddCollisionTarget(CollisionTarget target)
    {
        List<CollisionTarget> newTargets = collisionTargets.ToList();
        newTargets.Add(target);
        collisionTargets = newTargets.ToArray();
    }
}
