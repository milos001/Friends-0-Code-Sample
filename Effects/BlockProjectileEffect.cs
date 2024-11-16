using UnityEngine;

public class BlockProjectileEffect : Effect
{
    [SerializeField]
    private Animator _animator;
    [SerializeField]
    private string _animationName;
    [SerializeField]
    private bool _deflectProjectileBack;
    [SerializeField]
    private int _damageMultiplier = 10;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        if (_animator != null)
            _animator.Play(_animationName);

        if(!_deflectProjectileBack)
        {
            Destroy(collider.gameObject);
            return;
        }

        ProjectileController projectile = collider.GetComponent<ProjectileController>();
        projectile.Deflect();

        if(collider.TryGetComponent(out DamagePlayerEffect damagePlayerEffect))
        {
            DamageEnemyEffect damageEnemyEffect = collider.gameObject.AddComponent<DamageEnemyEffect>();
            damageEnemyEffect.Initialize(damagePlayerEffect.GetDamageNumber() * _damageMultiplier, Owner);

            CollisionTarget collisionTarget = new CollisionTarget(
                CollisionType.Tag,
                "Enemy",
                new Effect[] {damageEnemyEffect},
                true);

            projectile.AddCollisionTarget(collisionTarget);
        }
    }
}