using UnityEngine;

public class HealEntityEffect : DamageEffect
{
    [SerializeField]
    private bool _destroyOnHit;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        collider.GetComponent<Entity>().Heal(damage);
    }
}
