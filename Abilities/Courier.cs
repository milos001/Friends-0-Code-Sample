using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Courier : Ability
{
    [Header("Initialize Callback Parameters")]
    [SerializeField]
    private InitializeCallbackData[] _initializeCallbacks;
    [SerializeField]
    private NavMeshAgent _navAgent;
    [SerializeField]
    private GameObject _courierEffect;
    [SerializeField]
    private DestinationType _destinationType;
    [SerializeField]
    private float _moveSpeed = 5f, _stopDistance;

    private Entity _spawnerEntity;
    private Vector3 _targetPosition;
    private TargetData _targetData;

    private bool _stopped;

    public override void Initialize(AbilityController abilityController, float lateBy)
    {
        base.Initialize(abilityController, lateBy);

        _spawnerEntity = abilityController.GetInterface();

        switch (_destinationType)
        {
            case DestinationType.MousePosition:
                break;
            case DestinationType.TargetPosition:
                _targetData = controller.GetTarget();
                break;
            case DestinationType.NearestEnemy:
                SetNearestEnemy();
                break;
            case DestinationType.NearestPlayer:
                SetNearestPlayer();
                break;
            default:
                break;
        }

        if (_courierEffect != null)
            _courierEffect.SetActive(true);
        _stopped = false;

        InvokeRepeating(nameof(UpdateTargetPosition), 0f, .1f);
        controller.TargetUpdated += UpdateTarget;
    }

    private void UpdateTarget(TargetData target)
    {
        _targetData = target;
    }

    private void UpdateTargetPosition()
    {
        _navAgent.isStopped = _stopped;
        if(_stopped || _targetData.Transform == null)
            return;

        Vector3 destination = _targetData.Transform.position;
        destination.y = transform.position.y;

        bool reachedTarget = Vector3.Distance(destination, transform.position) < _stopDistance;

        if(reachedTarget)
        {
            if (_targetData.Entity != null)
                CheckForInitializeCallback(InitializeCallbackType.OnReachEntity);
            else
                CheckForInitializeCallback(InitializeCallbackType.OnReachTarget);

            return;
        }

        _navAgent.destination = destination;
    }

    protected override void OnObjectReleased()
    {
        CancelInvoke();

        base.OnObjectReleased();
    }

    private void SetNearestEnemy()
    {
        Enemy[] enemiesToExclude = null;

        if (!_spawnerEntity.IsPlayer)
            enemiesToExclude = new Enemy[] { _spawnerEntity as Enemy };

        Enemy nearestEnemy = GameManager.Instance.EnemyManager.GetNearestEnemy(transform.position, enemiesToExclude);
        _targetData = new TargetData(nearestEnemy.transform, nearestEnemy);
    }

    private void SetNearestPlayer()
    {
        PlayerManager[] playersToExclude = null;

        if (_spawnerEntity.IsPlayer)
            playersToExclude = new PlayerManager[] { _spawnerEntity as PlayerManager };

        PlayerManager player = GameManager.Instance.GetNearestPlayer(transform.position, playersToExclude);
        _targetData = new TargetData(player.transform, player);
    }

    private void CheckForInitializeCallback(InitializeCallbackType type)
    {
        if (InitializeCallback != null)
        {
            InitializeCallbackData[] data = _initializeCallbacks.Where(x => x.Type == type).ToArray();
            if (data.Length <= 0)
                return;

            InitializeCallback.Invoke();

            if (data[0].Stop)
            {
                _stopped = true;
                if(_courierEffect != null)
                    _courierEffect.SetActive(false);
            }
        }
    }

    [System.Serializable]
    private struct InitializeCallbackData
    {
        public InitializeCallbackType Type;
        public bool Stop;
        public float Arg1;
    }

    private enum InitializeCallbackType
    {
        AfterDistanceTravelled,
        OnHit,
        OnReachEntity,
        OnReachTarget
    }
}
