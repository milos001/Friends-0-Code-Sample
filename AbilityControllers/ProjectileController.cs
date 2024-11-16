using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileController : AbilityController
{
    [Header("Projectile Parameters")]
    [SerializeField]
    private Direction _direction;
    [SerializeField]
    private float _projectileSpeed;
    [SerializeField]
    private Rigidbody _projectileRb;
    [SerializeField]
    private GameObject _projectileEffect;
    [Header("Homing Parameters")]
    [SerializeField]
    private bool _homeOnEnemy;
    [SerializeField]
    private float _homingSpeed;
    [SerializeField]
    private bool _stopAtMousePos;
    [Header("Explosion Parameters")]
    [SerializeField]
    private ExplosionController _explosionController;
    [SerializeField]
    private float _explodeAfterDistance = 10f;

    private PredictionRigidbody _predictionRigidbody;
    private Enemy _nearestEnemy;

    private Vector3 _initialDirection, _startingPos, _mousePos;
    private float _initialForce;

    private bool _setInitialVelocity, _disabled;

    private void Awake()
    {
        _predictionRigidbody = new PredictionRigidbody();
        _predictionRigidbody.Initialize(_projectileRb);

        _startingPos = transform.position;
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 100f, 1 << LayerMask.NameToLayer("Ground"), QueryTriggerInteraction.Ignore))
        {
            _mousePos = hit.point;
            _mousePos.y = 1.5f;
        }
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (TimeManager != null)
        {
            TimeManager.OnTick += TimeManager_OnTick;
            TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        if (!Owner.IsLocalClient)
            return;

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
                break;
            case Direction.TowardsNearestPlayer:
                Vector3 nearestPlayerPosition = GameManager.Instance.GetNearestPlayer(transform.position).transform.position;
                direction = (nearestPlayerPosition - transform.position).normalized;
                break;
        }

        _initialDirection = direction;
        _initialForce = _projectileSpeed;

        InvokeRepeating(nameof(CheckForDestroy), 1f, 1f);
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        if (TimeManager != null)
        {
            TimeManager.OnTick -= TimeManager_OnTick;
            TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }
    }
    
    private void TimeManager_OnTick()
    {
        ReplicateData moveData = BuildActions();
        RunInputs(moveData);

        if(!_disabled && _explosionController != null && Vector3.Distance(_startingPos, transform.position) > _explodeAfterDistance)
            Stop(true, true);

        if (!_disabled && _stopAtMousePos && Vector3.Distance(_mousePos, transform.position) < 1f)
            Stop(false);
    }

    private void TimeManager_OnPostTick()
    {
        CreateReconcile();
    }

    private ReplicateData BuildActions()
    {
        if (!IsOwner)
            return default;

        ReplicateData moveData = new ReplicateData();

        if(!_setInitialVelocity)
        {
            moveData.Force = _initialForce;
            moveData.Direction = _initialDirection;
            moveData.SetInitialForce = true;
            _setInitialVelocity = true;
            return moveData;
        }

        GetNearestEnemy();
        if (_disabled)
        {
            moveData.Stopped = _disabled;
            return moveData;
        }

        if (!_homeOnEnemy || _nearestEnemy == null || _nearestEnemy.Dead)   
            return default;

        moveData.Direction = _nearestEnemy.transform.position - transform.position;
        moveData.Force = _homingSpeed;
        moveData.Homing = true;

        return moveData;
    }

    [Replicate]
    private void RunInputs(ReplicateData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        if (md.Stopped)
        {
            _predictionRigidbody.Velocity(Vector3.zero);
            return;
        }

        if (md.SetInitialForce)
            _predictionRigidbody.Velocity(md.Direction * md.Force);

        if(md.Homing)
            _predictionRigidbody.AddForce(md.Direction * md.Force);

        transform.forward = md.Direction;

        _predictionRigidbody.Simulate();
    }

    public override void CreateReconcile()
    {
        base.CreateReconcile();

        Reconcile(new ReconcileData(_predictionRigidbody));
    }

    [Reconcile]
    private void Reconcile(ReconcileData recData, Channel channel = Channel.Unreliable)
    {
        _predictionRigidbody.Reconcile(recData.PredictionRigidbody);
    }

    private void CheckForDestroy()
    {
        if (Vector3.Distance(transform.position, Vector3.zero) > 200f)
            Destroy(gameObject);
    }

    protected override void OnTriggerEnter(Collider other)
    {
        if (_disabled)
            return;

        base.OnTriggerEnter(other);

        if (hitATarget && _explosionController != null)
            Stop(true, true);

        hitATarget = false;
    }

    private void Stop(bool disableEffect, bool explode = false)
    {
        if (_explosionController != null)
        {
            if (explode)
                _explosionController.Initialize();
            else
                _explosionController.ExpandCollider();
        }

        if(disableEffect)
            _projectileEffect.SetActive(false);


        _disabled = true;
    }

    public void Deflect()
    {
        _predictionRigidbody.Velocity(-_predictionRigidbody.Rigidbody.velocity);
    }

    private void GetNearestEnemy()
    {
        Enemy nearestEnemy = GameManager.Instance.EnemyManager.GetNearestEnemy(transform.position);
        if (nearestEnemy != null)
            _nearestEnemy = nearestEnemy;
    }

    struct ReplicateData : IReplicateData
    {
        public Vector3 Direction;
        public float Force;
        public bool Stopped;
        public bool Homing;
        public bool SetInitialForce;

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }


    struct ReconcileData : IReconcileData
    {
        public PredictionRigidbody PredictionRigidbody;
        public ReconcileData(PredictionRigidbody pr) : this()
        {
            PredictionRigidbody = pr;
        }

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
}