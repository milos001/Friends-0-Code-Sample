using FishNet.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

public class PlayerFriendController : NetworkBehaviour
{
    public bool Locked;

    [SerializeField]
    private GameObject _movePointPrefab;

    [HideInInspector]
    public FriendManager FriendManager;

    private PlayerManager _playerManager;

    private Transform _movePoint;

    private Dictionary<string, AbilityModule> _primaryAbilites, _secondaryAbilities, _utilityAbilites, _ultimateAbilites;
    private AbilityModule _currentPrimaryAbility, _currentSecondaryAbility, _currentUtilityAbility, _currentUltimateAbility;
    private int _primaryModifier, _secondaryModifier, _utilityModifier, _ultimateModifier;

    private Vector3 _initialCameraPosition = new Vector3(0f, .75f, 0f);

    private float _primaryAbilityLastUse, _secondaryAbilityLastUse, _utilityAbilityLastUse, _ultimateAbilityLastUse;

    private int _primaryIndex, _secondaryIndex, _utilityIndex, _ultimateIndex;
    private bool _secondaryLocked, _utilityLocked, _ultimateLocked;
    private bool _primaryHeld, _secondaryHeld, _utilityHeld, _ultimateHeld;
    private bool _movingFriend, _moveFriendHeld;
    private float _moveFriendHoldDuration;

    private bool _abilitiesLocked;
    private AbilityModule? _lockingAbility;

    private bool _pressed;
    private bool _initialized;

    [ObserversRpc]
    private void InitForObservers(int friendId)
    {
        FriendManager = ClientManager.Objects.Spawned[friendId].GetComponent<FriendManager>();
    }

    [ServerRpc]
    private void InitForServer(int friendId)
    {
        InitForObservers(friendId);
    }

    public void Init(PlayerManager playerManager, FriendManager friendManager)
    {
        FriendManager = friendManager;
        _playerManager = playerManager;

        InitForServer(friendManager.ObjectId);

        _primaryAbilites = new Dictionary<string, AbilityModule>()
        {
            {"Bullet", new AbilityModule(nameof(BulletAbility), .35f, AbilityType.Repeating, AbilitySlot.Primary, true)},
            {"Slash", new AbilityModule(nameof(SlashAbility), .5f, AbilityType.Repeating, AbilitySlot.Primary, true) },
            {"Jab", new AbilityModule(nameof(JabAbility), .9f, AbilityType.Repeating, AbilitySlot.Primary, true) }
        };

        _secondaryAbilities = new Dictionary<string, AbilityModule>()
        {
            {"Grenade", new AbilityModule(nameof(GrenadeAbility), 8f, AbilityType.OneOff, AbilitySlot.Secondary, true)},
            {"Hook", new AbilityModule(nameof(HookAbility), 2.5f, AbilityType.OneOff, AbilitySlot.Secondary, true, .1f) },
            {"FreezeRay", new AbilityModule(nameof(FreezeRayAbility), 5f, AbilityType.OneOff, AbilitySlot.Secondary, true, .1f)}
        };

        _utilityAbilites = new Dictionary<string, AbilityModule>()
        {
            {"Shield", new AbilityModule(nameof(ShieldAbility), .1f, AbilityType.OnAndOff, AbilitySlot.Utility, false) },
            {"SwitchPlaces", new AbilityModule(nameof(SwitchPlacesAbility), .1f, AbilityType.OneOff, AbilitySlot.Utility, true, .1f)},
            {"BecomeInvisible", new AbilityModule(nameof(BecomeInvisibleAbility), .1f, AbilityType.OnAndOff, AbilitySlot.Utility, true, .2f) }
        };

        _ultimateAbilites = new Dictionary<string, AbilityModule>()
        {
            {"Nuke", new AbilityModule(nameof(NukeAbility), 30f, AbilityType.OneOff, AbilitySlot.Ultimate, false, 1.75f)},
            {"HealPulse", new AbilityModule(nameof(HealPulseAbility), 30f, AbilityType.OneOff, AbilitySlot.Ultimate, false, 5f)},
            {"Frenzy", new AbilityModule(nameof(FrenzyAbility), 30f, AbilityType.OneOff, AbilitySlot.Ultimate, true, .5f) }
        };

        _primaryModifier = CrossSceneValues.Instance.PrimaryModifier;
        _secondaryModifier = CrossSceneValues.Instance.SecondaryModifier;
        _utilityModifier = CrossSceneValues.Instance.UtilityModifier;
        _ultimateModifier = CrossSceneValues.Instance.UltimateModifier;

        if (CrossSceneValues.Instance.PrimaryAbility == "Jab" && _primaryModifier == 2)
            _primaryAbilites["Jab"] = new AbilityModule(nameof(JabAbility), 0f, AbilityType.Charging, AbilitySlot.Primary, false);

        if (CrossSceneValues.Instance.PrimaryAbility == "Slash" && _primaryModifier == 1)
            _primaryAbilites["Slash"] = new AbilityModule(nameof(SlashAbility), .1f, AbilityType.OnAndOff, AbilitySlot.Primary, false);

        if (CrossSceneValues.Instance.UtilityAbility == "Shield")
        {
            if (_utilityModifier == 2)
                _utilityAbilites["Shield"] = new AbilityModule(nameof(ShieldAbility), 0f, AbilityType.Charging, AbilitySlot.Utility, false);
            else if (_utilityModifier == 1)
                _utilityAbilites["Shield"] = new AbilityModule(nameof(ShieldAbility), .1f, AbilityType.OnAndOff, AbilitySlot.Utility, true);
        }

        if (CrossSceneValues.Instance.SecondaryAbility == "Hook" && _secondaryModifier != 0)
            _secondaryAbilities["Hook"] = new AbilityModule(nameof(HookAbility), 5f, AbilityType.OneOff, AbilitySlot.Secondary, true, .1f);

        if (CrossSceneValues.Instance.UltimateAbility == "Nuke" && _ultimateModifier == 0)
            _ultimateAbilites["Nuke"] = new AbilityModule(nameof(NukeAbility), 20f, AbilityType.OneOff, AbilitySlot.Ultimate, false, 1.75f);

        if (CrossSceneValues.Instance.UltimateAbility == "HealPulse" && _ultimateModifier == 2)
            _ultimateAbilites["HealPulse"] = new AbilityModule(nameof(HealPulseAbility), 6f, AbilityType.OneOff, AbilitySlot.Ultimate, true);

        if (CrossSceneValues.Instance.UltimateAbility == "Frenzy" && _ultimateModifier == 0)
            _ultimateAbilites["Frenzy"] = new AbilityModule(nameof(FrenzyAbility), 20f, AbilityType.OneOff, AbilitySlot.Ultimate, true, .75f);

        _currentPrimaryAbility = _primaryAbilites[CrossSceneValues.Instance.PrimaryAbility];
        _currentSecondaryAbility = _secondaryAbilities[CrossSceneValues.Instance.SecondaryAbility];
        _currentUtilityAbility = _utilityAbilites[CrossSceneValues.Instance.UtilityAbility];
        _currentUltimateAbility = _ultimateAbilites[CrossSceneValues.Instance.UltimateAbility];

        _primaryAbilityLastUse = Time.time - _currentPrimaryAbility.Cooldown;
        _secondaryAbilityLastUse = Time.time - _currentSecondaryAbility.Cooldown;
        _utilityAbilityLastUse = Time.time - _currentUtilityAbility.Cooldown;
        _ultimateAbilityLastUse = Time.time - _currentUltimateAbility.Cooldown;

        _primaryIndex = _primaryAbilites.Values.ToList().IndexOf(_currentPrimaryAbility);
        _secondaryIndex = _secondaryAbilities.Values.ToList().IndexOf(_currentSecondaryAbility);
        _utilityIndex = _utilityAbilites.Values.ToList().IndexOf(_currentUtilityAbility);
        _ultimateIndex = _ultimateAbilites.Values.ToList().IndexOf(_currentUltimateAbility);

        SetInputListeners();
        _playerManager.PlayerAnimationController.SetLinkMode(CrossSceneValues.Instance.PrimaryAbility);
        _initialized = true;
    }

    private void SetInputListeners()
    {
        _playerManager.InputActions.Player.FriendMovement.started += ctx => _movingFriend = true;
        _playerManager.InputActions.Player.FriendMovement.canceled += ctx => _movingFriend = false;
        _playerManager.InputActions.Player.MoveFriend.started += ctx =>
        {
            _moveFriendHeld = true;
        };
        _playerManager.InputActions.Player.MoveFriend.canceled += ctx =>
        {
            _moveFriendHeld = false;
        };
        _playerManager.InputActions.Player.FriendLockOn.performed += ctx => LockOnFriendToNearestEnemy();
        _playerManager.InputActions.Player.FriendLockOnDir.performed += ctx => LockOnFriendDir(ctx.ReadValue<Vector2>());

        switch (_currentPrimaryAbility.AbilityType)
        {
            case AbilityType.OneOff:
                _playerManager.InputActions.Player.Primary.performed += ctx => UseAbility(_currentPrimaryAbility, ref _primaryAbilityLastUse, true);
                break;
            case AbilityType.OnAndOff:
                _playerManager.InputActions.Player.Primary.performed += ctx => UseAbility(_currentPrimaryAbility, ref _primaryAbilityLastUse, true);
                break;
            case AbilityType.Repeating:
                _playerManager.InputActions.Player.Primary.started += ctx => _primaryHeld = true;
                _playerManager.InputActions.Player.Primary.canceled += ctx => _primaryHeld = false;
                break;
            case AbilityType.Charging:
                _playerManager.InputActions.Player.Primary.started += ctx => _primaryHeld = true;
                _playerManager.InputActions.Player.Primary.canceled += ctx => _primaryHeld = false;
                _playerManager.InputActions.Player.Primary.canceled += ctx => UseAbility(_currentPrimaryAbility, ref _primaryAbilityLastUse, false);
                break;
        }

        switch (_currentSecondaryAbility.AbilityType)
        {
            case AbilityType.OneOff:
                _playerManager.InputActions.Player.Secondary.performed += ctx => UseAbility(_currentSecondaryAbility, ref _secondaryAbilityLastUse, true);
                break;
            case AbilityType.OnAndOff:
                _playerManager.InputActions.Player.Secondary.performed += ctx => UseAbility(_currentSecondaryAbility, ref _secondaryAbilityLastUse, true);
                break;
            case AbilityType.Repeating:
                _playerManager.InputActions.Player.Secondary.started += ctx => _secondaryHeld = true;
                _playerManager.InputActions.Player.Secondary.canceled += ctx => _secondaryHeld = false;
                break;
            case AbilityType.Charging:
                _playerManager.InputActions.Player.Secondary.started += ctx => _secondaryHeld = true;
                _playerManager.InputActions.Player.Secondary.canceled += ctx => _secondaryHeld = false;
                _playerManager.InputActions.Player.Secondary.canceled += ctx => UseAbility(_currentSecondaryAbility, ref _secondaryAbilityLastUse, false);
                break;
        }

        switch (_currentUtilityAbility.AbilityType)
        {
            case AbilityType.OneOff:
                _playerManager.InputActions.Player.Utility.performed += ctx => UseAbility(_currentUtilityAbility, ref _utilityAbilityLastUse, true);
                break;
            case AbilityType.OnAndOff:
                _playerManager.InputActions.Player.Utility.performed += ctx => UseAbility(_currentUtilityAbility, ref _utilityAbilityLastUse, true);
                break;
            case AbilityType.Repeating:
                _playerManager.InputActions.Player.Utility.started += ctx => _utilityHeld = true;
                _playerManager.InputActions.Player.Utility.canceled += ctx => _utilityHeld = false;
                break;
            case AbilityType.Charging:
                _playerManager.InputActions.Player.Utility.started += ctx => _utilityHeld = true;
                _playerManager.InputActions.Player.Utility.canceled += ctx => _utilityHeld = false;
                _playerManager.InputActions.Player.Utility.canceled += ctx => UseAbility(_currentUtilityAbility, ref _utilityAbilityLastUse, false);
                break;
        }

        switch (_currentUltimateAbility.AbilityType)
        {
            case AbilityType.OneOff:
                _playerManager.InputActions.Player.Ultimate.performed += ctx => UseAbility(_currentUltimateAbility, ref _ultimateAbilityLastUse, true);
                break;
            case AbilityType.OnAndOff:
                _playerManager.InputActions.Player.Ultimate.performed += ctx => UseAbility(_currentUltimateAbility, ref _ultimateAbilityLastUse, true);
                break;
            case AbilityType.Repeating:
                _playerManager.InputActions.Player.Ultimate.started += ctx => _ultimateHeld = true;
                _playerManager.InputActions.Player.Ultimate.canceled += ctx => _ultimateHeld = false;
                break;
            case AbilityType.Charging:
                _playerManager.InputActions.Player.Ultimate.started += ctx => _ultimateHeld = true;
                _playerManager.InputActions.Player.Ultimate.canceled += ctx => _ultimateHeld = false;
                _playerManager.InputActions.Player.Ultimate.canceled += ctx => UseAbility(_currentUltimateAbility, ref _ultimateAbilityLastUse, false);
                break;
        }
    }

    #region GetInput
    private void Update()
    {
        if (!_initialized)
            return;

        UpdateAbilityCooldowns();

        if (_playerManager.BlockInput)
            return;

        if (_primaryHeld)
            UseAbility(_currentPrimaryAbility, ref _primaryAbilityLastUse, true);
        if (_secondaryHeld)
            UseAbility(_currentSecondaryAbility, ref _secondaryAbilityLastUse, true);
        if (_utilityHeld)
            UseAbility(_currentUtilityAbility, ref _utilityAbilityLastUse, true);
        if (_ultimateHeld)
            UseAbility(_currentUltimateAbility, ref _ultimateAbilityLastUse, false);

        if (_moveFriendHeld)
            _moveFriendHoldDuration += Time.deltaTime;

        if (_moveFriendHoldDuration > .3f)
        {
            RecallFriend();
            _moveFriendHoldDuration = 0f;
            _moveFriendHeld = false;
        }
        else if (!_moveFriendHeld && _moveFriendHoldDuration > 0f)
        {
            SendFriendToPosition(true);
            _moveFriendHoldDuration = 0f;
        }

        if (!_movingFriend)
            return;

        Vector2 friendMovement = _playerManager.InputActions.Player.FriendMovement.ReadValue<Vector2>();
        if (friendMovement.magnitude > .1f)
            SendFriendToPosition(false, friendMovement);
    }
    #endregion

    #region PositionFriend
    private void SendFriendToPosition(bool mouse, Vector3 position = new Vector3())
    {
        if (FriendManager.Stunned || FriendManager.Emoting || _playerManager.IsDead.Value || _playerManager.BlockInput)
            return;

        if (mouse)
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 1000f, 1 << LayerMask.NameToLayer("Ground") | 1 << LayerMask.NameToLayer("Player")))
            {
                if (hit.collider.CompareTag("Player"))
                    return;

                if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 10f, NavMesh.AllAreas))
                    FriendManager.MovementController.MoveToPositionCommand(navHit.position);
            }

            if (_movePoint == null)
                _movePoint = Instantiate(_movePointPrefab, hit.point, Quaternion.identity).transform;
            else
            {
                _movePoint.gameObject.SetActive(true);
                _movePoint.transform.position = hit.point;
            }
        }
        else
        {
            Vector3 nextPos = FriendManager.transform.position + new Vector3(position.x, 0f, position.y) * 25f * Time.deltaTime;
            FriendManager.MovementController.MoveToPositionCommand(nextPos, false);
        }
    }

    private void RecallFriend()
    {
        if (FriendManager.Stunned || FriendManager.Emoting || _playerManager.BlockInput)
            return;

        FriendManager.MovementController.RecallCommand();
        if (_movePoint != null)
            _movePoint.gameObject.SetActive(false);
    }

    private void LockOnFriendToNearestEnemy()
    {
        if (FriendManager.Stunned || FriendManager.Emoting || _playerManager.BlockInput)
            return;

        FriendManager.MovementController.LockOnCommand();
    }

    private void LockOnFriendDir(Vector2 dir)
    {
        if (FriendManager.Stunned || FriendManager.Emoting || _playerManager.BlockInput)
            return;

        Vector3 direction = new Vector3(dir.x, 0f, dir.y);
        FriendManager.MovementController.LockOnInDirection(direction);
    }
    #endregion

    #region AbilityTriggerAndUnlocks

    public void LockAbilitiesForLink(bool locked)
    {
        _utilityLocked = locked;
        _ultimateLocked = locked;

        GameManager.Instance.UIManager.SetActiveAbilityBar(2, !locked);
        GameManager.Instance.UIManager.SetActiveAbilityBar(3, !locked);
    }

    private void UseAbility(AbilityModule ability, ref float abilityLastUse, bool pressed)
    {
        if (Locked || _abilitiesLocked || FriendManager.Stunned || FriendManager.Emoting || _playerManager.IsDead.Value || _playerManager.BlockInput)
            return;

        if (_lockingAbility.HasValue && !_lockingAbility.Equals(ability))
            return;

        if ((ability.AbilitySlot == AbilitySlot.Secondary && _secondaryLocked) || (ability.AbilitySlot == AbilitySlot.Utility && _utilityLocked) || (ability.AbilitySlot == AbilitySlot.Ultimate && _ultimateLocked))
            return;

        float speedModifier = 1;

        if (_primaryAbilites.ContainsValue(ability))
        {
            speedModifier = _playerManager.Stats.GetStat(Stat.AttackSpeed);
        }

        if (Time.time < abilityLastUse + ability.Cooldown * speedModifier)
            return;

        _pressed = pressed; 
        Type thisType = this.GetType();
        MethodInfo theMethod = thisType.GetMethod(ability.Method, BindingFlags.NonPublic | BindingFlags.Instance);
        theMethod.Invoke(this, null);
        abilityLastUse = Time.time;

        if (ability.CastTime > 0f)
            LockFriendForDuration(ability.CastTime);

        if(!ability.MoveDuringAbility & ability.AbilityType == AbilityType.Charging)
            FriendManager.MovementController.Lock(pressed);
        else if (!ability.MoveDuringAbility && ability.CastTime > 0f)
            FriendManager.MovementController.Lock(true, ability.CastTime);

        if (ability.AbilityType == AbilityType.OnAndOff && !ability.CastWhileOn && !_lockingAbility.HasValue)
            _lockingAbility = ability;
        else if (_lockingAbility.HasValue && _lockingAbility.Equals(ability))
            _lockingAbility = null;
    }

    private void LockFriendForDuration(float duration)
    {
        _abilitiesLocked = true;
        Invoke(nameof(UnlockAbilities), duration);
    }

    private void UnlockAbilities()
    {
        _abilitiesLocked = false;
    }

    private void UpdateAbilityCooldowns()
    {
        float nextPrimaryTime = _primaryAbilityLastUse + _currentPrimaryAbility.Cooldown * _playerManager.Stats.GetStat(Stat.AttackSpeed);

        if (nextPrimaryTime >= Time.time - .01f)
            GameManager.Instance.UIManager.ChangeAbilityBarSize(0, GetCooldownValue(nextPrimaryTime, _currentPrimaryAbility.Cooldown * _playerManager.Stats.GetStat(Stat.AttackSpeed)));

        float nextSecondaryTime = _secondaryAbilityLastUse + _currentSecondaryAbility.Cooldown;

        if (nextSecondaryTime >= Time.time - .01f)
            GameManager.Instance.UIManager.ChangeAbilityBarSize(1, GetCooldownValue(nextSecondaryTime, _currentSecondaryAbility.Cooldown));

        float nextUltimateTime = _ultimateAbilityLastUse + _currentUltimateAbility.Cooldown;

        if (nextUltimateTime >= Time.time - .01f)
            GameManager.Instance.UIManager.ChangeAbilityBarSize(3, GetCooldownValue(nextUltimateTime, _currentUltimateAbility.Cooldown));
    }

    private float GetCooldownValue(float nextAbilityUse, float abilityCooldown)
    {
        float timeLeft = nextAbilityUse - Time.time;
        float updatedValue = timeLeft / abilityCooldown;
        return Mathf.Clamp(1 - updatedValue, 0f, 1f);
    }

    public void UnlockNextAbility()
    {
        return;

        if (!_secondaryLocked)
        {
            _secondaryLocked = true;
            GameManager.Instance.UIManager.SetActiveAbilityBar(1, true);
        }
        else if (!_utilityLocked)
        {
            _utilityLocked = true;
            GameManager.Instance.UIManager.SetActiveAbilityBar(2, true);
        }
        else if (!_ultimateLocked)
        {
            GameManager.Instance.UIManager.SetActiveAbilityBar(3, true);
            _ultimateLocked = true;
        }
    }

    public void UtilityRanOut()
    {
        UseAbility(_currentUtilityAbility, ref _utilityAbilityLastUse, false);
    }

    public void UpgradeUtility(int amount)
    {
        FriendManager.AbilityController.UpgradeUtility(amount);
    }
    #endregion

    #region PrimaryAbilityMethods

    private void BulletAbility()
    {
        CheckIfLinked();
        FriendManager.AbilityController.BulletCommand(_primaryModifier);
    }

    private void SlashAbility()
    {
        CheckIfLinked();
        FriendManager.AbilityController.SlashCommand(_primaryModifier);
    }

    private void JabAbility()
    {
        CheckIfLinked();

        if(!_playerManager.Linked.Value)
            FriendManager.AbilityController.JabCommand(_primaryModifier, _pressed);
    }

    private void CheckIfLinked()
    {
        if (!_playerManager.Linked.Value)
            return;

        _playerManager.PlayerMovementController.SetRunning(false);
        _playerManager.PlayerAnimationController.Attack();
    }

    public void JabAnimationCallback()
    {
        if (!_playerManager.Linked.Value)
            return;

        FriendManager.AbilityController.JabCommand(_primaryModifier, _pressed, true);
    }

    #endregion

    #region SecondaryAbilityMethods

    private void GrenadeAbility()
    {
        FriendManager.AbilityController.GrenadeCommand(_secondaryModifier);
    }

    private void HookAbility()
    {
        FriendManager.AbilityController.HookCommand(_secondaryModifier);
    }

    private void FreezeRayAbility()
    {
        FriendManager.AbilityController.ShootFreezeRayCommand(_secondaryModifier);
    }

    #endregion

    #region UtilityAbilityMethods

    private void SwitchPlacesAbility()
    {
        FriendManager.AbilityController.SwitchPlacesCommand(_utilityModifier);
    }

    private void ShieldAbility()
    {
        FriendManager.AbilityController.ShieldCommand(_utilityModifier, _pressed);
    }

    private void BecomeInvisibleAbility()
    {
        FriendManager.AbilityController.BecomeInvisibleCommand(_utilityModifier);
    }

    #endregion

    #region UltimateAbilityMethods

    private void NukeAbility()
    {
        FriendManager.AbilityController.NukeCommand(_ultimateModifier);
    }

    private void HealPulseAbility()
    {
        FriendManager.AbilityController.HealPulseCommand(_ultimateModifier);
    }
    
    private void FrenzyAbility()
    {
        FriendManager.AbilityController.FrenzyCommand(_ultimateModifier);
    }

    #endregion
}

[System.Serializable]
public struct AbilityModule
{
    public string Method;
    public float Cooldown;
    public AbilityType AbilityType;
    public AbilitySlot AbilitySlot;
    public bool MoveDuringAbility;
    public float CastTime;
    public bool CastWhileOn;

    public AbilityModule(string method, float cooldown, AbilityType abilityType, AbilitySlot abilitySlot, bool moveDuringAbility, float castTime = 0f, bool castWhileOn = false)
    {
        Method = method;
        Cooldown = cooldown;
        AbilityType = abilityType;
        AbilitySlot = abilitySlot;
        MoveDuringAbility = moveDuringAbility;
        CastTime = castTime;
        CastWhileOn = castWhileOn;
    }
}

public enum AbilityType
{
    OneOff,
    OnAndOff,
    Repeating,
    Charging
}

public enum AbilitySlot
{
    Primary,
    Secondary,
    Utility,
    Ultimate
}
