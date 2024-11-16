using UnityEngine;

public class FreezeEnemyEffect : Effect
{
    [SerializeField]
    private float _freezeDuration;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        collider.GetComponent<Enemy>().Stun(_freezeDuration, true);
    }
}
