using UnityEngine;
using FishNet.Transporting.Tugboat;
using FishNet;
using FishNet.Transporting;

namespace NightHunt.Gameplay.Utils{
    public class ConnectionStarter : MonoBehaviour
    {
        private Tugboat tugboat;

        private void OnEnable() {
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void OnDisable() {
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args) {
            if(args.ConnectionState == LocalConnectionState.Stopping){
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }




        private void Start() {
            if(TryGetComponent(out Tugboat t)){
                tugboat = t;
            }
            else{
                Debug.LogError("ConnectionStarter: Tugboat component not found on this object");
                return;
            }

            if (ParrelSync.ClonesManager.IsClone())
            {
                tugboat.StartConnection(false);
            }
            else{
                tugboat.StartConnection(true); 
            }
        }
    }
}