using FishNet.Object;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Gameplay.Deployables
{
    /// <summary>
    /// Minimal network deployable used by generic placed items. Specific gameplay
    /// effects can subclass BaseDeployable later without changing item definitions.
    /// </summary>
    public sealed class GenericDeployable : BaseDeployable
    {
        private DeployableKind _kind;
        private string _definitionId;

        [Server]
        public void Initialize(int teamId, int maxHP, DeployableKind kind, string definitionId)
        {
            _kind = kind;
            _definitionId = definitionId;
            base.Initialize(teamId, maxHP);
        }

        [Server]
        protected override void OnDeployablePlaced()
        {
            Debug.Log($"[GenericDeployable] Placed {_definitionId} kind={_kind} team={OwnerTeamId} hp={CurrentHP}");
        }
    }
}
