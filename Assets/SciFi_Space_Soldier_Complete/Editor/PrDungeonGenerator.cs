#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

public class PrDungeonGenerator : ScriptableWizard
{
    public Object[] AllRooms;
    public Object[] AllProps;

    private GameObject[] InitialRooms;
    private GameObject[] Rooms;
    private GameObject[] BossRooms;
    private GameObject[] Corridors;
    private GameObject[] Connectors;
    private GameObject[] ClosedDoors;
    private GameObject[] DoorsPrefabs;
    
    private Transform Dungeon;
    private List<GameObject> initialRoomsPool = new List<GameObject>();
    private List<GameObject> roomsPool = new List<GameObject>();
    private List<GameObject> bossRoomsPool = new List<GameObject>();
    private List<GameObject> corridorsPool = new List<GameObject>();
    private List<GameObject> connnectorsPool = new List<GameObject>();
    private List<GameObject> doorsPool = new List<GameObject>();
    private List<GameObject> closedDoorsPool = new List<GameObject>();

    private List<Transform> currentRooms = new List<Transform>();
    private List<Transform> roomDoors = new List<Transform>();
    private List<Transform> newDoors = new List<Transform>();

    private List<Vector3> roomVolumes = new List<Vector3>();
    private List<Bounds> roomBVolumes = new List<Bounds>();
    //private List<Transform> removeDoors = new List<Transform>();

    // Boss Room update:
    public bool useBossRooms = true;
    private bool bossRoomCreated = false;

    public string main_set_folder = "Demo";
    public string rooms_folder = "Rooms";
    public string props_folder = "Props";

    public int seed = 0;
    public int iterations = 2;
    private int currentIterations = 2;
    public int roomLimit = 4;
    public bool randomSeed = true;

    public bool assignColorsToRooms = true;

    //public bool useVolumes = false;
    public float collisionGranularity = 2.5f;
    private int roomCount = 0;
    private int nextRoomType = 0;

    // Colors
    public Color InitialRoomColor = new Color();
    public Color RoomColors = new Color();
    public Color BossRoomColors = new Color();
    public Color CorridorColors = new Color();
    public Color DoorColors = new Color();

    public Material initialRoomMaterial;

    [MenuItem("PolygonR/Dungeon Generator...")]
    static void CreateWizard()
    {
        ScriptableWizard.DisplayWizard<PrDungeonGenerator>("PolygonR : Create Dungeon", "Close", "Create Dungeon");
        
    }

    void Awake()
    {
        //load prefs
        if (PlayerPrefs.HasKey("RoomFolder"))
        {
            rooms_folder = PlayerPrefs.GetString("RoomFolder");
        }
        if (PlayerPrefs.HasKey("PropsFolder"))
        {
            props_folder = PlayerPrefs.GetString("PropsFolder");
        }
        if (PlayerPrefs.HasKey("MaxRooms"))
        {
            roomLimit = PlayerPrefs.GetInt("MaxRooms");
        }

        AllRooms = Resources.LoadAll("DungeonGenerator/" + main_set_folder + "/" + rooms_folder, typeof(GameObject));
        AllProps = Resources.LoadAll("DungeonGenerator/" + main_set_folder + "/" + props_folder, typeof(GameObject));

        UpdateRoomsAndSetRandomColors();

    }

    void OnWizardUpdate()
    {
        //AllRooms = Resources.LoadAll("Rooms", typeof(GameObject));
        //SetRandomColors();
    }

    void OnWizardCreate()
    {
        //SetRandomColors();

        //DeleteOlderDungeon();

        //CreateDungeon();
    }

    void DeleteOlderDungeon()
    {
        bossRoomCreated = false;

        if (GameObject.Find("Dungeon"))
        {
            //Debug.Log("Object Found");
            DestroyImmediate(GameObject.Find("Dungeon"));
        }
    }

    void UpdateRoomsAndSetRandomColors()
    {
        //AllRooms = Resources.LoadAll(rooms_folder, typeof(GameObject));
        //AllProps = Resources.LoadAll(props_folder, typeof(GameObject));

        SetRandomColors();
    }

    void SavePrefs()
    {
        PlayerPrefs.SetString("PropsFolder", props_folder);
        PlayerPrefs.SetString("RoomFolder", rooms_folder);
        PlayerPrefs.SetInt("MaxRooms", roomLimit);
    }

    void OnWizardOtherButton()
    {
        NewDungeon();
    }

    void NewDungeon()
    {
        SavePrefs();

        SetRandomColors();

        DeleteOlderDungeon();

        CreateDungeon();
    }

    Vector3 RandomColor(bool desaturate, bool darken)
    {
        Vector3 HSV = new Vector3(Random.Range(0f, 1f), Random.Range(0.7f, 1.0f), Random.Range(0.8f, 1.0f));
        if (desaturate)
            HSV.y = Random.Range(0.1f, 0.3f);
        if (darken)
        {
            HSV.y = Random.Range(0.5f, 0.7f);
            HSV.z = Random.Range(0.25f, 0.4f);
        }
                       
        return HSV;
    }

    Vector3 DesaturateColor(Vector3 col)
    {
        Vector3 color = col;
        //Hue Variation
        color.x += Random.Range(0.05f, -0.05f);
        color.x = Mathf.Clamp(color.x, 0.0f, 1.0f);
        //Desaturated
        color.y = Random.Range(0.1f, 0.3f);
        return color; 
    }

    Vector3 DarkenColor(Vector3 col)
    {
        Vector3 color = col;
        //Hue Variation
        color.x += Random.Range(0.05f, -0.05f);
        color.x = Mathf.Clamp(color.x, 0.0f, 1.0f);
        //Darken
        color.y = Random.Range(0.4f, 0.8f);
        color.z = Random.Range(0.3f, 0.55f);
        return color;
    }

    Vector3 OpossiteColor(Vector3 col)
    {
        Vector3 color = col;
        color.x = col.x + Random.Range(0.45f,0.55f);
        if (color.x > 1.0f)
        {
            color.x -= 1.0f;
        }
        return color;
    }

    void SetRandomColors()
    {
        Vector3 roomColor = RandomColor(false, false);
        Vector3 bossRoomColor = DarkenColor(roomColor);
        Vector3 corrColor = DesaturateColor(roomColor);
        Vector3 initialRoom = OpossiteColor(roomColor);
        Vector3 doorColor = DarkenColor(roomColor);

        RoomColors = Color.HSVToRGB(roomColor.x, roomColor.y, roomColor.z);
        BossRoomColors = Color.HSVToRGB(bossRoomColor.x, bossRoomColor.y, bossRoomColor.z);
        CorridorColors = Color.HSVToRGB(corrColor.x, corrColor.y, corrColor.z);
        DoorColors = Color.HSVToRGB(doorColor.x, doorColor.y, doorColor.z);
        InitialRoomColor = Color.HSVToRGB(initialRoom.x, initialRoom.y, initialRoom.z);
    }

    void AddRoomVolumes(GameObject room)
    {

        if (!room.GetComponent<Collider>() || !room.GetComponent<MeshCollider>())
        {
            room.AddComponent<MeshCollider>();
        }
        Bounds roomBounds = room.GetComponent<Collider>().bounds;
        roomBVolumes.Add(roomBounds);
    }

    bool CheckRoomCollision(GameObject room)
    {
        bool roomCollides = false;

        Vector3[] roomPositions = GetRoomPositions(room);

        if (roomPositions.Length > 0)
        {
            foreach (Vector3 a in roomPositions)
            {
                ////Debug.Log("RoomPosition = " + a.position);
                foreach (Bounds x in roomBVolumes)
                {
                    if (x.Contains(a))
                    {
                        roomCollides = true;
                    }
                }
            }
        }
        return roomCollides;
    }

    Vector3[] GetRoomPositions(GameObject room)
    {
        //Debug.Log(room);
        Vector3[] roomPositions = new Vector3[0];
        if (room)
        {
            if (room.GetComponent<PrDungeonRoom>() == null)
            {
                room.AddComponent<PrDungeonRoom>();
            }

            bool deleteCollider = false;
            //Debug.Log(room.name);
            if (room.GetComponent<Collider>() == null)
            {
                room.AddComponent<BoxCollider>();
                deleteCollider = true;
            }
            Bounds roomBounds = room.GetComponent<Collider>().bounds;
            //Debug.Log(roomBounds);
            if (deleteCollider)
            {
                DestroyImmediate(room.GetComponent<Collider>());
            }

            float xBound = (roomBounds.extents.x - collisionGranularity);
            float zBound = (roomBounds.extents.z - collisionGranularity);

            int xVolumes = (Mathf.RoundToInt(xBound / collisionGranularity) * 2) + 1;
            int zVolumes = (Mathf.RoundToInt(zBound / collisionGranularity) * 2) + 1;

            int volumesCount = (xVolumes) * (zVolumes);
            //Set points
            roomPositions = new Vector3[volumesCount];
            int currentVolume = 0;
            for (int z = 0; z < zVolumes; z++)
            {
                float zPos = -zBound + (z * collisionGranularity);
                for (int i = 0; i < xVolumes; i++)
                {
                    float xPos = -xBound + (i * collisionGranularity);
                    roomPositions[currentVolume] = new Vector3(xPos + room.transform.position.x, 0, zPos + room.transform.position.z);
                    currentVolume++;
                }
            }

        }
        if (roomPositions.Length > 0)
        { /*
            foreach (Vector3 a in roomPositions)
            {
                Debug.Log("NewPos " + a);
            }
            */
        }
        else
        {
            //Debug.Log(roomPositions.Length);
        }
        //Debug.Log(roomPositions);
        return roomPositions;
    }

    void CreateArrays()
    {
        //reset variables
        initialRoomsPool = new List<GameObject>();
        roomsPool = new List<GameObject>();
        bossRoomsPool = new List<GameObject>();
        corridorsPool = new List<GameObject>();
        connnectorsPool = new List<GameObject>();
        doorsPool = new List<GameObject>();
        closedDoorsPool = new List<GameObject>();

        currentRooms = new List<Transform>();
        roomDoors = new List<Transform>();
        newDoors = new List<Transform>();

        roomVolumes = new List<Vector3>();
        roomBVolumes = new List<Bounds>();

        roomCount = 0;
        nextRoomType = 0;
        currentIterations = iterations;

        foreach (GameObject a in AllRooms)
        {
            if (a.GetComponent<PrDungeonRoom>().roomType == PrDungeonRoom.rType.Room)
            {
                roomsPool.Add(a);
                if (a.GetComponent<PrDungeonRoom>().initialRoom)
                {
                    initialRoomsPool.Add(a);
                }
            }
            else if (a.GetComponent<PrDungeonRoom>().roomType == PrDungeonRoom.rType.BossRoom)
            {
                bossRoomsPool.Add(a);
            }
            else if (a.GetComponent<PrDungeonRoom>().roomType == PrDungeonRoom.rType.Corridor)
            {
                corridorsPool.Add(a);
            }
            else if (a.GetComponent<PrDungeonRoom>().roomType == PrDungeonRoom.rType.Connection)
            {
                connnectorsPool.Add(a);
            }
            else if (a.GetComponent<PrDungeonRoom>().roomType == PrDungeonRoom.rType.Door)
            {
                doorsPool.Add(a);
            }
            else if (a.GetComponent<PrDungeonRoom>().roomType == PrDungeonRoom.rType.BrokenDoor)
            {
                closedDoorsPool.Add(a);
            }

        }

        InitialRooms = initialRoomsPool.ToArray();
        Debug.Log("init: " + InitialRooms.Length);
        Rooms = roomsPool.ToArray();
        BossRooms = bossRoomsPool.ToArray();
        Debug.Log("Rooms: " + Rooms.Length);
        Corridors = corridorsPool.ToArray();
        Debug.Log("Corridors: " + Corridors.Length);
        Connectors = connnectorsPool.ToArray();
        Debug.Log("Connectors: " + Connectors.Length);
        ClosedDoors = closedDoorsPool.ToArray();
        Debug.Log("ClosedDoors: " + ClosedDoors.Length);
        DoorsPrefabs = doorsPool.ToArray();
        Debug.Log("DoorsPrefabs: " + DoorsPrefabs.Length);

        if (InitialRooms.Length == 0)
        {
            InitialRooms = Rooms;
        }
    }

    void CreateDungeon()
    {
        CreateArrays();

        Dungeon = new GameObject("Dungeon").transform;

        if (randomSeed)
        {
            seed = Random.Range(0, 1024);
            //Debug.Log("SEED =" + seed);
        }

        Random.InitState(seed);
        //Create Room ZERO
        GameObject initialRoom = Instantiate(InitialRooms[Random.Range(0,InitialRooms.Length)], Vector3.zero, Quaternion.identity) as GameObject;
        if (assignColorsToRooms)
        {
            initialRoom.GetComponent<Renderer>().material = initialRoomMaterial;
            initialRoom.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", InitialRoomColor);
        }

        initialRoom.name = "StartRoom";
        currentRooms.Add(initialRoom.transform);
        AddRoomVolumes(initialRoom);
        
        roomCount += 1;
        nextRoomType = 1;
        AddDoorsToList(initialRoom, roomDoors, true);
        initialRoom.transform.SetParent(Dungeon);

        for (int i = 0; i <= currentIterations; i++)
        {
            int roomDoorsCount = roomDoors.Count;
            if (roomDoorsCount > 0 )
            {
                for (int x = 0; x < roomDoorsCount; x++)
                {
                    //GetRandomDoor(roomDoors);
                    if (nextRoomType == 0 && roomCount < roomLimit)
                    {
                        
                        if (useBossRooms && roomCount > (roomLimit - (roomLimit / 4)) && !bossRoomCreated)
                        {
                            CreateRoom(BossRooms, roomDoors[x], "BossRoom_");
                        }
                        else
                        {
                            CreateRoom(Rooms, roomDoors[x], "Room_");
                        }
                        //CreateRoom(Rooms, roomDoors[x], "Room_");
                        ////Debug.Log(x);
                    }
                    else if (nextRoomType == 1)
                    {
                        CreateRoom(Corridors, roomDoors[x], "Corridor_");
                        ////Debug.Log(x);
                    }
                    else
                    {
                        CreateRoom(Connectors, roomDoors[x], "Connector_");
                        ////Debug.Log(x);
                    }
                }
                ChangeRoomType();
                roomDoors = newDoors;
                newDoors = new List<Transform>();

                if (roomCount < roomLimit && roomDoors.Count > 0)
                {
                    currentIterations += 1;
                }

            }
            
        }
        ////Debug.Log("new Doors Count")
        if (roomDoors.Count > 0)
        {
            foreach (Transform a in roomDoors)
            {
                CloseDoor(a);
            }
        }

        //Saves the Scene
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(), null,false);

        //Generate Navigation Mesh
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        
        //Delete Default Objects if they are in the scene
        if (GameObject.Find("Main Camera"))
            DestroyImmediate(GameObject.Find("Main Camera"));
        if (GameObject.Find("Directional Light"))
            DestroyImmediate(GameObject.Find("Directional Light"));
        //Instantiates the Game Manager for the level with the Player start nodes. 
        if (GameObject.Find("SinglePlayerLocalGame") == null)
        {
            GameObject gameSetup = Instantiate(Resources.Load("DungeonGenerator/SinglePlayerLocalGame", typeof(GameObject)), null) as GameObject;
            gameSetup.name = "SinglePlayerLocalGame";
        }

        //Saves Gameobject with Seed and room  settings.
        GameObject help = new GameObject("Rooms=" + roomCount + "_Seed=" + seed + "_Iterations="+ currentIterations);
        Debug.Log("Last Dungeon setup: Rooms=" + roomCount + "_Seed=" + seed + "_Iterations=" + currentIterations);
        help.transform.SetParent(GameObject.Find("Dungeon").transform);

        if (useBossRooms && !bossRoomCreated)
        {
            NewDungeon();
        }
    }

    void CreateRoom(GameObject[] RoomType,Transform doors, string name)
    {
        //GetRandomDoor(roomDoors);
        int nextRoom = Random.Range(0, RoomType.Length);
        Debug.Log("RoomType: " + name);

        GameObject tempRoom = Instantiate(RoomType[nextRoom], doors.transform.position, doors.transform.rotation) as GameObject;
        tempRoom.name = name + roomCount;
        //Transform d = tempRoom.transform.GetComponentInChildren<PrDungeonRoom>().Doors[0];
        Vector3 posDif = tempRoom.transform.GetComponentInChildren<PrDungeonRoom>().Doors[0].position - tempRoom.transform.position;
        tempRoom.transform.position -= posDif;
        if (assignColorsToRooms)
        {
            if (nextRoomType == 0)
            {
                if (name.Contains("Boss"))
                {
                    tempRoom.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_Color", BossRoomColors);
                    tempRoom.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_BaseColor", BossRoomColors);
                }
                else
                {
                    tempRoom.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_Color", RoomColors);
                    tempRoom.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_BaseColor", RoomColors);
                }
            }
            else if (nextRoomType >= 1)
            {
                tempRoom.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_Color", CorridorColors);
                tempRoom.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_BaseColor", CorridorColors);
            }
        }

       
        //SnapRotations =     v
        Vector3 vec = tempRoom.transform.eulerAngles;
        vec.x = Mathf.Round(vec.x / 90) * 90;
        vec.y = Mathf.Round(vec.y / 90) * 90;
        vec.z = Mathf.Round(vec.z / 90) * 90;
        tempRoom.transform.eulerAngles = vec;

        //tempRoom.transform.rotation = Quaternion.Euler(tempRoom.transform.rotation.eulerAngles.x, ((float)Mathf.Round(tempRoom.transform.rotation.eulerAngles.y / 90) * 90), tempRoom.transform.rotation.eulerAngles.z);
        tempRoom.transform.position = new Vector3((float)Mathf.Round(tempRoom.transform.position.x / 1) * 1, 0, (float)Mathf.Round(tempRoom.transform.position.z / 1) * 1);

        //CheckCollision
        bool isColliding = CheckRoomCollision(tempRoom);
        if (!isColliding)
        {
            AddRoomVolumes(tempRoom);
            currentRooms.Add(tempRoom.transform);
            if (name == "Room_")
                roomCount += 1;
            //roomDoors.Remove(doors);
            AddDoorPrefab(doors);
            AddDoorsToList(tempRoom, newDoors, false);

            tempRoom.transform.SetParent(Dungeon);
            tempRoom.transform.SetParent(doors.transform.parent);

            //Spawn props
            List<GameObject> props_pool = new List<GameObject>();
            foreach (Object a in AllProps)
            {
                if (a.name.Contains(RoomType[nextRoom].name))
                {
                    props_pool.Add(a as GameObject);
                }
                
            }
            Object[] selected_props = props_pool.ToArray();
            if (selected_props.Length > 0)
            {
                //select random prefab from group
                int final_selection = Random.Range(0, selected_props.Length);
                GameObject tempProps = Instantiate(selected_props[final_selection], tempRoom.transform.position, tempRoom.transform.rotation) as GameObject;
                tempProps.name = name + roomCount + "_props";
                tempProps.transform.SetParent(tempRoom.transform);
            }

            if (name.Contains("Boss"))
            {
                bossRoomCreated = true;
            }
        }
        else
        {
            //Debug.LogWarning("Room is Colliding");
            DestroyImmediate(tempRoom);
            CloseDoor(doors);
        }
    }
    
    void AddDoorPrefab(Transform doors)
    {
        int randomDoor = Random.Range(0, DoorsPrefabs.Length);
        GameObject tempDoor = Instantiate(DoorsPrefabs[randomDoor], doors.transform.position, doors.transform.rotation) as GameObject;
        tempDoor.name = "Door";
        tempDoor.transform.SetParent(Dungeon);
        tempDoor.transform.SetParent(doors.transform.parent);
    }

    void CloseDoor(Transform doors)
    {
        GameObject tempDoor = Instantiate(ClosedDoors[0], doors.transform.position, doors.transform.rotation) as GameObject;
        tempDoor.name = "ClosedDoor";
        tempDoor.transform.SetParent(Dungeon);
        tempDoor.transform.SetParent(doors.transform.parent);
        /*if (assignColorsToRooms)
        {
            tempDoor.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_Color", DoorColors);
            tempDoor.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_BaseColor", DoorColors);
        }*/
    }

    void ChangeRoomType()
    {
        nextRoomType = Random.Range(0, 2);
        //Debug.Log("next Room: " + nextRoomType);
        /*
        if (nextRoomType < 2)
        {
            nextRoomType += 1;
        }
        else
        {
            nextRoomType = 0;
        }*/
    }

    void AddDoorsToList(GameObject room, List<Transform> listToAdd, bool addFirst)
    {
        int index = 0;

        foreach (Transform d in room.transform.GetComponentInChildren<PrDungeonRoom>().Doors)
        {
            if (addFirst && index == 0)
            {
                listToAdd.Add(d);
            }
            else if (index > 0)
            {
                listToAdd.Add(d);
            }

            index += 1;
        }
        //roomDoors.AddRange(room.transform.GetComponentInChildren<PrDungeonRoom>().Doors);
        ////Debug.Log("Doors Are:");
        foreach(Transform r in listToAdd)
        {
            if (r != null)
                r.gameObject.AddComponent<PrDungeonDoor>();
            ////Debug.Log(r);
        }

        //roomDoors = listToAdd;
        /*
        foreach (Transform a in roomDoors)
        {
            Debug.Log(a);
        }*/
        
    }
    
    void ReparentObjects(Transform Target, Transform newParent)
    {
        //Debug.Log("Reparent objects Target =" + Target.name + " New Parent = " + newParent);
        Target.parent = newParent;
    }

}
#endif