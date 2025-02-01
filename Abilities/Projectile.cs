using FishNet.Component.Prediction;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent (typeof(OfflineRigidbody))]
public class Projectile : Ability
{
    [Header("Initialize Callback Parameters")]
    [SerializeField]
    private InitializeCallbackData[] _initializeCallbacks;
    [SerializeField]
    private DestinationType _destinationType;
    [Header("Projectile Parameters")]
    [SerializeField]
    private Direction _direction;
    [SerializeField]
    private float _projectileSpeed, _maxDistance;
    [SerializeField]
    private Rigidbody _projectileRb;
    [SerializeField]
    private GameObject _projectileEffect;
    [Header("Homing Parameters")]
    [SerializeField]
    private bool _homeOnEnemy;
    [SerializeField]
    private float _homingSpeed;

    private Enemy _nearestEnemy;
    private List<Enemy> _enemiesToIgnore = new List<Enemy>();

    private Vector3 _initialDirection, _startingPos, _destinationPos;

    private bool _initializeAfterDistance, _stopAtDestination;
    private float _distanceToTravel;
    private bool _disabled;

    public override void Initialize(AbilityController abilityController, float lateBy)
    {
        base.Initialize(abilityController, lateBy);

        SetupInitializeCallbacks();

        Vector3 direction = Vector3.zero;
        
        switch (_direction)
        {
            case Direction.Forward:
                direction = transform.forward;
                break;
            case Direction.TowardsFriend:
                break;
            case Direction.AwayFromFriend:
                break;
            case Direction.TowardsNearestEnemy:
                GetNearestEnemy();

                if (_nearestEnemy == null || Vector3.Distance(transform.position, _nearestEnemy.transform.position) > 25f)
                    return;

                Vector3 enemyPos = _nearestEnemy.transform.position;
                enemyPos.y = transform.position.y;
                direction = (enemyPos - collider.transform.position).normalized;
                break;
            case Direction.TowardsNearestPlayer:
                Vector3 nearestPlayerPosition = GameManager.Instance.GetNearestPlayer(transform.position).transform.position;
                nearestPlayerPosition.y = transform.position.y;
                direction = (nearestPlayerPosition - transform.position).normalized;
                break;
        }

        if (_maxDistance > 0f)
            _startingPos = transform.position;

        float catchupSpeed = 1f;
        
        if (lateBy > 0f)
        {
            catchupSpeed = 1.2f;
        
            float initialGap = _projectileSpeed * lateBy;
            float relativeSpeed = _projectileSpeed * catchupSpeed - _projectileSpeed;
            float catchUpTime = initialGap / relativeSpeed;
        
            Invoke(nameof(ResetVelocity), catchUpTime);
        }
        
        _projectileRb.linearVelocity = direction * _projectileSpeed * catchupSpeed;
        
        if(_homeOnEnemy)
            GetNearestEnemy();

        InvokeRepeating(nameof(CheckForDestroy), 1f, 1f);
    }

    private void SetupInitializeCallbacks()
    {
        if (_initializeCallbacks != null && _initializeCallbacks.Length != 0)
        {
            _initializeAfterDistance = InitializeCallback != null &&
                _initializeCallbacks.Any(x => x.Type == InitializeCallbackType.AfterDistanceTravelled);

            if (_initializeAfterDistance)
            {
                InitializeCallbackData afterDistanceData = _initializeCallbacks.First(x => x.Type == InitializeCallbackType.AfterDistanceTravelled);
                _distanceToTravel = afterDistanceData.Arg1;
                _startingPos = transform.position;
            }

            _stopAtDestination = InitializeCallback != null &&
                _initializeCallbacks.Any(x => x.Type == InitializeCallbackType.OnReachDestination);

            if (_stopAtDestination)
            {
                switch (_destinationType)
                {
                    case DestinationType.MousePosition:
                        if(Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 100f, 1 << LayerMask.NameToLayer("Ground"), QueryTriggerInteraction.Ignore))
                            _destinationPos = hit.point;
                        break;
                    case DestinationType.TargetPosition:
                            _destinationPos = controller.GetTarget().Transform.position;
                        break;
                    default:
                        break;
                }
                _destinationPos.y = 1.5f;
            }
        }
    }

    private void CheckForDestroy()
    {
        if (Vector3.Distance(Vector3.zero, transform.position) > 155f)
            ReleaseObject();
    }

    protected override void OnObjectReleased()
    {
        _projectileRb.linearVelocity = Vector3.zero;
        _disabled = false;
        if (_projectileEffect != null)
            _projectileEffect.SetActive(true);

        base.OnObjectReleased();
    }

    private void ResetVelocity()
    {
        if (_disabled || _homeOnEnemy)
            return;

        _projectileRb.linearVelocity = transform.forward * _projectileSpeed;
    }

    protected override void OnTriggerEnter(Collider other)
    {
        if (_disabled)
            return;

        base.OnTriggerEnter(other);
    }

    protected override void TargetHitObserver(Collider collider, CollisionTarget target)
    {
        base.TargetHitObserver(collider, target);

        CheckForInitializeCallback(InitializeCallbackType.OnHit);
    }

    protected override void Update()
    {
        base.Update();

        if(_disabled && _projectileRb.linearVelocity.magnitude > 0f)
            _projectileRb.linearVelocity = Vector3.zero;

        if (_disabled)
            return;

        if(_maxDistance > 0f && Vector3.Distance(_startingPos, transform.position) > _maxDistance)
            ReleaseObject();

        if (_initializeAfterDistance && Vector3.Distance(_startingPos, transform.position) > _distanceToTravel)
            CheckForInitializeCallback(InitializeCallbackType.AfterDistanceTravelled);

        if (_stopAtDestination && Vector3.Distance(_destinationPos, transform.position) < 1f)
            CheckForInitializeCallback(InitializeCallbackType.OnReachDestination);
    }

    private void FixedUpdate()
    {
        if (!_homeOnEnemy || _nearestEnemy == null)
            return;

        Vector3 direction = _nearestEnemy.transform.position - transform.position;
        direction.y = 0f;
        direction.Normalize();
        _projectileRb.linearVelocity += direction * _homingSpeed;
    }

    private void Stop()
    {
        _projectileRb.linearVelocity = Vector3.zero;
        transform.localEulerAngles = Vector3.zero;
        if (_projectileEffect != null)
            _projectileEffect.SetActive(false);

        _disabled = true;
    }

    private void DisableProjectileEffect()
    {
        _projectileEffect.SetActive(false);
    }

    public void Deflect()
    {
        _projectileRb.linearVelocity = -_projectileRb.linearVelocity;
    }

    public void AddEnemyToIgnore(Enemy enemy)
    {
        if (enemy == null)
            return;

        _enemiesToIgnore.Add(enemy);
    }

    private void GetNearestEnemy()
    {
        Enemy nearestEnemy = GameManager.Instance.EnemyManager.GetNearestEnemy(transform.position, _enemiesToIgnore.ToArray());
        if (nearestEnemy != null)
            _nearestEnemy = nearestEnemy;
    }

    private void CheckForInitializeCallback(InitializeCallbackType type)
    {
        if (InitializeCallback != null )
        {
            InitializeCallbackData[] data = _initializeCallbacks.Where(x => x.Type == type).ToArray();
            if (data.Length <= 0)
                return;

            InitializeCallback.Invoke();

            if (data[0].Stop)
                Stop();
        }
    }

    [System.Serializable]
    private struct InitializeCallbackData
    {
        public InitializeCallbackType Type;
        public bool Stop;
        public bool DisableProjectileEffect;
        public float Arg1;
    }

    private enum InitializeCallbackType
    {
        AfterDistanceTravelled,
        OnHit,
        OnReachDestination
    }
}