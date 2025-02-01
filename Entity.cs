using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

public class Entity : NetworkBehaviour
{
    public bool IsPlayer;
    public readonly SyncVar<bool> IsDead = new SyncVar<bool>(false);
    public EntityStats Stats;

    public Action EntityDied;

    [SerializeField]
    private BaseStats _baseStats;
    [Header("Statistics")]
    public int DamageDealt;
    public int EntitiesKilled;

    [Header("Take Damage Parameters")]
    [SerializeField]
    private bool _oneShotProtection;
    [SerializeField]
    private bool _invincibleOnHit;
    [SerializeField]
    private float _invincibilityDuration;
    [SerializeField]
    private float _regenRate = 1;

    private uint _hitTick;
    private bool _regenerating;
    private bool _invincible;

    private Dictionary<int, ObjectPool<AbilityController>> _poolDictionary = new Dictionary<int, ObjectPool<AbilityController>>();
    private Dictionary<int, AbilityController> _abilityDictionary = new Dictionary<int, AbilityController>();

    protected virtual void Awake()
    {
        Stats = new EntityStats(_baseStats);

        Regen();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        Stats.ValueChanged += OnStatValueChanged;
    }

    [ObserversRpc(ExcludeServer = true)]
    private void OnStatValueChanged(Stat stat, int index, float value)
    {
        Stats.SetStatValue(stat, index, value);
    }

    public virtual async void TakeDamage(float damageValue, Entity entity, bool overwriteProtection = false)
    {
        if (_invincible || IsDead.Value)
            return;

        int damage = (int)damageValue;
        int health = (int) Stats.GetStat(Stat.Health);

        health -= damage;

        if (_oneShotProtection && !overwriteProtection)
        {
            if (health <= 0 && health + damage <= 0)
            {
                Die(entity);
                return;
            }
            else if (health <= 0)
                health = 0;
        }
        else
        {
            if (health <= 0)
            {
                Die(entity);
                return;
            }
        }

        Stats.SetBaseStat(Stat.Health, health);

        _hitTick = TimeManager.Tick;

        if (entity != null)
            entity.DamageDealt += damage;

        TakeDamageRpc(damage);

        if (!_invincibleOnHit)
            return;

        _invincible = true;

        await Task.Delay((int)(_invincibilityDuration * 1000));

        _invincible = false;
    }

    [ObserversRpc]
    protected virtual void TakeDamageRpc(int damage)
    {
        GameManager.Instance.UIManager.CreateDamageNumber(damage, transform.position, Color.red);
    }

    protected virtual void Die(Entity entity)
    {
        foreach (var pool in _poolDictionary.Values)
        {
            pool.Dispose();
        }

        IsDead.Value = true;
        if (entity != null)
            entity.EntitiesKilled++;

        if (EntityDied != null)
            EntityDied.Invoke();

        Stats.SetBaseStat(Stat.Health, 0f);

        DieRpc();
    }

    [ObserversRpc]
    protected virtual void DieRpc(){}

    public virtual void Heal(int healAmount)
    {
        if(Stats.GetStat(Stat.Health) + healAmount > Stats.GetStat(Stat.MaxHealth))
        {
            Stats.SetBaseStat(Stat.Health, Stats.GetStat(Stat.MaxHealth));
            return;
        }

        Stats.AddToBaseStat(Stat.Health, healAmount);
        HealRpc(healAmount);
    }

    [ObserversRpc]
    protected virtual void HealRpc(int healAmount)
    {
        GameManager.Instance.UIManager.CreateDamageNumber(healAmount, transform.position, Color.green);
    }

    public bool IsMaxHealth()
    {
        return Stats.GetStat(Stat.Health) >= Stats.GetStat(Stat.MaxHealth);
    }

    protected virtual void Regen()
    {
        Invoke(nameof(Regen), _regenRate);

        if (!_regenerating || IsDead.Value)
            return;

        if (IsMaxHealth() || TimeManager.TimePassed(_hitTick) < 5f)
            return;

        Stats.AddToBaseStat(Stat.Health, 1);
    }

    public bool CheckForAbilityPool(int id)
    {
        return _poolDictionary.ContainsKey(id);
    }

    public void AddAbilityPrefab(AbilityController prefab)
    {
        _abilityDictionary.Add(prefab.Id, prefab);
        _poolDictionary.Add(prefab.Id, new ObjectPool<AbilityController>(() => PoolSpawnFunction(prefab.Id)));
    }

    public AbilityController GetPooledAbility(int id)
    {
        return _poolDictionary[id].Get();
    }

    private AbilityController PoolSpawnFunction(int id)
    {
        return Instantiate(_abilityDictionary[id]);
    }

    public void ReturnAbilityToPool(AbilityController ability)
    {
        _poolDictionary[ability.Id].Release(ability);
    }

    public void SetRegenerating(bool regenerating)
    {
        _regenerating = regenerating;
    }

    public void SetRegenRate(float value)
    {
        _regenRate = value;
    }

    public float GetRegenRate()
    {
        return _regenRate;
    }

    public struct EntityStats
    {
        public Action<Stat, int, float> ValueChanged;
        public Action<float, float> OnHealthChanged;

        [Header("Values")]
        private float[] _value;
        private float[] _health, _maxHealth;
        private float[] _damage;
        private float[] _moveSpeed;
        private float[] _attackSpeed;
        private float[] _rangeModifier;

        public EntityStats(BaseStats baseStats)
        {
            _value = new float[3];
            _value[0] = baseStats.Value;
            _value[1] = 1;
            _value[2] = 1;
            _health = new float[3];
            _health[0] = baseStats.Health;
            _health[1] = 1;
            _health[2] = 1;
            _maxHealth = new float[3];
            _maxHealth[0] = baseStats.MaxHealth;
            _maxHealth[1] = 1;
            _maxHealth[2] = 1;
            _damage = new float[3];
            _damage[0] = baseStats.Damage;
            _damage[1] = 1;
            _damage[2] = 1;
            _moveSpeed = new float[3];
            _moveSpeed[0] = baseStats.MoveSpeed;
            _moveSpeed[1] = 1;
            _moveSpeed[2] = 1;
            _attackSpeed = new float[3];
            _attackSpeed[0] = baseStats.AttackSpeed;
            _attackSpeed[1] = 1;
            _attackSpeed[2] = 1;
            _rangeModifier = new float[3];
            _rangeModifier[0] = baseStats.RangeModifier;
            _rangeModifier[1] = 1;
            _rangeModifier[2] = 1;

            ValueChanged = null;
            OnHealthChanged = null;
        }

        public float GetStat(Stat stat)
        {
            return Mathf.Clamp(GetStatValue(stat, 0) * GetStatValue(stat, 1) * GetStatValue(stat, 2), 0f, 9999999f);
        }

        public void SetBaseStat(Stat stat, float value)
        {
            SetStatValue(stat, 0, value);
        }

        public void AddToBaseStat(Stat stat, float value)
        {
            AddToStatValue(stat, 0, value);
        }

        public async void AddTempBuff(Stat stat, float modifier, float duration)
        {
            AddToStatValue(stat, 1, modifier);

            await Task.Delay((int)(duration * 1000));

            AddToStatValue(stat, 1, -modifier);
        }

        public void AddBuff(Stat stat, float modifier)
        {
            AddToStatValue(stat, 1, modifier);
        }

        public async void AddTempNerf(Stat stat, float modifier, float duration)
        {
            AddToStatValue(stat, 2, -modifier);

            await Task.Delay((int)(duration * 1000));

            AddToStatValue(stat, 2, modifier);
        }

        public void AddNerf(Stat stat, float modifier)
        {
            AddToStatValue(stat, 2, -modifier);
        }

        private void AddToStatValue(Stat stat, int index, float value)
        {
            SetStatValue(stat, index, GetStatValue(stat, index) + value);
        }

        private float GetStatValue(Stat stat, int index)
        {
            switch (stat)
            {
                case Stat.Value:
                    return _value[index];
                case Stat.Health:
                    return _health[index];
                case Stat.MaxHealth:
                    return _maxHealth[index];
                case Stat.Damage:
                    return _damage[index];
                case Stat.MoveSpeed:
                    return _moveSpeed[index];
                case Stat.AttackSpeed:
                    return _attackSpeed[index];
                case Stat.RangeModifier:
                    return _rangeModifier[index];
                default:
                    Debug.Log("Wrong stat");
                    return 0;
            }
        }

        public void SetStatValue(Stat stat, int index, float value)
        {
            switch (stat)
            {
                case Stat.Value:
                    ChangeValue(ref _value[index], value);
                    break;
                case Stat.Health:
                    ChangeValue(ref _health[index], value);
                    if(OnHealthChanged != null)
                        OnHealthChanged.Invoke(value, _maxHealth[0]);
                    break;
                case Stat.MaxHealth:
                    ChangeValue(ref _maxHealth[index], value);
                    break;
                case Stat.Damage:
                    ChangeValue(ref _damage[index], value);
                    break;
                case Stat.MoveSpeed:
                    ChangeValue(ref _moveSpeed[index], value);
                    break;
                case Stat.AttackSpeed:
                    ChangeValue(ref _attackSpeed[index], value);
                    break;
                case Stat.RangeModifier:
                    ChangeValue(ref _rangeModifier[index], value);
                    break;
            }

            if(ValueChanged != null)
                ValueChanged.Invoke(stat, index, value);
        }

        private void ChangeValue(ref float valueRef, float newValue)
        {
            valueRef = newValue;
        }
    }

    [Serializable]
    public struct BaseStats
    {
        public float Value;
        public float Health, MaxHealth;
        public float Damage;
        public float MoveSpeed;
        public float AttackSpeed;
        public float RangeModifier;

    }
}

public enum Stat 
{ 
    Value,
    Health,
    MaxHealth,
    Damage,
    MoveSpeed,
    AttackSpeed,
    RangeModifier
}
