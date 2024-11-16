using UnityEngine;

public class SlowEnemyEffect : Effect
{
    [SerializeField]
    private float _slowModifier, _slowDuration;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        collider.GetComponent<Enemy>().Slow(_slowModifier, _slowDuration);
    }
}
