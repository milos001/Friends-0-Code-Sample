using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PushEnemyEffect : Effect
{
    [SerializeField]
    private Direction _pushDir;

    [SerializeField]
    private float _force = 15f;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        Enemy enemy = collider.GetComponent<Enemy>();
        if (enemy.IsDead.Value)
            return;

        Vector3 direction = Vector3.zero;
        Vector3 startPos = Vector3.zero;

        PlayerManager playerManager = GameManager.Instance.Players[entityInterface.OwnerId];

        Direction pushDir = _pushDir;

        if(_pushDir == Direction.TowardsNearestEnemy && GameManager.Instance.EnemyManager.Enemies.Value.Count < 2)
            pushDir = Direction.TowardsFriend;

        switch (pushDir)
        {
            case Direction.TowardsFriend:
                startPos = playerManager.PlayerFriendController.FriendManager.transform.position;
                direction = (startPos - collider.transform.position).normalized;
                break;
            case Direction.AwayFromFriend:
                startPos = playerManager.PlayerFriendController.FriendManager.transform.position;
                direction = (collider.transform.position - startPos).normalized;
                break;
            case Direction.TowardsNearestEnemy:
                startPos = GameManager.Instance.EnemyManager.GetNearestEnemy(transform.position, collider.GetComponents<Enemy>()).transform.position;
                direction = (startPos - collider.transform.position).normalized;
                break;

        }
        enemy.Push(direction, _force);
    }
}
