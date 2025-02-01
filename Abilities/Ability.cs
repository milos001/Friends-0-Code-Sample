using FischlWorks_FogWar;
using FishNet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Ability : MonoBehaviour
{
    public Action InitializeCallback;

    [SerializeField]
    protected CollisionTarget[] collisionTargets;
    [SerializeField]
    private bool _isExclusionary;
    [SerializeField]
    private float _colliderResetTimer = .25f;
    [SerializeField]
    private AudioSource _onSpawnSound; 
    [Header("Delay Parameters")]
    [SerializeField]
    private float _delayToEnableCollider;
    [SerializeField]
    private float _disableColliderAfter;
    [SerializeField]
    protected Collider collider;
    [SerializeField]
    private GameObject _visuals;
    [SerializeField]
    private ParticleSystem _trailEffect;
    [SerializeField]
    protected GameObject telegraphEffect;
    
    protected AbilityController controller;

    private List<Collider> _hitColliders = new List<Collider>();
    private List<Collider> _collidersToExclude = new List<Collider>();
    private Collider _exclusiveCollider;

    private bool _initializedController, _visible = true;
    private bool test;

    public virtual void Initialize(AbilityController abilityController, float lateBy)
    {
        if (_onSpawnSound != null)
            GameManager.Instance.SoundManager.PlaySound(_onSpawnSound);

        if (_delayToEnableCollider > 0f)
        {
            collider.enabled = false;
            Invoke(nameof(EnableCollider), _delayToEnableCollider);
        }
        else
        {
            if (collider == null)
                collider = GetComponent<Collider>();

            collider.enabled = true;
        }

        if (_disableColliderAfter > 0f)
            Invoke(nameof(DisableCollider), _disableColliderAfter);

        if(_trailEffect != null)
            _trailEffect.Clear();

        enabled = true;

        foreach (var effectList in collisionTargets.Select(x => x.Effects))
        {
            foreach (var effect in effectList)
            {
                effect.Initialize(abilityController.GetInterface());

                if(effect.GetType().IsSubclassOf(typeof(DamageEffect)))
                {
                    DamageEffect damageEffect = effect as DamageEffect;
                    damageEffect.Initialize(abilityController.Stats.GetStat(Stat.Damage));
                }
            }
        }

        if (_initializedController)
            return;

        this.controller = abilityController;

        controller.ObjectReleased += OnObjectReleased;

        _initializedController = true;
    }

    protected virtual void Update()
    {
        if (_visuals == null)
            return;

        bool visible = csFogWar.Instance.CheckVisibility(transform.position);

        if (_visible != visible)
        {
            _visuals.SetActive(visible);
            _visible = visible;
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (_hitColliders.Contains(other) || (_collidersToExclude != null && _collidersToExclude.Contains(other)))
            return;

        if (_isExclusionary && other != _exclusiveCollider)
            return;

        _hitColliders.Add(other);

        ActivateOnHitForCollider(other);

        Invoke(nameof(ResetCollider), _colliderResetTimer);
    }

    protected virtual void TargetHit(Collider collider, CollisionTarget target)
    {
        if (!gameObject.activeInHierarchy)
            return;

        if(InstanceFinder.IsServerStarted)
            TargetHitServer(collider, target);

        TargetHitObserver(collider, target);
    }

    protected virtual void TargetHitServer(Collider collider, CollisionTarget target)
    {
        foreach (var effect in target.Effects)
        {
            if(!effect.IsClientEffect)
                effect.OnHitHandler(collider, target.CollisionIdentifier);
        }
    }

    protected virtual void TargetHitObserver(Collider collider, CollisionTarget target)
    {
        foreach (var effect in target.Effects)
        {
            if (controller.IsOwner && effect.IsClientEffect)
                effect.OnHitHandler(collider, target.CollisionIdentifier);

            if (effect.HasClientFX)
                effect.PlayClientFX(collider, target.CollisionIdentifier);
        }

        if (target.DestroyOnHit)
            ReleaseObject();
    }

    protected void ActivateOnHitForCollider(Collider collider)
    {
        foreach (var target in collisionTargets)
        {
            switch (target.CollisionType)
            {
                case CollisionType.Layer:
                    if (collider.gameObject.layer == LayerMask.NameToLayer(target.CollisionIdentifier))
                        TargetHit(collider, target);
                    break;
                case CollisionType.Tag:
                    if (collider.CompareTag(target.CollisionIdentifier))
                        TargetHit(collider, target);
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

    protected virtual void ReleaseObject()
    {
        controller.ReleaseObject();
    }

    protected virtual void OnObjectReleased()
    {
        _hitColliders = new List<Collider>();
        _collidersToExclude = new List<Collider>();
        _exclusiveCollider = null;
    }

    private void ResetCollider()
    {
        if(_hitColliders != null && _hitColliders.Count > 0)
            _hitColliders.RemoveAt(0);
    }

    private void EnableCollider()
    {
        collider.enabled = true;
    }

    private void DisableCollider()
    {
        collider.enabled = false;
    }

    public void AddColliderToExclude(Collider collider)
    {
        _collidersToExclude.Add(collider);
    }

    public void RemoveColliderToExclude(Collider collider)
    {
        _collidersToExclude.Remove(collider);
    }

    public void SetExclusiveCollider(Collider collider)
    {
        _exclusiveCollider = collider;
    }

    public void AddCollisionTarget(CollisionTarget target)
    {
        List<CollisionTarget> newTargets = collisionTargets.ToList();
        newTargets.Add(target);
        collisionTargets = newTargets.ToArray();
    }

    public void RemoveLastCollisionTarget()
    {
        List<CollisionTarget> newTargets = collisionTargets.ToList();
        newTargets.RemoveAt(newTargets.Count - 1);
        collisionTargets = newTargets.ToArray();
    }
}

public enum DestinationType
{
    MousePosition,
    TargetPosition,
    NearestEnemy,
    NearestPlayer
}
