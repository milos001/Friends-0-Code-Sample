using FishNet.Object;
using FishNet;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;

public class FriendAbilityController : NetworkBehaviour
{
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

    public void Init(FriendManager friendManager)
    {
        _manager = friendManager;
    }

    private void SpawnAbility(AbilityController prefab, Vector3 spawnPos, Quaternion rotation, int modifier, float destroyTimer = 0f)
    {
        if (CrossSceneValues.Instance.Multiplayer)
            RpcSpawnAbility(prefab, spawnPos, rotation, null, _manager.PlayerManager, destroyTimer);
        else
        {
            Instantiate(prefab, spawnPos, rotation, null);
        }
    }

    private void SpawnAbility(AbilityController prefab, Vector3 spawnPos, Quaternion rotation, int modifier, Transform parentTs, float destroyTimer = 0f)
    {
        if (CrossSceneValues.Instance.Multiplayer)
            RpcSpawnAbility(prefab, spawnPos, rotation, parentTs, _manager.PlayerManager, destroyTimer);
        else
        {
            Instantiate(prefab, spawnPos, rotation, parentTs);
        }
    }

    private void SpawnAbility(AbilityController[] prefabs, Vector3 spawnPos, Quaternion rotation, int modifier, float destroyTimer = 0f)
    {
        if (CrossSceneValues.Instance.Multiplayer)
            RpcSpawnAbility(prefabs[modifier], spawnPos, rotation, null, _manager.PlayerManager, destroyTimer);
        else
        {
            Instantiate(prefabs[modifier], spawnPos, rotation, null);
        }
    }

    private void SpawnAbility(AbilityController[] prefabs, Vector3 spawnPos, Quaternion rotation, int modifier, Transform parentTs, float destroyTimer = 0f)
    {
        if (CrossSceneValues.Instance.Multiplayer)
            RpcSpawnAbility(prefabs[modifier], spawnPos, rotation, parentTs, _manager.PlayerManager, destroyTimer);
        else
        {
            Instantiate(prefabs[modifier], spawnPos, rotation, parentTs);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RpcSpawnAbility(AbilityController prefab, Vector3 spawnPos, Quaternion rotation, Transform parentTs, PlayerManager playerManager, float destroyTimer)
    {
        AbilityController ability = null;
        if (parentTs == null)
            ability = Instantiate(prefab, spawnPos, rotation);
        else
            ability = Instantiate(prefab, spawnPos, rotation, parentTs);

        InstanceFinder.ServerManager.Spawn(ability.gameObject, playerManager.Owner);

        if(destroyTimer > 0f)
            ability.SetDestroyTimer(destroyTimer);

        if (parentTs != null)
            ability.transform.localPosition = Vector3.zero;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DestroyableTerrain"))
            other.GetComponent<DestroyableTerrain>().TakeDamage(_manager.PlayerManager.Damage * _jabDamageModifier);

        if (!other.CompareTag("Enemy"))
            return;

        Enemy enemy = other.GetComponent<Enemy>();

        if (_jabbing)
        {
            if (_enemiesHitByJab.Contains(enemy))
                return;

            enemy.TakeDamage(_manager.PlayerManager.Damage * _jabDamageModifier, transform.position, _manager.PlayerManager);
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
        if (_manager.Linked)
            spawnPos = _manager.PlayerManager.ProjectileSpawnPoint.position;

        SpawnAbility(_bulletPrefabs, spawnPos, transform.rotation, modifier);
    }

    public void SlashCommand(int modifier)
    {
        if (modifier == 2)
            SpawnAbility(_slashPrefabs, transform.position, transform.rotation, modifier);
        else if (modifier == 1)
        {
            NetworkObject[] objects = Owner.Objects.Where(x => x.CompareTag("Slash")).ToArray();
            if (objects == null || objects.Length == 0f)
            {
                SpawnAbility(_slashPrefabs, transform.position, transform.rotation, modifier, transform);
                _manager.MovementController.Lock(true);
                _manager.MovementController.Push(transform.forward, 25f, 0f);
            }
            else
            {
                _manager.MovementController.Lock(false);
                Destroy(objects.First().gameObject);
            }
        }
        else
        {
            if (_manager.Linked)
                SpawnAbility(_slashPrefabs, transform.position + transform.forward * 2, transform.rotation, modifier, transform);
            else
                SpawnAbility(_slashPrefabs, transform.position, transform.rotation, modifier, transform);
        }
    }

    public async void JabCommand(int modifier, bool pressed, bool linked = false)
    {
        if(linked)
        {
            SpawnAbility(_jabLinkedAbility, _manager.PlayerManager.PunchSpawnPoint.position, _manager.PlayerManager.PunchSpawnPoint.rotation, modifier, transform);
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
                    if(Time.time > _jabStartTime + (_jabChargeCounter + 1) * _manager.PlayerManager.AttackSpeed)
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
                Invoke(nameof(EndJab), .85f * _manager.PlayerManager.AttackSpeed);
                _manager.MovementController.Lock(true, .85f * _manager.PlayerManager.AttackSpeed);
            }
            else
            {
                await Task.Delay(700);
                JabCommand(modifier, pressed);
            }
        }
        else
        {
            _manager.MovementController.Lock(true, .85f * _manager.PlayerManager.AttackSpeed);
            Invoke(nameof(EndJab), .85f * _manager.PlayerManager.AttackSpeed);
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
        SpawnAbility(_grenadePrefabs, transform.position + transform.forward, transform.rotation, modifier);
    }

    public void HookCommand(int modifier)
    {
        SpawnAbility(_hookPrefabs, transform.position + transform.forward, transform.rotation, modifier);
    }

    public void ShootFreezeRayCommand(int modifier)
    {
        SpawnAbility(_freezeRayPrefabs, transform.position + transform.forward, transform.rotation, modifier);
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
                    SpawnAbility(_shieldPrefabs, spawnPos, transform.rotation, modifier, _shieldMod2Counter * .25f);
                    _shieldMod2Counter = 0;
                }

                InvokeRepeating(nameof(AddUtility), _utilityRegenRate, _utilityRegenRate);
                CancelInvoke(nameof(SpendUtility));
            }
        }
        else
        {
            if (_utilityObject == null)
            {
                NetworkObject[] objects = Owner.Objects.Where(x => x.CompareTag("Shield")).ToArray();
                
                if (objects == null || objects.Length == 0)
                {
                    SpawnAbility(_shieldPrefabs, transform.position, transform.rotation, modifier, transform);
                    _utilityActive = true;
                    InvokeRepeating(nameof(SpendUtility), .1f, .1f);
                    return;
                }
                else
                    _utilityObject = objects.First().gameObject;
            }

            _utilityActive = !_utilityActive;
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

            RpcShieldCommand(_utilityObject, _utilityActive);

            if (modifier != 1)
                _manager.MovementController.Lock(_utilityActive);

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

    [ServerRpc]
    private void RpcShieldCommand(GameObject utilityObject, bool enabled)
    {
        RpcShieldEnable(utilityObject, enabled);
    }

    [ObserversRpc]
    private void RpcShieldEnable(GameObject utilityObject, bool enabled)
    {
        utilityObject.SetActive(enabled);

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
            SpawnAbility(_switchPlacesDamageModifier, friendPos, _switchPlacesDamageModifier.transform.rotation, modifier);
        else if (modifier == 2)
            _manager.PlayerManager.SetInvisible(true, 1f);

        RpcSwitchPlaces(_manager.PlayerManager.transform, playerPos, transform, friendPos);
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
    private void RpcSwitchPlaces(Transform playerTs, Vector3 playerPos, Transform friendTs, Vector3 friendPos)
    {
        playerTs.position = playerPos;
        friendTs.position = friendPos;
    }

    public void BecomeInvisibleCommand(int modifier)
    {
        if (_utilityResource < 1)
            return;

        _utilityActive = !_utilityActive;
        _manager.PlayerManager.SetInvisible(_utilityActive);
        if (modifier == 1 && _utilityActive)
            _manager.PlayerManager.SetTempMoveSpeed(1.5f, 1);
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
        SpawnAbility(_nukePrefabs, transform.position, transform.rotation, modifier);
    }

    public void HealPulseCommand(int modifier)
    {
        Quaternion rotation = transform.rotation;
        Vector3 spawnPos = transform.position;
        if (modifier != 2)
            spawnPos.y = 0.1f;
        else
            rotation = Quaternion.Euler((_manager.PlayerManager.transform.position - transform.position).normalized);

        SpawnAbility(_healPulsePrefabs, spawnPos, rotation, modifier);
    }

    public void FrenzyCommand(int modifier)
    {
        if (modifier == 2)
            SpawnAbility(_frenzyPrefabs, transform.position, transform.rotation, modifier, transform);
        else
            SpawnAbility(_frenzyPrefabs, transform.position, transform.rotation, modifier);
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
