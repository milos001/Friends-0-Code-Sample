using FishNet.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AbilityManager : NetworkBehaviour
{
    public Transform PlayerAbilityParent, EnemyAbilityParent;
    public Action PlayerAbilitiesChanged;

    private Dictionary<Guid, AbilityController> _playerAbilities = new Dictionary<Guid, AbilityController>();
    private Dictionary<int, List<AbilityController>> _playerAbilitiesDic = new Dictionary<int, List<AbilityController>>();
    private Dictionary<int, List<AbilityController>> _enemyAbilities = new Dictionary<int, List<AbilityController>>();
    private Dictionary<Guid, Vector3> _networkedAbilityPositions = new Dictionary<Guid, Vector3>();

    public void Initialize(){}

    public void AddPlayerAbility(AbilityController ability, int playerId, Guid uniqueId)
    {
        if(!_playerAbilitiesDic.ContainsKey(playerId))
            _playerAbilitiesDic.Add(playerId, new List<AbilityController>());

        _playerAbilitiesDic[playerId].Add(ability);

        _playerAbilities.Add(uniqueId, ability);

        if (PlayerAbilitiesChanged != null)
            PlayerAbilitiesChanged.Invoke();
    }

    public void RemovePlayerAbility(AbilityController ability, int playerId)
    {
        if (!_playerAbilitiesDic.ContainsKey(playerId) || !_playerAbilitiesDic[playerId].Contains(ability))
            return;

        _playerAbilitiesDic[playerId].Remove(ability);

        if (PlayerAbilitiesChanged != null)
            PlayerAbilitiesChanged.Invoke();
    }

    public AbilityController GetPlayerAbility(Guid abilityId)
    {
        if (!_playerAbilities.ContainsKey(abilityId))
            return null;

        return _playerAbilities[abilityId];
    }

    public AbilityController GetPlayerAbility(int playerId, int abilityId)
    {
        if (!_playerAbilitiesDic.ContainsKey(playerId) || !_playerAbilitiesDic[playerId].Any(x => x.Id == abilityId))
            return null;

        return _playerAbilitiesDic[playerId].First();
    }

    public AbilityController[] GetAllPlayerProjectiles()
    {
        List<AbilityController> projectiles = new List<AbilityController>();

        foreach (var abilityList in _playerAbilitiesDic.Values)
        {
            foreach (var ability in abilityList)
            {
                if (ability.TryGetComponent<Projectile>(out _))
                { 
                    projectiles.Add(ability);
                }
            }
        }

        return projectiles.ToArray();
    }

    public void AddEnemyAbility(AbilityController ability, int enemyId)
    {
        if (!_enemyAbilities.ContainsKey(enemyId))
            _enemyAbilities.Add(enemyId, new List<AbilityController>());

        _enemyAbilities[enemyId].Add(ability);
    }

    public void RemoveEnemyAbility(AbilityController ability, int playerId)
    {
        if (!_enemyAbilities.ContainsKey(playerId) || !_enemyAbilities[playerId].Contains(ability))
            return;

        _enemyAbilities[playerId].Remove(ability);
    }

    public void AddNetworkedAbility(Guid guid, Vector3 position)
    {
        AddNetworkedAbilityRpc(guid, position);
    }

    [ObserversRpc]
    private void AddNetworkedAbilityRpc(Guid guid, Vector3 position)
    {
        _networkedAbilityPositions.Add(guid, position);
    }

    public void RemoveNetworkedAbility(Guid guid)
    {
        RemoveNetworkedAbilityRpc(guid);
    }

    [ObserversRpc]
    private void RemoveNetworkedAbilityRpc(Guid guid)
    {
        _networkedAbilityPositions.Remove(guid);
    }

    public void UpdateNetworkedAbility(Guid guid, Vector3 position)
    {
        UpdateNetworkedAbilityRpc(guid, position);
    }

    [ObserversRpc]
    private void UpdateNetworkedAbilityRpc(Guid guid, Vector3 position)
    {
        _networkedAbilityPositions[guid] = position;
    }

    public Vector3 GetNetworkedAbilityPosition(Guid guid)
    {
        return _networkedAbilityPositions[guid];
    }
}
