using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PrDungeonRoom : MonoBehaviour
{
    public enum rType
    {
        Room,
        BossRoom,
        Corridor,
        Connection,
        Door,
        BrokenDoor
    }
    public rType roomType;
    public bool initialRoom = false;
    [HideInInspector]
    public int totalDoors = 0;
    [HideInInspector]
    public int freeDoors = 0;
    //[HideInInspector]
    public Transform[] Doors;
    //[HideInInspector]
    //public Transform[] RoomVolumes;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
