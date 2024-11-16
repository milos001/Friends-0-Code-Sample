using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnProjectilesEffect : Effect
{
    [SerializeField]
    private ProjectileController _projectilePrefab;
    [SerializeField]
    private int _projectileCount;
    [SerializeField]
    private Direction _direction;
    [Header("Random Parameters")]
    [SerializeField]
    private bool _randomPositions;
    [SerializeField]
    private float _randomRadius;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        Vector3 facingDir = Vector3.zero;

        switch (_direction)
        {
            case Direction.TowardsNearestEnemy:
                Enemy nearestEnemy = GameManager.Instance.EnemyManager.GetNearestEnemy(transform.position, collider.GetComponents<Enemy>());

                if (nearestEnemy == null || Vector3.Distance(transform.position, nearestEnemy.transform.position) > 20f)
                    return;

                Vector3 enemyPos = nearestEnemy.transform.position;
                facingDir = (enemyPos - collider.transform.position).normalized;
                break;
            default:
                break;
        }

        for (int i = 0; i < _projectileCount; i++)
        {
            Vector3 spawnPos = transform.position;

            if(_randomPositions)
                spawnPos += new Vector3(Random.Range(-_randomRadius, _randomRadius), Random.Range(-_randomRadius, _randomRadius));

            ProjectileController projectile = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
            projectile.transform.forward = facingDir;
            projectile.AddColliderToExclude(collider);
            Spawn(projectile.gameObject, Owner);
        }
    }
}
