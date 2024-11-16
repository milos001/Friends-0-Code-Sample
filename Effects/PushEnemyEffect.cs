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
        Vector3 pushDir = Vector3.zero;
        Vector3 startPos = Vector3.zero;
        switch (_pushDir)
        {
            case Direction.TowardsFriend:
                startPos = GameManager.Instance.Players[Owner].PlayerFriendController.FriendManager.transform.position;
                pushDir = (startPos - collider.transform.position).normalized;
                break;
            case Direction.AwayFromFriend:
                startPos = GameManager.Instance.Players[Owner].PlayerFriendController.FriendManager.transform.position;
                pushDir = (collider.transform.position - startPos).normalized;
                break;
            case Direction.TowardsNearestEnemy:
                startPos = GameManager.Instance.EnemyManager.GetNearestEnemy(transform.position, collider.GetComponents<Enemy>()).transform.position;
                pushDir = (startPos - collider.transform.position).normalized;
                break;

        }
        collider.GetComponent<Enemy>().Push(pushDir, _force);
    }
}
