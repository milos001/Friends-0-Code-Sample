using FishNet.Object;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using System;
using System.Xml;
using FishNet.Transporting;

public class FriendAbilityController : NetworkBehaviour
{
    [SerializeField]
    private AbilityController[] _abilityPrefabs;

    [SerializeField]
    private AbilityController[] _bulletPrefabs, _slashPrefabs, _grenadePrefabs, _hookPrefabs, _freezeRayPrefabs, _shieldPrefabs, _nukePrefabs, _healPulsePrefabs, _frenzyPrefabs;
    [SerializeField]
    private AbilityController _jabLinkedAbility;
    [SerializeField]
    private AbilityController _switchPlacesDamageModifier;

    [SerializeField]
    private GameObject _switchPlacesParticle;
    [SerializeField]
    private AudioSource _source;
    [SerializeField]
    private AudioClip _jabAwakeSound, _switchPlacesAwakeSound, _shieldAwakeSound, _shieldDestroySound, _becomeInvisibleAwakeSound;

    [SerializeField]
    private SphereCollider _sphereCollider;

    private FriendManager _manager;

    private Dictionary<int, AbilityController> _abilityDictionary;

    private AbilityController _lastSpawnedAbility;
    private Action<AbilityController> _setLastSpawnedAbility;

    // Utility Values
    private GameObject _utilityObject;
    private float _utilityRegenRate = .3f;
    private int _utilityResource = 100;
    private int _maxUtility = 100;
    private bool _utilityActive;

    // Shield values
    private int _shieldMod2Counter;
    private float _lastUtilitySpendCall;
    private float _shieldMod2ChargeRate = .05f;

    // Jab values
    private List<Enemy> _enemiesHitByJab;
    private int _jabDamageModifier = 2;
    private int _jabModifier;
    private bool _jabbing;
    private float _jabStartTime;
    private int _jabChargeCounter = 0;

    private void Awake()
    {
        _abilityDictionary = new Dictionary<int, AbilityController>();
        foreach (var ability in _abilityPrefabs)
        {
            _abilityDictionary.Add(ability.Id, ability);
        }
    }

    [ServerRpc]
    private void InitServer(int friendId)
    {
        InitForObservers(friendId);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void InitForObservers(int friendId)
    {
        _manager = ClientManager.Objects.Spawned[friendId].GetComponent<FriendManager>();
    }

    public void Init(FriendManager friendManager)
    {
        _manager = friendManager;

        InitServer(friendManager.ObjectId);
    }

    private void SpawnAbility(int id, Vector3 spawnPos, Quaternion rotation, float destroyTimer = 0f)
    {
        Guid uniqueId = Guid.NewGuid();
        RpcServerSpawnAbility(uniqueId, id, spawnPos, rotation, -1, _manager.PlayerManager.OwnerId, destroyTimer, TimeManager.Tick);
        SpawnAbilityFinal(uniqueId, id, spawnPos, rotation, -1, _manager.PlayerManager.OwnerId, destroyTimer, 0f);
    }

    private void SpawnAbility(int id, Vector3 spawnPos, Quaternion rotation, int parentId, float destroyTimer = 0f)
    {
        Guid uniqueId = Guid.NewGuid();
        RpcServerSpawnAbility(uniqueId, id, spawnPos, rotation, parentId, _manager.PlayerManager.OwnerId, destroyTimer, TimeManager.Tick);
        SpawnAbilityFinal(uniqueId, id, spawnPos, rotation, parentId, _manager.PlayerManager.OwnerId, destroyTimer, 0f);
    }

    [ServerRpc]
    public void RpcServerSpawnAbility(Guid uniqueId, int id, Vector3 spawnPos, Quaternion rotation, int parentId, int playerId, float destroyTimer, uint tick, Channel channel = Channel.Reliable)
    {
        RpcObserversSpawnAbility(uniqueId, id, spawnPos, rotation, parentId, playerId, destroyTimer, tick);
    }

    [ObserversRpc(ExcludeOwner = true)]
    public void RpcObserversSpawnAbility(Guid uniqueId, int id, Vector3 spawnPos, Quaternion rotation, int parentId, int playerId, float destroyTimer, uint tick, Channel channel = Channel.Reliable)
    {
        SpawnAbilityFinal(uniqueId, id, spawnPos, rotation, parentId, playerId, destroyTimer, (float) TimeManager.TimePassed(tick, false));
    }

    private async void SpawnAbilityFinal(Guid uniqueId, int id, Vector3 spawnPos, Quaternion rotation, int parentId, int playerId, float destroyTimer, float timePassed)
    {
        float castTime = _abilityDictionary[id].CastTime;
        castTime -= timePassed;
        castTime = Mathf.Max(castTime, 0f);

        if(IsOwner && _manager.PlayerManager.Linked.Value)
            _manager.PlayerManager.Stats.AddTempNerf(Stat.MoveSpeed, _abilityDictionary[id].SlowPercent / 100, castTime);

        Entity entityInterface = _manager.PlayerManager;

        if (!entityInterface.CheckForAbilityPool(id))
            entityInterface.AddAbilityPrefab(_abilityDictionary[id]);

        AbilityController ability = entityInterface.GetPooledAbility(id);
        
        ability.transform.position = spawnPos;
        ability.transform.rotation = rotation;
        ability.transform.parent = GameManager.Instance.AbilityManager.PlayerAbilityParent;

        GameManager.Instance.AbilityManager.AddPlayerAbility(ability, playerId, uniqueId);

        await Task.Delay((int) (castTime * 1000));

        ability.gameObject.SetActive(true); 

        PlayerManager player = GameManager.Instance.Players[playerId];

        if (parentId != -1)
            ability.transform.parent = ClientManager.Objects.Spawned[parentId].transform;

        ability.Initialize(player, uniqueId, Mathf.Max(timePassed - castTime, 0f));

        if (destroyTimer > 0f)
            ability.SetDespawnTimer(destroyTimer);

        if (parentId != -1 && !_manager.PlayerManager.Linked.Value)
            ability.transform.localPosition = Vector3.zero;

        _lastSpawnedAbility = ability;
        if (_setLastSpawnedAbility != null)
            _setLastSpawnedAbility.Invoke(ability);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DestroyableTerrain"))
            other.GetComponent<DestroyableTerrain>().TakeDamage((int) _manager.PlayerManager.Stats.GetStat(Stat.Damage) * _jabDamageModifier);

        if (!other.CompareTag("Enemy"))
            return;

        Enemy enemy = other.GetComponent<Enemy>();

        if (_jabbing)
        {
            if (_enemiesHitByJab.Contains(enemy))
                return;

            enemy.TakeDamage((int) _manager.PlayerManager.Stats.GetStat(Stat.Damage) * _jabDamageModifier, _manager.PlayerManager);
            if (_jabModifier == 1)
                enemy.Push(-transform.forward, 10f, .2f);
            else if (_jabModifier == 2)
                enemy.Push(transform.forward, 5f * _jabDamageModifier);
            else
                enemy.Push(transform.forward, 10f);

            _enemiesHitByJab.Add(enemy);
        }
    }

    #region PrimaryAbilities
    public void BulletCommand(int modifier)
    {
        Vector3 spawnPos = transform.position + transform.forward;
        if (_manager.PlayerManager.Linked.Value)
            spawnPos = _manager.PlayerManager.ProjectileSpawnPoint.position;

        SpawnAbility(_bulletPrefabs[modifier].Id, spawnPos, transform.rotation);
    }

    public void SlashCommand(int modifier)
    {
        Vector3 spawnPos = transform.position;
        if (_manager.PlayerManager.Linked.Value)
            spawnPos += transform.forward * 2;

        if (modifier == 2)
            SpawnAbility(_slashPrefabs[modifier].Id, spawnPos, transform.rotation);
        else if (modifier == 1)
        {
            AbilityController slash = GameManager.Instance.AbilityManager.GetPlayerAbility(OwnerId, _slashPrefabs[modifier].Id);
            if (slash == null)
            {
                SpawnAbility(_slashPrefabs[modifier].Id, spawnPos, transform.rotation, ObjectId);
                _manager.MovementController.Lock(true);
                _manager.MovementController.Push(transform.forward, 25f, 0f);
            }
            else
            {
                _manager.MovementController.Lock(false);
                slash.ReleaseObject();
                ReleaseSlashServer(slash.Id);
            }
        }
        else
            SpawnAbility(_slashPrefabs[modifier].Id, spawnPos, transform.rotation, ObjectId);
    }
    
    [ServerRpc]
    private void ReleaseSlashServer(int id)
    {
        ReleaseSlashRpc(id);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void ReleaseSlashRpc(int id)
    {
        GameManager.Instance.AbilityManager.GetPlayerAbility(OwnerId, id).ReleaseObject();
    }

    public async void JabCommand(int modifier, bool pressed, bool linked = false)
    {
        if(linked)
        {
            SpawnAbility(_jabLinkedAbility.Id, _manager.PlayerManager.PunchSpawnPoint.position, _manager.PlayerManager.PunchSpawnPoint.rotation, ObjectId);
            return;
        }

        if(modifier == 2)
        {
            if (pressed)
            {
                if (!_jabbing)
                {
                    _manager.MovementController.Lock(true);
                    _manager.AnimationController.Jab(modifier);
                    _jabStartTime = Time.time;
                    _jabDamageModifier = 1;
                    _jabChargeCounter = 0;
                }
                else
                {
                    if(Time.time > _jabStartTime + (_jabChargeCounter + 1) * _manager.PlayerManager.Stats.GetStat(Stat.AttackSpeed))
                    {
                        _jabChargeCounter++;
                        _jabDamageModifier++;
                    }
                }
            }
            else if(_jabStartTime + .7f < Time.time)
            {
                _manager.AnimationController.Jab(modifier);
                _source.clip = _jabAwakeSound;
                GameManager.Instance.SoundManager.PlaySound(_source);
                Invoke(nameof(EndJab), .85f * _manager.PlayerManager.Stats.GetStat(Stat.AttackSpeed));
                _manager.MovementController.Lock(true, .85f * _manager.PlayerManager.Stats.GetStat(Stat.AttackSpeed));
            }
            else
            {
                await Task.Delay(700);
                JabCommand(modifier, pressed);
            }
        }
        else
        {
            _manager.MovementController.Lock(true, .85f * _manager.PlayerManager.Stats.GetStat(Stat.AttackSpeed));
            Invoke(nameof(EndJab), .85f * _manager.PlayerManager.Stats.GetStat(Stat.AttackSpeed));
            _manager.AnimationController.Jab(modifier);
            _source.clip = _jabAwakeSound;
            GameManager.Instance.SoundManager.PlaySound(_source);
        }

        if (_jabbing)
            return;

        _enemiesHitByJab = new List<Enemy>();
        ResetCollider();
        _jabbing = true;
        _jabModifier = modifier;
    }

    private void EndJab()
    {
        _jabbing = false;
    }
    #endregion

    #region SecondaryAbilities

    public void GrenadeCommand(int modifier)
    {
        SpawnAbility(_grenadePrefabs[modifier].Id, transform.position + transform.forward, transform.rotation);
    }

    public void HookCommand(int modifier)
    {
        SpawnAbility(_hookPrefabs[modifier].Id, transform.position + transform.forward, transform.rotation);
    }

    public void ShootFreezeRayCommand(int modifier)
    {
        SpawnAbility(_freezeRayPrefabs[modifier].Id, transform.position + transform.forward, transform.rotation);
    }

    #endregion

    #region UtilityAbilites

    public void ShieldCommand(int modifier, bool held)
    {
        if (_utilityResource < 1)
            return;

        if(modifier == 2)
        {
            if (held && _utilityResource > 1)
            {
                if (_lastUtilitySpendCall + _shieldMod2ChargeRate > Time.time)
                    return;

                SpendUtility();
                _shieldMod2Counter++;
                _lastUtilitySpendCall = Time.time;

            }
            else
            {
                if(_shieldMod2Counter > 0)
                {
                    Vector3 spawnPos = transform.position + transform.forward * 5;
                    spawnPos.y = 0f;
                    SpawnAbility(_shieldPrefabs[modifier].Id, spawnPos, transform.rotation, _shieldMod2Counter * .25f);
                    _shieldMod2Counter = 0;
                }

                InvokeRepeating(nameof(AddUtility), _utilityRegenRate, _utilityRegenRate);
                CancelInvoke(nameof(SpendUtility));
            }
        }
        else
        {
            _utilityActive = !_utilityActive;
            RpcShieldCommand(_utilityActive);

            if (modifier != 1)
                _manager.MovementController.Lock(_utilityActive);

            if (_utilityObject == null)
            {
                SpawnAbility(_shieldPrefabs[modifier].Id, transform.position, transform.rotation, ObjectId);
                _setLastSpawnedAbility += SetShield;
                _utilityActive = true;
                InvokeRepeating(nameof(SpendUtility), .1f, .1f);
                return;
            }

            _utilityObject.SetActive(_utilityActive);

            if (_utilityActive)
            {
                _source.clip = _shieldAwakeSound;
                GameManager.Instance.SoundManager.PlaySound(_source);
            }
            else
            {
                _source.clip = _shieldDestroySound;
                GameManager.Instance.SoundManager.PlaySound(_source);
            }

            if (_utilityActive)
            {
                InvokeRepeating(nameof(SpendUtility), .1f, .1f);
                CancelInvoke(nameof(AddUtility));
            }
            else
            {
                InvokeRepeating(nameof(AddUtility), _utilityRegenRate, _utilityRegenRate);
                CancelInvoke(nameof(SpendUtility));
            }
        }
    }

    private void SetShield(AbilityController shield)
    {
        _utilityObject = shield.gameObject;
        _setLastSpawnedAbility -= SetShield;
    }

    [ServerRpc]
    private void RpcShieldCommand(bool enabled)
    {
        RpcShieldEnable(enabled);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void RpcShieldEnable(bool enabled)
    {
        if (_utilityObject == null)
        {
            _setLastSpawnedAbility += SetShield;
            return;
        }

        _utilityObject.SetActive(enabled);

        if (enabled)
        {
            _source.clip = _shieldAwakeSound;
            GameManager.Instance.SoundManager.PlaySound(_source);
        }
        else
        {
            _source.clip = _shieldDestroySound;
            GameManager.Instance.SoundManager.PlaySound(_source);
        }
    }

    public void SwitchPlacesCommand(int modifier)
    {
        if (_utilityResource < 30)
            return;

        GameManager.Instance.CameraController.LockCamera(true, .2f);

        Vector3 playerPos = transform.position;
        playerPos.y = 0f;

        Vector3 friendPos = _manager.PlayerManager.transform.position;
        friendPos.y = 1.5f;
        GameObject particle = Instantiate(_switchPlacesParticle, friendPos, _switchPlacesParticle.transform.rotation);
        particle.transform.localScale *= 2f;
        Spawn(particle);
        _source.clip = _switchPlacesAwakeSound;
        GameManager.Instance.SoundManager.PlaySound(_source);

        if (modifier == 1)
            SpawnAbility(_switchPlacesDamageModifier.Id, friendPos, _switchPlacesDamageModifier.transform.rotation);
        else if (modifier == 2)
            _manager.PlayerManager.SetInvisible(true, 1f);

        RpcSwitchPlaces(OwnerId, playerPos, friendPos);
        _manager.MovementController.MoveToPositionCommand(friendPos);
        particle = Instantiate(_switchPlacesParticle, playerPos, _switchPlacesParticle.transform.rotation);
        Spawn(particle);

        if (modifier == 0)
            _utilityResource -= 20;
        else
            _utilityResource -= 30;

        if (!IsInvoking(nameof(AddUtility)))
            InvokeRepeating(nameof(AddUtility), _utilityRegenRate, _utilityRegenRate);
    }

    [ServerRpc]
    private void RpcSwitchPlaces(int playerId, Vector3 playerPos, Vector3 friendPos)
    {
        PlayerManager player = GameManager.Instance.Players[playerId];
        player.transform.position = playerPos;
        player.PlayerFriendController.FriendManager.transform.position = friendPos;
    }

    public void BecomeInvisibleCommand(int modifier)
    {
        if (_utilityResource < 1)
            return;

        _utilityActive = !_utilityActive;
        _manager.PlayerManager.SetInvisible(_utilityActive);
        if (modifier == 1)
        {
            if (_utilityActive)
                _manager.PlayerManager.Stats.AddBuff(Stat.MoveSpeed, .5f);
            else
                _manager.PlayerManager.Stats.AddBuff(Stat.MoveSpeed, -.5f);
        }
        else if(modifier == 2)
        {
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Terrain"), LayerMask.NameToLayer("Player"), _utilityActive);
            if(!_utilityActive)
                if(Physics.OverlapSphere(_manager.PlayerManager.transform.position, 1f, 1 << LayerMask.NameToLayer("Terrain")).Length != 0)
                {
                    NavMesh.SamplePosition(_manager.PlayerManager.transform.position, out NavMeshHit hit, 100f, NavMesh.AllAreas);
                    hit.position = new Vector3(hit.position.x, 0f, hit.position.z);
                    _manager.PlayerManager.transform.position = hit.position;
                }
        }

        if (_utilityActive)
        {
            if(modifier == 0)
                InvokeRepeating(nameof(SpendUtility), .1f, .1f);
            else
                InvokeRepeating(nameof(SpendUtility), .05f, .05f);

            CancelInvoke(nameof(AddUtility));

            _source.clip = _becomeInvisibleAwakeSound;
            GameManager.Instance.SoundManager.PlaySound(_source);
        }
        else
        {
            InvokeRepeating(nameof(AddUtility), _utilityRegenRate, _utilityRegenRate);
            CancelInvoke(nameof(SpendUtility));
        }
    }

    private void SpendUtility()
    {
        if (_utilityResource < 2)
        {
            _manager.PlayerManager.PlayerFriendController.UtilityRanOut();
            return;
        }

        _utilityResource--;
        GameManager.Instance.UIManager.ChangeAbilityBarSize(2, (float)_utilityResource /(float) _maxUtility);
    }

    private void AddUtility()
    {
        if (_utilityResource >= _maxUtility)
        {
            CancelInvoke(nameof(AddUtility));
            return;
        }

        _utilityResource += _maxUtility / 100;
        GameManager.Instance.UIManager.ChangeAbilityBarSize(2, (float)_utilityResource / (float)_maxUtility);
    }

    #endregion

    #region UltimateAbilities

    public void NukeCommand(int modifier)
    {
        SpawnAbility(_nukePrefabs[modifier].Id, transform.position, transform.rotation);
    }

    public void HealPulseCommand(int modifier)
    {
        Quaternion rotation = transform.rotation;
        Vector3 spawnPos = transform.position;
        if (modifier != 2)
            spawnPos.y = 0.1f;
        else
            rotation = Quaternion.Euler((_manager.PlayerManager.transform.position - transform.position).normalized);

        SpawnAbility(_healPulsePrefabs[modifier].Id, spawnPos, rotation);
    }

    public void FrenzyCommand(int modifier)
    {
        if (modifier == 2)
            SpawnAbility(_frenzyPrefabs[modifier].Id, transform.position, transform.rotation, ObjectId);
        else
            SpawnAbility(_frenzyPrefabs[modifier].Id, transform.position, transform.rotation);
    }

    #endregion

    public void UpgradeUtility(int amount)
    {
        _maxUtility += amount;
        GameManager.Instance.UIManager.ChangeAbilityBarSize(2, (float)_utilityResource / (float)_maxUtility);
        if(!_utilityActive && !IsInvoking(nameof(AddUtility)))
            InvokeRepeating(nameof(AddUtility), .2f, .2f);
    }

    private void ResetCollider()
    {
        _sphereCollider.enabled = false;
        _sphereCollider.enabled = true;
    }
}
