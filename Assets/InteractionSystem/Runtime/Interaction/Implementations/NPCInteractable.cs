using FishNet.Connection;
using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Interaction
{
     public class NPCInteractable : InteractableBase
     {
          [Header("NPC")]
          [SerializeField] private string npcName = "Trader";
          [SerializeField] private NPCType npcType = NPCType.Trader;
    
          [Header("Dialogue")]
          [SerializeField] private string[] dialogueLines;
    
          public enum NPCType
          {
               Trader,
               QuestGiver,
               Information
          }
    
          private void Start()
          {
               interactionType = InteractionType.Immediate;
               interactionPrompt = $"Talk to {npcName}";
          }
    
          public override void OnInteract(NetworkConnection player)
          {
               if (!IsServer) return;
        
               switch (npcType)
               {
                    case NPCType.Trader:
                         TargetOpenTradeUI(player);
                         break;
            
                    case NPCType.QuestGiver:
                         TargetShowQuest(player);
                         break;
            
                    case NPCType.Information:
                         TargetShowDialogue(player, dialogueLines);
                         break;
               }
          }
    
          [TargetRpc]
          private void TargetOpenTradeUI(NetworkConnection conn)
          {
               // TODO: Implement trade UI
               Debug.Log("Trade UI opened");
          }
    
          [TargetRpc]
          private void TargetShowQuest(NetworkConnection conn)
          {
               // TODO: Implement quest UI
               Debug.Log("Quest UI opened");
          }
    
          [TargetRpc]
          private void TargetShowDialogue(NetworkConnection conn, string[] lines)
          {
               // TODO: Implement dialogue UI
               Debug.Log($"Dialogue: {string.Join("\n", lines)}");
          }
     }
}