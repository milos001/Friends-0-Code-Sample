using FishNet;
using Steamworks;
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class AbilityController : MonoBehaviour
{
    public Guid UniqueId;
    public Action ObjectReleased, ObjectDestroyed;
    public Action<TargetData> TargetUpdated;

    [Range(1000, 100000)]
    public int Id;
    public float CastTime = .1f, SlowPercent = 50;
    [HideInInspector]
    public bool IsOwner;
    [HideInInspector]
    public Entity.EntityStats Stats;

    [SerializeField]
    private bool _isNetworked, _isDestroyable;
    [SerializeField]
    private int _health = 0;
    [SerializeField]
    private AbilityData[] _abilityData;

    private Entity _entityInterface;
    private TargetData _target;

    private float _lateBy;
    private int _maxHealth;

    private bool _initialized;

    public void Initialize(Entity entityInterface, Guid guid, float lateBy)
    {
        _lateBy = lateBy;
        UniqueId = guid;

        if (!_initialized)
        {
            IsOwner = entityInterface.IsOwner;
            _entityInterface = entityInterface;
            Stats = _entityInterface.Stats;
        }

        foreach (var abilityData in _abilityData)
        {
            if (!_initialized)
            {
                foreach (var abilitToInit in abilityData.AbilitiesToInitialize)
                {
                    abilityData.Ability.InitializeCallback += () => InitializeAbility(abilitToInit);
                }
            }

            if (abilityData.Delay > 0f)
            {
                abilityData.Ability.enabled = false;
                StartCoroutine(InitializeAbilityWithDelay(abilityData.Ability, abilityData.Delay, lateBy));
            }
            else
                InitializeAbility(abilityData.Ability);
        }

        if (!entityInterface.IsPlayer)
            GameManager.Instance.AbilityManager.AddEnemyAbility(this, entityInterface.ObjectId);

        if (_isNetworked)
        {
            GameManager.Instance.AbilityManager.AddNetworkedAbility(UniqueId, transform.position);
            InstanceFinder.TimeManager.OnPostTick += CheckAbilityPosition;
        }

        if (_isDestroyable)
            _maxHealth = _health;

        if (!_initialized)
            _initialized = true;
    }

    private void CheckAbilityPosition()
    {
        if (InstanceFinder.IsServerStarted)
        {
            GameManager.Instance.AbilityManager.UpdateNetworkedAbility(UniqueId, transform.position);
            return;
        }

        Vector3 networkPosition = GameManager.Instance.AbilityManager.GetNetworkedAbilityPosition(UniqueId);

        if (Vector3.Distance(networkPosition, transform.position) > .2f)
            transform.position = networkPosition;
    }

    private void InitializeAbility(Ability ability)
    {
        ability.Initialize(this, _lateBy);
    }

    private IEnumerator InitializeAbilityWithDelay(Ability ability, float delay, float lateBy)
    {
        yield return new WaitForSeconds(delay);

        ability.Initialize(this, lateBy);
    }

    public void SetDespawnTimer(float time)
    {
        DelayedDespawn(time);
    }

    private async void DelayedDespawn(float time)
    {
        await Task.Delay((int)(time * 1000));

        ReleaseObject();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isDestroyable || other.gameObject.layer != LayerMask.NameToLayer("Ability"))
            return;

        if (!other.TryGetComponent(out DamageEffect damageEffect))
            return;

        AbilityController abCon = other.GetComponent<AbilityController>();
        if (abCon.Id == Id)
            return;

        abCon.ReleaseObject();

        _health -= damageEffect.GetDamage();

        if(_health <= 0)
        {
            if (ObjectDestroyed != null)
                ObjectDestroyed.Invoke();
            else
                ReleaseObject();
        }
    }

    public void ReleaseObject()
    {
        if (!gameObject.activeInHierarchy)
            return;
       
        ObjectReleased.Invoke();

        transform.position = Vector3.zero;
        gameObject.SetActive(false);

        _entityInterface.ReturnAbilityToPool(this);

        if (_entityInterface.IsPlayer)
            GameManager.Instance.AbilityManager.RemovePlayerAbility(this, _entityInterface.OwnerId);
        else
            GameManager.Instance.AbilityManager.RemoveEnemyAbility(this, _entityInterface.ObjectId);

        if(_isNetworked)
            GameManager.Instance.AbilityManager.RemoveNetworkedAbility(UniqueId);

        if (_isDestroyable)
            _health = _maxHealth;
    }

    public Entity GetInterface()
    {
        return _entityInterface;
    }

    public void SetTarget(TargetData target)
    {
        _target = target;

        if (TargetUpdated != null)
            TargetUpdated.Invoke(target);
    }

    public TargetData GetTarget()
    {
        return _target;
    }

    [Serializable]
    private struct AbilityData
    {
        public Ability Ability;
        public float Delay;
        public Ability[] AbilitiesToInitialize;
    }
}

public struct TargetData
{
    public Transform Transform;
    public Entity Entity;

    public TargetData(Transform transform, Entity entity)
    {
        Transform = transform;
        Entity = entity;
    }
}

