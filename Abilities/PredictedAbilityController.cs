//using FishNet.Object;
//using FishNet.Object.Prediction;
//using FishNet.Transporting;
//using System;
//using System.Collections.Generic;
//using UnityEngine;

//public class PredictedAbilityController : AbilityController
//{
//    private Enemy _nearestEnemy;

//    private Vector3 _initialDirection, _startingPos, _mousePos;
//    private float _initialForce;

//    private bool _setInitialVelocity, _disabled;

//    public override void OnStartNetwork()
//    {
//        base.OnStartNetwork();
//        if (TimeManager != null)
//        {
//            TimeManager.OnTick += TimeManager_OnTick;
//            TimeManager.OnPostTick += TimeManager_OnPostTick;
//        }

//        if (!Owner.IsLocalClient)
//            return;

//        Vector3 direction = Vector3.zero;

//        switch (_direction)
//        {
//            case Direction.Forward:
//                direction = transform.forward;
//                break;
//            case Direction.TowardsFriend:
//                break;
//            case Direction.AwayFromFriend:
//                break;
//            case Direction.TowardsNearestEnemy:
//                break;
//            case Direction.TowardsNearestPlayer:
//                Vector3 nearestPlayerPosition = GameManager.Instance.GetNearestPlayer(transform.position).transform.position;
//                direction = (nearestPlayerPosition - transform.position).normalized;
//                break;
//        }

//        _initialDirection = direction;
//        _initialForce = _projectileSpeed;

//        InvokeRepeating(nameof(CheckForDestroy), 1f, 1f);
//    }

//    public override void OnStopNetwork()
//    {
//        base.OnStopNetwork();
//        if (TimeManager != null)
//        {
//            TimeManager.OnTick -= TimeManager_OnTick;
//            TimeManager.OnPostTick -= TimeManager_OnPostTick;
//        }
//    }

//    private void TimeManager_OnTick()
//    {
//        ReplicateData moveData = BuildActions();
//        RunInputs(moveData);
//    }

//    private void TimeManager_OnPostTick()
//    {
//        CreateReconcile();
//    }

//    private ReplicateData BuildActions()
//    {
//        if (!IsOwner)
//            return default;

//        ReplicateData moveData = new ReplicateData();

//        return moveData;
//    }

//    [Replicate]
//    private void RunInputs(ReplicateData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
//    {

//    }

//    public override void CreateReconcile()
//    {
//        base.CreateReconcile();

//        Reconcile(new ReconcileData(_predictionRigidbody));
//    }

//    [Reconcile]
//    private void Reconcile(ReconcileData recData, Channel channel = Channel.Unreliable)
//    {
//    }

//    struct ReplicateData : IReplicateData
//    {
//        private uint _tick;
//        public void Dispose() { }
//        public uint GetTick() => _tick;
//        public void SetTick(uint value) => _tick = value;
//    }


//    struct ReconcileData : IReconcileData
//    {
//        public PredictionRigidbody PredictionRigidbody;
//        public ReconcileData(PredictionRigidbody pr) : this()
//        {
//        }

//        private uint _tick;
//        public void Dispose() { }
//        public uint GetTick() => _tick;
//        public void SetTick(uint value) => _tick = value;
//    }
//}
//}
