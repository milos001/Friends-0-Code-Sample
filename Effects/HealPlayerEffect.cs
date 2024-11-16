using UnityEngine;

public class HealPlayerEffect : DamageEffect
{
    [SerializeField]
    private bool _destroyOnHit;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        collider.GetComponent<PlayerManager>().Heal(damage);

        if (_destroyOnHit)
            Destroy(gameObject);
    }
}
