using System;
using FishNet.Connection;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core
{
     [Serializable]
     public struct InteractionProgress
     {
          public float currentDuration;
          public float requiredDuration;
          public bool isCompleted;
          public NetworkConnection interactor;
    
          public float Progress => requiredDuration > 0 
               ? Mathf.Clamp01(currentDuration / requiredDuration) 
               : 0f;
     }
}