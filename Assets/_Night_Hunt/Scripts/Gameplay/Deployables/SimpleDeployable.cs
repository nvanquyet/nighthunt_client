using UnityEngine;
using FishNet.Object;

namespace NightHunt.Gameplay.Deployables
{
    /// <summary>
    /// Concrete deployable with only BaseDeployable health, ownership, and fog visibility.
    /// Use this for placeable network objects that do not need trap or vision behavior.
    /// </summary>
    public sealed class SimpleDeployable : BaseDeployable
    {
        [Header("Runtime")]
        [SerializeField] private string _definitionId;

        [Server]
        public void SetDefinitionId(string definitionId)
        {
            _definitionId = definitionId;
        }

        [Server]
        protected override void OnDeployablePlaced()
        {
            base.OnDeployablePlaced();
            Debug.Log($"[SimpleDeployable] Placed {_definitionId} team={OwnerTeamId} hp={CurrentHP}");
        }
    }
}
