using FischlWorks_FogWar;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.InputSystem.InputAction;

public class PlayerManager : Entity
{
    [Header("Player Parameters")]
    public PlayerMovementController PlayerMovementController;
    public PlayerAnimationController PlayerAnimationController;
    public PlayerFriendController PlayerFriendController;
    public Transform GraphicTs, ProjectileSpawnPoint, PunchSpawnPoint;
    public bool BlockInput;
    public bool Rooted;

    public readonly SyncVar<bool> Linked = new SyncVar<bool>();
    public readonly SyncVar<int> Money = new SyncVar<int>(0);
    public bool Invisible;

    [HideInInspector]
    public PlayerInputActions InputActions;
    [HideInInspector]
    public int BorrowedAmount;
    [SerializeField]
    private AudioClip[] _grunts;
    [SerializeField]
    private AudioSource _audioSource;
    [SerializeField]
    private Transform[] _friendHolderPositions;
    [SerializeField]
    private SkinnedMeshRenderer _meshRenderer;
    [SerializeField]
    private Rigidbody _rigidbody;
    [SerializeField]
    private Collider _collider;
    [SerializeField]
    private Material _defaultMaterial, _invisMaterial, _linkedMaterial;
    [SerializeField]
    private GameObject _healBuff, _invincibilityShield;
    [SerializeField]
    private Image _quickTimeImage;
    [SerializeField]
    private PlayerUIController _uiController;

    private FriendManager _friendManager;
    private PredictionRigidbody _predictionRigidbody;
    private ShopController _currentShop;
    private List<EnemyHarvestField> _harvestFields;
    private EnemyHarvestField _currentHarvestField;

    private int _burnCounter;
    private bool _harvesting;
    private float _harvestRate = .15f, _lastHarvestTime;

    private TrapController _trap;
    private Vector2 _nextQuickTimePress;
    private int _pressesLeft;
    private int _lastDir;

    private bool _visible;
    private bool _interacting;

    private float _timeAtStartScene;

    protected override void Awake()
    {
        base.Awake();

        _predictionRigidbody = new PredictionRigidbody();
        _predictionRigidbody.Initialize(_rigidbody);
        PlayerMovementController.SetPredictionRigidbody(_predictionRigidbody);

        _timeAtStartScene = Time.time;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (!Owner.IsLocalClient)
        {
            Destroy(PlayerAnimationController);
            Destroy(GetComponent<AudioListener>());
        }

        Linked.OnChange += OnLinkedValueChange;
    }

    public void Initialize(FriendManager friendManager)
    {
        _harvestFields = new List<EnemyHarvestField>();

        _friendManager = friendManager;
        
        InputActions = new PlayerInputActions();
        InputActions.Enable();

        name = CrossSceneValues.Instance.PlayerName;

        PlayerMovementController.Initialize();
        PlayerAnimationController.Initialize(this, Stats.GetStat(Stat.MoveSpeed));
        _uiController.Initialize(Camera.main, CrossSceneValues.Instance.PlayerName, CrossSceneValues.Instance.PlayerColor.ToColor());
        GameManager.Instance.UIManager.SetShieldAmount(Stats.GetStat(Stat.Health), Stats.GetStat(Stat.MaxHealth));
        GameManager.Instance.UIManager.ChangeMoneyAmount(Money.Value, BorrowedAmount);

        friendManager.Init(this);
        PlayerFriendController.Init(this, friendManager);
        RpcServerInitialize(friendManager.ObjectId, CrossSceneValues.Instance.PlayerColor);

        GameObject.Find("FogWar").GetComponent<csFogWar>().AddFogRevealer(new csFogWar.FogRevealer(transform, 35, true));

        InputActions.Player.Interact.started += Interact;
        InputActions.Player.Interact.canceled += CancelInteract;
        Money.OnChange += OnMoneyAmountChanged;
    }

    [ServerRpc]
    private void RpcServerInitialize(int friendId, string color)
    {
        RpcInitializeForObservers(friendId, color);
    }

    [ObserversRpc]
    private void RpcInitializeForObservers(int friendId, string color)
    {
        _friendManager = ClientManager.Objects.Spawned[friendId].GetComponent<FriendManager>();

        Material linkedMat = new Material(_linkedMaterial);
        linkedMat.color = color.ToColor();
        _linkedMaterial = linkedMat;
    }

    private void Update()
    {
        if (IsOwner)
        {
            if (_harvesting && _lastHarvestTime + _harvestRate < Time.time)
                TryHarvestEnemy(_currentHarvestField);

            return;
        }

        bool visible = csFogWar.Instance.CheckVisibility(transform.position);

        if (_visible != visible)
        {
            GraphicTs.gameObject.SetActive(visible);
            _visible = visible;
        }
    }

    private void Interact(CallbackContext obj)
    {
        List<EnemyHarvestField> fieldsToRemove = new List<EnemyHarvestField>();
        foreach (var field in _harvestFields)
        {
            if (field == null)
                fieldsToRemove.Add(field);
        }

        _harvestFields = _harvestFields.Except(fieldsToRemove).ToList();

        if(_harvestFields.Count > 0)
        {
            BlockInput = true;
            PlayerMovementController.SetRunning(false);
            PlayerAnimationController.SetCrouching(true);
            _currentHarvestField = GeneralUtility.GetNearestTransformInArray(transform.position, _harvestFields.Select(x => x.transform).ToArray()).GetComponent<EnemyHarvestField>();
            int currentCredits = _currentHarvestField.GetCurrentCredits();
            int totalCredits = _currentHarvestField.GetTotalCredits();
            _harvesting = true;

            //float startPoint = (totalCredits - currentCredits) / (float)totalCredits;
            //StartCoroutine(_uiController.FillBarInTime(totalCredits * _harvestRate, startPoint));
            //_uiController.CancelFill();

            return;
        }

        if (_currentShop != null)
            TryBuyItem(_currentShop.ThisShopType);
    }

    private void CancelInteract(CallbackContext obj)
    {
        if (!_harvesting)
            return;

        BlockInput = false;
        PlayerAnimationController.SetCrouching(false);
        _harvesting = false;
    }

    private void TryHarvestEnemy(EnemyHarvestField field)
    {
        if (_currentHarvestField == null)
        {
            _harvesting = false;
            _harvestFields.Remove(_currentHarvestField);
            _currentHarvestField = null;
            BlockInput = false;
            PlayerAnimationController.SetCrouching(false);
            return;
        }

        field.RpcServerHarvestCredit(OwnerId);
        _lastHarvestTime = Time.time;
    }
    

    [ServerRpc]
    private void TryBuyItem(ShopController.ShopType shopType)
    {
        int cost = GameManager.Instance.UIManager.ShopManager.GetShopPrice(OwnerId, shopType);

        if (Money.Value >= cost)
            BuyItem(cost, shopType);
        else
            GameManager.Instance.UIManager.ShopManager.OnShopInteract(Owner, shopType, false);
    }

    private void BuyItem(int cost, ShopController.ShopType itemType)
    {
        Money.Value -= cost;
        GameManager.Instance.UIManager.ChangeMoneyAmount(Money.Value, BorrowedAmount);
        GameManager.Instance.UIManager.ShopManager.OnShopInteract(Owner, itemType, true);

        switch (itemType)
        {
            case ShopController.ShopType.MaxShield:
                Stats.AddToBaseStat(Stat.MaxHealth, 20);
                Stats.AddToBaseStat(Stat.Health, 20);
                GameManager.Instance.UIManager.SetShieldAmount(Stats.GetStat(Stat.Health), Stats.GetStat(Stat.MaxHealth));
                break;
            case ShopController.ShopType.Power:
                Stats.AddToBaseStat(Stat.Damage, 10);
                break;
            case ShopController.ShopType.MoveSpeed:
                Stats.AddToBaseStat(Stat.MoveSpeed, 15);
                break;
            case ShopController.ShopType.AttackSpeed:
                Stats.AddToBaseStat(Stat.AttackSpeed, -.1f);
                break;
            case ShopController.ShopType.ShieldRecharge:
                SetRegenRate(GetRegenRate() / 1.25f);
                break;
            case ShopController.ShopType.Credits:
                BorrowMoney();
                break;
            case ShopController.ShopType.Utility:
                PlayerFriendController.UpgradeUtility(50);
                break;
            case ShopController.ShopType.Area:
                Stats.AddToBaseStat(Stat.RangeModifier, 25);
                break;
        }

        OnBuyItemRpc(Owner, itemType);
    }

    [TargetRpc]
    private void OnBuyItemRpc(NetworkConnection connection, ShopController.ShopType itemType)
    {
        switch (itemType)
        {
            case ShopController.ShopType.MoveSpeed:
                PlayerAnimationController.SetAnimationSpeed(Stats.GetStat(Stat.MoveSpeed));
                break;
            default:
                break;
        }
    }

    public void Revive()
    {
        IsDead.Value = false;
        ReviveRpc(Owner);
        _collider.enabled = true;
    }

    [TargetRpc]
    private void ReviveRpc(NetworkConnection connection)
    {
        PlayerAnimationController.SetDead(false);
        BlockInput = false;
        _collider.enabled = true;
    }

    public override void TakeDamage(float damage, Entity entity , bool overwriteProtection = false)
    {
        base.TakeDamage(damage, entity, overwriteProtection);

        TakeDamageRpc(Owner, (int)damage);
    }

    [TargetRpc]
    private void TakeDamageRpc(NetworkConnection connection, int damage)
    {
        ActivateDamageEffect(damage);
    }

    protected override void Die(Entity entity)
    {
        base.Die(entity);

        GameManager.Instance.PlayerDied(OwnerId);
        DieRpc(Owner);
        _collider.enabled = false;
    }

    [TargetRpc]
    private void DieRpc(NetworkConnection connection)
    {
        PlayerAnimationController.SetDead(true);
        BlockInput = true;
        _collider.enabled = false;
    }

    private void ActivateDamageEffect(int damage)
    {
        if (Linked.Value)
        {
            Vector3 spawnPos = transform.position;
            spawnPos.y = .9f;
            Instantiate(_invincibilityShield, spawnPos, Quaternion.identity, transform);
        }

        GameManager.Instance.UIManager.SetShieldAmount(Stats.GetStat(Stat.Health), Stats.GetStat(Stat.MaxHealth), 1);
        GameManager.Instance.UIManager.ActivateDamageEffect();

        _audioSource.clip = _grunts[Random.Range(0, _grunts.Length)];
        GameManager.Instance.SoundManager.PlaySound(_audioSource);
        PlayerAnimationController.TakeHit();

    }

    public void Push(Vector3 direction, float force)
    {
        _predictionRigidbody.Velocity(direction * force);
    }

    public void BlockInputForDuration(float duration)
    {
        BlockInput = true;
        if (IsInvoking(nameof(ResetInput)))
            CancelInvoke(nameof(ResetInput));

        Invoke(nameof(ResetInput), duration);
    }

    private void ResetInput()
    {
        BlockInput = false;
        _predictionRigidbody.Velocity(Vector3.zero);
    }

    public override void Heal(int healAmount)
    {
        base.Heal(healAmount);

        HealRpc(Owner, healAmount);
    }

    [TargetRpc]
    private void HealRpc(NetworkConnection connection, int healAmount)
    {
        Instantiate(_healBuff, transform.position, _healBuff.transform.rotation);
        GameManager.Instance.UIManager.SetShieldAmount(Stats.GetStat(Stat.Health), Stats.GetStat(Stat.MaxHealth));
    }


    protected override void Regen()
    {
        base.Regen();

        RegeneRpc(Owner);
    }

    [TargetRpc]
    private void RegeneRpc(NetworkConnection connection)
    {
        GameManager.Instance.UIManager.SetShieldAmount(Stats.GetStat(Stat.Health), Stats.GetStat(Stat.MaxHealth));
    }

    public void SetTrapped(bool trapped, int presses, TrapController trap)
    {
        Rooted = trapped;
        _trap = trap;
        PlayerAnimationController.SetDirectionAndRunning(Vector3.zero, false);
        _quickTimeImage.gameObject.SetActive(true);
        Camera camera = Camera.main;
        transform.LookAt(transform.position + camera.transform.rotation * Vector3.back, camera.transform.rotation * Vector3.up);
        

        InputActions.Player.Movement.performed += ctx => QuickTimeCheck(ctx.ReadValue<Vector2>());
        InputActions.Enable();

        _pressesLeft = presses;
        int randomDir = Random.Range(0, 4);
        _lastDir = randomDir;
        switch (randomDir)
        {
            case 0:
                _nextQuickTimePress = Vector2.up;
                _quickTimeImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, 0f);
                break;
            case 1:
                _nextQuickTimePress = Vector2.down;
                _quickTimeImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
                break;
            case 2:
                _nextQuickTimePress = Vector2.left;
                _quickTimeImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
                break;
            case 3:
                _nextQuickTimePress = Vector2.right;
                _quickTimeImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
                break;
        }
    }

    private void QuickTimeCheck(Vector2 input)
    {
        if ((input - _nextQuickTimePress).magnitude > .1f)
            return;

        _pressesLeft--;

        if(_pressesLeft == 0)
        {
            Rooted = false;
            _quickTimeImage.gameObject.SetActive(false);
            InputActions.Disable();
            _trap.Release();
            return;
        }

        int randomDir;
        do
        {
            randomDir = Random.Range(0, 4);
            switch (randomDir)
            {
                case 0:
                    _nextQuickTimePress = Vector2.up;
                    _quickTimeImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, 0f);
                    break;
                case 1:
                    _nextQuickTimePress = Vector2.down;
                    _quickTimeImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
                    break;
                case 2:
                    _nextQuickTimePress = Vector2.left;
                    _quickTimeImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
                    break;
                case 3:
                    _nextQuickTimePress = Vector2.right;
                    _quickTimeImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
                    break;
            }
            
        } while(randomDir == _lastDir);

        _lastDir = randomDir;
    }

    public void SetInvisible(bool invisible, float duration = 0f)
    {
        if (invisible)
        {
            _meshRenderer.material = _invisMaterial;
            foreach (var enemy in GameManager.Instance.EnemyManager.Enemies.Value)
            {
                enemy.ResetVision();
            }
        }
        else
            _meshRenderer.material = _defaultMaterial;

        Invisible = invisible;

        if(duration > 0f && invisible)
        {
            if (IsInvoking(nameof(ResetInvisibility)))
                CancelInvoke(nameof(ResetInvisibility));

            Invoke(nameof(ResetInvisibility), duration);
        }
    }

    private void ResetInvisibility()
    {
        SetInvisible(false);
    }

    public void SetLinked(bool linked)
    {
        if (linked == Linked.Value)
            return;

        UpdateLinkedVisuals(linked);
        RpcSetLinked(linked);
        PlayerFriendController.LockAbilitiesForLink(linked);
    }

    private void UpdateLinkedVisuals(bool linked)
    {
        if (linked)
        {
            List<Material> materials = _meshRenderer.materials.ToList();
            materials.Add(_linkedMaterial);
            _meshRenderer.materials = materials.ToArray();
        }
        else
        {
            List<Material> materials = _meshRenderer.materials.ToList();
            materials.RemoveAt(1);
            _meshRenderer.materials = materials.ToArray();
        }
    }

    [ServerRpc]
    private void RpcSetLinked(bool linked)
    {
        Linked.Value = linked;

        SetRegenerating(linked);

        if (linked)
            Stats.AddBuff(Stat.AttackSpeed, -.2f);
        else
            Stats.AddBuff(Stat.AttackSpeed, .2f);
    }

    private void OnLinkedValueChange(bool prev, bool next, bool asServer)
    {
        if (asServer || IsOwner)
            return;

        UpdateLinkedVisuals(next);
        _friendManager.EnableDisableRenderers(!next);
    }

    public void PickupMoney(int moneyAmount)
    {
        if(BorrowedAmount > 0)
            BorrowedAmount -= moneyAmount;
        else
            Money.Value += moneyAmount;
    }

    private void OnMoneyAmountChanged(int prev, int next, bool asSever)
    {
        GameManager.Instance.UIManager.ChangeMoneyAmount(next, BorrowedAmount);
    }

    public void SetCurrentShop(ShopController shop)
    {
        _currentShop = shop;
    }

    public void TryAddHarvestField(EnemyHarvestField harvestField)
    {
        if (!IsOwner)
            return;

        if (!_harvestFields.Contains(harvestField))
            _harvestFields.Add(harvestField);
    }

    public void TryRemoveHarvestField(EnemyHarvestField harvestField)
    {
        if (!IsOwner)
            return;

        if (_harvestFields.Contains(harvestField))
            _harvestFields.Remove(harvestField);

        if (_currentHarvestField == harvestField)
            _currentHarvestField = null;
    }

    public Transform[] GetFriendHolderPositions()
    {
        return _friendHolderPositions;
    }

    public Vector3 GetFriendPosition()
    {
        return PlayerFriendController.FriendManager.transform.position;
    }

    private void BorrowMoney()
    {
        BorrowedAmount = GameManager.Instance.CurrentWave.Value * 10;
        Money.Value += BorrowedAmount;
        GameManager.Instance.UIManager.ChangeMoneyAmount(Money.Value, BorrowedAmount);
    }


    private void OnDisable()
    {
        if (InputActions == null)
            return;

        InputActions.Disable();
    }
}
