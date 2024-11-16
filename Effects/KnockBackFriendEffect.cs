using UnityEngine;

public class KnockBackFriendEffect : Effect
{
    [SerializeField]
    private float _knockBackForce = 10f;
    [SerializeField]
    private float _knockBackDuration = .25f;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        FriendManager friend = GameManager.Instance.Players[Owner].PlayerFriendController.FriendManager;
        friend.MovementController.Push((friend.transform.position - collider.transform.position).normalized, _knockBackForce, _knockBackDuration);
    }
}
