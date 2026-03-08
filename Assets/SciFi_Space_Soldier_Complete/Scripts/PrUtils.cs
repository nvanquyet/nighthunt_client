using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PrUtils{

    public static string[] SetMultiplayerInputs(int playerNmb, PrPlayerSettings playerSettings, string[] playerCtrlMap)
    {

        if (playerNmb > 1)
        {
            int values = 0;
            foreach (string ctrl in playerSettings.playerCtrlMap)
            {
                playerCtrlMap[values] = ctrl + playerNmb.ToString();
                values += 1;
            }
        }
        else
        {
            playerCtrlMap = playerSettings.playerCtrlMap;
        }

        return(playerCtrlMap);
    }

    public static string floatToTimerString(float timer)
    {
        string minString = Mathf.Floor(timer / 60).ToString("00");
        string secString = Mathf.Floor(timer % 60).ToString("00");

        string finalTimer = minString + ":" + secString;
        return finalTimer; 
    }

    public static GameObject InstantiateActor(GameObject actor, Vector3 pos, Quaternion rot, string name, Transform parent)
    {
        GameObject newActor = Object.Instantiate(actor, pos, rot, parent);
        newActor.name = name;
        return newActor;
    }

    public static Vector3 RaycastToFloor(Vector3 StartPosition)
    {
        Vector3 finalPos = Vector3.zero;

        RaycastHit hit;
        
        ////Debug.Log(maxDistance);

        if (Physics.Raycast(StartPosition, Vector3.down, out hit, 10))
        {
            finalPos = hit.point;
        }
        else
        {
            finalPos = StartPosition;
        }
        return finalPos;
    }

}
