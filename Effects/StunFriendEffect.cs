using UnityEngine;

public class StunFriendEffect : Effect
{
    [SerializeField]
    private float _stunDuration = 1;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        collider.GetComponent<FriendManager>().Stun(_stunDuration);
    }
}
