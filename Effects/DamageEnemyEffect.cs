using UnityEngine;

public class DamageEnemyEffect : DamageEffect
{
    public bool IsDeflected;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        collider.GetComponent<Enemy>().TakeDamage(damage, entityInterface);

        if (IsDeflected)
        {
            CollisionTarget collisionTarget = new CollisionTarget(
                CollisionType.Tag,
                "Enemy",
                new Effect[] { this },
            true);

            GetComponent<Projectile>().RemoveLastCollisionTarget();
            Destroy(this);
        }
    }

    public override void PlayClientFX(Collider collider, string collisionIdentifier)
    {
        if (!HasClientFX)
            return;

        collider.GetComponent<Enemy>().PlayOnHitEffect(transform.position);
    }
}