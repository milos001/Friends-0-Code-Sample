using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class SpawnProjectilesEffect : Effect
{
    [SerializeField]
    private AbilityController _projectilePrefab;
    [SerializeField]
    private int _projectileCount;
    [SerializeField]
    private bool _excludeHitTarget = true;
    [Header("Random Parameters")]
    [SerializeField]
    private bool _randomPositions;
    [SerializeField]
    private float _randomRadius;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        Vector3 spawnPos = transform.position;
        if (!entityInterface.CheckForAbilityPool(_projectilePrefab.Id))
            entityInterface.AddAbilityPrefab(_projectilePrefab);

        for (int i = 0; i < _projectileCount; i++)
        {
            if(_randomPositions)
                spawnPos += new Vector3(Random.Range(-_randomRadius, _randomRadius), Random.Range(-_randomRadius, _randomRadius));

            AbilityController projectile = entityInterface.GetPooledAbility(_projectilePrefab.Id);
            projectile.transform.position = spawnPos;

            if (_excludeHitTarget)
            {
                Projectile projectileModule = projectile.GetComponent<Projectile>();
                projectileModule.AddColliderToExclude(collider);
                projectileModule.AddEnemyToIgnore(collider.GetComponent<Enemy>());
            }

            projectile.Initialize(entityInterface, Guid.NewGuid(), 0f);
        }
    }
}
