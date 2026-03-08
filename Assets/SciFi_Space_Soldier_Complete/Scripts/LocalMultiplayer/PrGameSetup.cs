using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;


public class PrGameSetup : MonoBehaviour {

    public enum GameMode
    {
        SinglePlayer = 0,
        Coop = 1,
        DeathMatch = 2,
        TeamDeathMatch = 3,
        TowerDefense = 4,
        Survival = 5
    }

    [Header("Game Setup")]
    public GameMode mode = GameMode.Coop;
    public int actualPlayerCount = 4;
    public GameObject[] playersPrefabs;
    private GameObject[] actualPlayerPrefabs;
    private PrCharacter[] playerCharacters;
    private PrCharacterController[] playersControllers;
    private PrCharacterInventory[] playersInventorys;

    private GameObject[] playersForCamera;
    public Transform[] playersSpawnPos;
    public bool[] spawnPointFull;
    public PrPlayerSettings playersSettings;
    public PrLevelSettings levelSettings;
    public PrWeaponList weaponList;
    private PrObjectivesManager objectivesManager;
    private string[] weaponNames;

    public enum GameStage
    {
        inGame,
        EndedMatch,
        levelComplete
    }

    [Header("In Game Stats")]
    public GameStage stage = GameStage.inGame;
    public int actualLevel = 0;

    //pickups
    private GameObject[] pickups;
    private Vector3[] playersDeathPos;
    private bool useLives = false;
    private int[] livesPerPlayer;
    private bool[] playerReachedEndZone;
    
    [Header("SinglePlayer and coop Setup")]

    public GameObject LevelCompleteUI;
    public GameObject GameOverUI;
    private GameObject actualGameOverUI;
    private string[] coopPlayerStates;
    private float[] respawnTimers;

    [Header("DeathMatch Setup")]
    public int fragsToWin = 10;
    public GameObject[] fragCounter;
    private int[] playersFrags;
    public GameObject playerWinsText;
    public PrPlayerSettings playerSettings;

    [Header("Team DeathMatch HUD")]
    public GameObject[] teamfragCounter;
    private int[] teamFrags;

    public enum SurvivalStage
    {
        Start,
        InWave,
        WaitingWave,
        EndedMatch,
    }
    [Header("Survival Setup")]
    public SurvivalStage survivalStage = SurvivalStage.Start;

    public PrSurvivalWavesSetup survivalSetup;
    public PrEnemySpawner[] enemySpawners;

    private int actualWave = -1;
    private int actualWaveHUD = 0;
    private int waveEnemiesSpawned = 0;
    private int waveEnemiesDeath = 0;
    private int totalEnemiesSpawned = 0;
    //timers
    public bool displayTimer = true;
    public GameObject HUDSurvivalTimer;
    public GameObject HUDSurvivalActualWave;

    private float survivalTimer = 0.0f;

    private float interWavesTimer = 0.0f;
    private float actualWaveTimer = 0.0f;
    private float waveSpawnTimer = 0.0f;
    private float timeBetweenSpawn = 0.0f;

    
    [Header("Camera Setup")]
    private bool useSplitScreen = false;
    public bool useSingleScreenCameraLimits = false;
    public Vector2 targetHeightVariation = new Vector2(8, 50);
    public float targetHeightDistanceFactor = 1.0f;
    public float targetHeightCorrection = -3.0f;
    [HideInInspector]
    public PrTopDownMutiplayerCam actualCameraScript;

    [Header("Debug")]
    
    public Mesh areaMesh;
    public Mesh targetArrow;


    /*
    GameSetup flow:
    Level Load:
    - Sets players
    1) StartGame()
        ResetLives()
        Sets stage=InGame
        SpawnPlayer()
            CreatesPlayers
            Sets SplitScreen
        Creates Camera
        if survival:
            starts survival mode()

    SINGLEPLAYER:
    2) Player Dies:
        Message : New Frag (counter for deathmatch)
        Message : Player Died
        GameSetup > Game Over()
            Check lives > 
                Respawn or Death            

    */


    // Use this for initialization
    void Start () {

        this.tag = "Game";

        if (playerSettings && playersSettings.selectedCharacters.Length > 0)
        {
            playersPrefabs = playersSettings.selectedCharacters;
            actualPlayerCount = playersSettings.finalPlayerCount;
            respawnTimers = new float[4];
            int x = 0;
            foreach (float a in respawnTimers)
            {
                respawnTimers[x] = 0.0f;
                x += 1;
            }

            useSplitScreen = playerSettings.useSplitScreenForMultiplayer;
        }

        if (mode == GameMode.TeamDeathMatch)
            actualPlayerCount = 4;

        //Set initialarrays
        playerCharacters = new PrCharacter[4];
        playersControllers = new PrCharacterController[4];
        playersInventorys = new PrCharacterInventory[4];
        coopPlayerStates = new string[4];

        if (mode == GameMode.Coop)
        {
            int pS = 0;
            foreach (string playerState in coopPlayerStates)
            {
                if (pS < actualPlayerCount)
                    coopPlayerStates[pS] = "InGame";
                else
                    coopPlayerStates[pS] = "None";
                pS += 1;
            }
        }
        

        actualPlayerPrefabs = new GameObject[4];
        spawnPointFull = new bool[playersSpawnPos.Length];

        if (playersSettings)
        {
            if (weaponList)
            {
                weaponNames = new string[weaponList.weapons.Length];
                int index = 0;
                foreach (GameObject w in weaponList.weapons)
                {
                    weaponNames[index] = w.gameObject.GetComponent<PrWeapon>().WeaponName;
                    index++;
                }
            }
            pickups = GameObject.FindGameObjectsWithTag("Pickup");
            if (pickups.Length > 0)
            {
                foreach (GameObject pickup in pickups)
                {
                    if (pickup.GetComponent<PrPickupObject>())
                    {
                        pickup.GetComponent<PrPickupObject>().ColorSetup = playerSettings;
                        pickup.GetComponent<PrPickupObject>().weaponNames = weaponNames;
                        pickup.GetComponent<PrPickupObject>().Initialize();
                    }
                }
            }

            PrItemsSpawner[] itemSpawners = FindObjectsOfType<PrItemsSpawner>();
            if (itemSpawners.Length > 0)
            {
                foreach (PrItemsSpawner itemSpawner in itemSpawners)
                {
                    itemSpawner.settings = playerSettings;
                    itemSpawner.weaponNames = weaponNames;
                }
            }
        }

        //Initialize Endzones
        initializeEndzones();
        //Set Actual Level from levelsettings file
        actualLevel = levelSettings.actualLevel;
        //Start Game Setup
        StartGame();

    }
	
    void initializeEndzones()
    {
        //EndZone Initialization
        playerReachedEndZone = new bool[4];
        int indexReached = 0;
        foreach (bool reached in playerReachedEndZone)
        {
            playerReachedEndZone[indexReached] = false;
            indexReached = indexReached + 1;
        }
    }

    void ResetLives()
    {
        playersDeathPos = new Vector3[4];
        useLives = playerSettings.useLives;
        livesPerPlayer = new int[4];
       
        for (int x = 0; x < 4; x++)
        {
            playersDeathPos[x] = playersSpawnPos[x].position;
            if (playerSettings.livesPerPlayer >= 0)
            {
                livesPerPlayer[x] = playerSettings.livesPerPlayer;
            }
            else
            {
                livesPerPlayer[x] = 99999;
            }
        }
        
    }

    // Update is called once per frame
	void Update () {

        if (mode == GameMode.DeathMatch || mode == GameMode.TeamDeathMatch)
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                NewMultiplayerMatch(1);
            }
            if (Input.GetKeyDown(KeyCode.F6))
            {
                NewMultiplayerMatch(2);
            }
            if (Input.GetKeyDown(KeyCode.F7))
            {
                NewMultiplayerMatch(3);
            }
            if (Input.GetKeyDown(KeyCode.F8))
            {
                NewMultiplayerMatch(4);
            }

            if (Input.GetKeyDown(KeyCode.F4))
            {
                if (useSplitScreen)
                    playerSettings.useSplitScreenForMultiplayer = false;
                else
                    playerSettings.useSplitScreenForMultiplayer = true;

                NewMultiplayerMatch(actualPlayerCount);
            }
        }

        if (mode == GameMode.SinglePlayer || mode == GameMode.Coop)
        {
            if (stage != GameStage.levelComplete)
            {
                if (Input.GetButtonDown("Player1Start") && actualPlayerCount >= 1)
                {
                    if (stage == GameStage.EndedMatch)
                    {
                        NewMultiplayerMatch(1);
                    }
                    else
                    {
                        if (playersControllers[0].character.isDead == true)
                        {
                            DestroyPlayer(0);
                            if (useLives && livesPerPlayer[0] > 0)
                            {
                                RestartLvlFromLastObjective(1);
                            }
                        }
                    }

                }

                UpdateRespawnTimers();
            }
            else
            {
                if (Input.GetButtonDown("Player1Start") && actualPlayerCount >= 1)
                {
                    LevelDone();
                }
            }
           
        }

        if (mode == GameMode.DeathMatch || mode == GameMode.TeamDeathMatch)
        {
            if (Input.GetButtonDown("Player1Start") && playersSettings.playersInGame[0])
            {
                if (stage == GameStage.EndedMatch)
                {
                    NewMultiplayerMatch(actualPlayerCount);
                }
                else
                {
                    RespawnPlayer(0);
                }
                    
            }

            if (Input.GetButtonDown("Player2Start") && playersSettings.playersInGame[1])
            {
                if (stage == GameStage.EndedMatch)
                {
                    NewMultiplayerMatch(actualPlayerCount);
                }
                else
                {
                    RespawnPlayer(1);
                }

            }
            if (Input.GetButtonDown("Player3Start") && playersSettings.playersInGame[2])
            {
                if (stage == GameStage.EndedMatch)
                {
                    NewMultiplayerMatch(actualPlayerCount);
                }
                else
                {
                    RespawnPlayer(2);
                }
            }
            if (Input.GetButtonDown("Player4Start") && playersSettings.playersInGame[3])
            {
                if (stage == GameStage.EndedMatch)
                {
                    NewMultiplayerMatch(actualPlayerCount);
                }
                else
                {
                    RespawnPlayer(3);
                }
            }
        }
        
        if (spawnPointFull.Length > 0 && playersSpawnPos.Length > 0)
        {
            for (int i = 0; i < spawnPointFull.Length; i++)
            {
                spawnPointFull[i] = playersSpawnPos[i].GetComponent<PrSpawnPoint>().isFull;
            }
        }

        if (mode == GameMode.Survival)
        {
            UpdateSurvivalGame();
        }
    }

    void UpdateRespawnTimers()
    {
        int r = 0;
        foreach (float respawnT in respawnTimers)
        {
            if (coopPlayerStates[r] == "Respawning")
            {
                if (respawnT > 0.0f)
                {
                    respawnTimers[r] -= Time.deltaTime;

                }
                else
                {
                    respawnTimers[r] = 0.0f;
                    RespawnPlayer(r);
                }
            }

            r += 1;
        }
    }

    void RespawnPlayer(int plyNmb)
    {
        if (playersControllers[plyNmb].character.isDead == true)
        {
            DestroyPlayer(plyNmb);
            SpawnPlayer(plyNmb, true);
            PlayerSpawned();
        }
    }

    void RestartLvlFromLastObjective(int playerCount)
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    }

    void NewMultiplayerMatch(int playerCount)
    {
        for (int i = 0; i < 4; i++)
        {
            if (actualPlayerPrefabs[i] != null)
                DestroyPlayer(i);
            
            if (playersInventorys[i] != null)
                playersInventorys[i].character.DestroyHUD();

        }

        actualPlayerCount = playerCount;
        if (mode == GameMode.TeamDeathMatch)
            actualPlayerCount = 4;

        StartGame();
        
    }
    /*
    void HideSpawnPoints()
    {
        foreach (Transform spawn in playersSpawnPos)
        {
            spawn.gameObject.SetActive(false);
        }
    }*/


    public void SetXP(int plyrNmb, int XpPoints)
    {
        ////Debug.Log("SetXP in Game Node " + plyrNmb + "_" + actualPlayerPrefabs[plyrNmb - 1] + "_" + actualPlayerPrefabs[plyrNmb]);
        //Debug.Log("Set XP " + plyrNmb);
        if (actualPlayerPrefabs.Length >= plyrNmb && (plyrNmb - 1) >= 0 && actualPlayerPrefabs[plyrNmb - 1])
        {
            playerCharacters[plyrNmb - 1].AddXP(XpPoints);
            playersInventorys[plyrNmb - 1].UpdateWeaponsLevel();
        }
    }

    void PlayerReachedEndZone(int playerNumber)
    {
        if (stage != GameStage.levelComplete)
        {
            if (objectivesManager != null && objectivesManager.allObjectivesClear)
            {
                if (actualPlayerPrefabs[playerNumber - 1] != null)
                {
                    ////Debug.Log("player is here " + playerNumber);
                    playerReachedEndZone[playerNumber - 1] = true;
                }
                int count = 0;
                foreach (bool reached in playerReachedEndZone)
                {
                    if (reached)
                        count = count + 1;
                }
                if (count >= actualPlayerCount)
                {
                    SetLevelComplete();
                }
            }
            else if (objectivesManager == null || objectivesManager.isActiveAndEnabled == false)
            {
                if (actualPlayerPrefabs[playerNumber - 1] != null)
                {
                    ////Debug.Log("player is here " + playerNumber);
                    playerReachedEndZone[playerNumber - 1] = true;
                }
                int count = 0;
                foreach (bool reached in playerReachedEndZone)
                {
                    if (reached)
                        count = count + 1;
                }
                if (count >= actualPlayerCount)
                {
                    SetLevelComplete();
                }
            }
        }
        
    }

    void SetLevelComplete()
    {
        if (LevelCompleteUI)
        {
            Instantiate(LevelCompleteUI, Vector3.zero, Quaternion.identity);
        }
        CallEnemiesToStop();
        foreach (PrCharacterController p in playersControllers)
        {
            if (p != null)
                p.m_CanMove = false;
        }
        stage = GameStage.levelComplete;
    }

    void CallEnemiesToStop()
    {
        PrAIController[] Enemies = FindObjectsOfType(typeof(PrAIController)) as PrAIController[];
        if (Enemies.Length != 0)
        {
            foreach (PrAIController enemy in Enemies)
            {
                enemy.StopAllActivities();
            }
        }
        PrEnemySpawner[] spawners = FindObjectsOfType(typeof(PrEnemySpawner)) as PrEnemySpawner[];
        foreach (var s in spawners)
        {
            s.SpawnerEnabled = false;
        }

    }

    void LevelDone()
    {
        
        //Debug.Log("Level Complete - Loading Next Level");

        if (mode == GameMode.SinglePlayer)
        {
            foreach (PrCharacterController player in playersControllers)
            {
                if (player != null)
                    player.SavePlayerInfo(false);
            }
        }

        LoadNextLevel();
                    
    }

    void LoadNextLevel()
    {
        int nextLevel = actualLevel + 1;
        levelSettings.actualLevel = nextLevel;

        if (mode == GameMode.SinglePlayer)
        {
            if (levelSettings.availableSinglePlayerLevels.Length > nextLevel)
                SceneManager.LoadScene(levelSettings.availableSinglePlayerLevels[nextLevel]);
        }
        else if (mode == GameMode.Coop)
        {
            SceneManager.LoadScene(levelSettings.availableCoopLevels[nextLevel]);
        }
        else if (mode == GameMode.DeathMatch)
        {
            SceneManager.LoadScene(levelSettings.availableMultiplayerLevels[nextLevel]);
        }
        else if (mode == GameMode.Survival)
        {
            SceneManager.LoadScene(levelSettings.availableSurvivalLevels[nextLevel]);
        }
        else if (mode == GameMode.TowerDefense)
        {
            SceneManager.LoadScene(levelSettings.availableTowerDefenseLevels[nextLevel]);
        }


    }

    void StartGame()
    {
        ResetLives();

        playersFrags = new int[4];
        for (int i = 0; i < 4; i++)
        {
            playersFrags[i] = 0;
        }

        teamFrags = new int[2];
        for (int i = 0; i < 2; i++)
        {
            teamFrags[i] = 0;
        }

        stage = GameStage.inGame;

        if (playersPrefabs.Length >= actualPlayerCount && playersSpawnPos.Length >= actualPlayerCount)
        {
           /* for (int x = 0; x < actualPlayerCount; x++)
            {
                SpawnPlayer(x, false);
            }*/
            for (int x = 0; x < 4; x++)
            {
                if (playersSettings.playersInGame[x])
                    SpawnPlayer(x, false);
            }

        }

        if (mode == GameMode.DeathMatch || mode == GameMode.TeamDeathMatch || mode == GameMode.Coop || mode == GameMode.Survival)
        {
            if (playerWinsText)
                playerWinsText.SetActive(false);

            CreateCamera();//Create the Camera

        }

        //HUD Reset
        if (mode == GameMode.DeathMatch || mode == GameMode.TeamDeathMatch )
        {
            ResetFragHUD();
            UpdateFragHUD();
            OrganizeFragHUD();

        }
        else if (mode == GameMode.Survival)
        {
            StartSurvivalMode();
        }
        else if (mode == GameMode.SinglePlayer || mode == GameMode.Coop)
        {
            if (gameObject.GetComponent<PrObjectivesManager>())
            {
                objectivesManager = gameObject.GetComponent<PrObjectivesManager>();
                objectivesManager.InitializeManager();
            }
        }

        ////Debug.Log(actualPlayerPrefabs[0]);

        if (PrPlayerInfo.player1 && PrPlayerInfo.player1.loadPrevSettings == true)
        {
            if (gameObject.GetComponent<PrObjectivesManager>())
            {
                //Set Last Objective and player lives
                objectivesManager.startingObjective = PrPlayerInfo.player1.lastObjectiveActive;
                objectivesManager.actualObjective = PrPlayerInfo.player1.lastObjectiveActive;
            }
            int pN = 0;
            foreach (PrCharacterController p in playersControllers)
            {
                if (p != null)
                {
                    p.transform.position = PrPlayerInfo.player1.lastPlayerPosition[pN];
                }
                pN += 1;
            }
            int pL = 0;
            if (useLives)
            {
                foreach (int lP in livesPerPlayer)
                {
                    livesPerPlayer[pL] = PrPlayerInfo.player1.lives[pL];
                    pL += 1;
                }
            }
                
            if (actualGameOverUI)
            {
               // //Debug.Log("Turning Off GameOver UI");
                actualGameOverUI.SetActive(false);
            }
            
        }
    }

    void UpdateLastPlayerPos(int thePlayer)
    {
        playersDeathPos[thePlayer] = playersControllers[thePlayer].transform.position;
        UpdateLastPlayerPosObjective(thePlayer);


    }

    void UpdateLastPlayerPosObjective(int thePlayer)
    {
        if (PrPlayerInfo.player1)
        {
            if (playersControllers[thePlayer] != null)
            {
                PrPlayerInfo.player1.lastPlayerPosition[0] = playersControllers[thePlayer].transform.position;
            }
        }
    }

    void StartSurvivalMode()
    {
        if (survivalSetup != null && enemySpawners.Length >= 1)
        {
            survivalStage = SurvivalStage.Start;
            foreach (PrEnemySpawner spawner in enemySpawners)
            {
                spawner.transform.parent = this.transform;
                spawner.SearchPlayerAfterSpawn = true;
                spawner.SpawnerEnabled = false;
                spawner.SpawnStartDelay = 0.0f;
            }
                
        }

    }

   /* void UpdateSurvivalTimer()
    {
        minString = Mathf.Floor(survivalTimer / 60).ToString("00");
        secString = Mathf.Floor(survivalTimer % 60).ToString("00");
    
    }*/

    void UpdateSurvivalGame()
    {
        if (stage == GameStage.inGame)
        {
            survivalTimer += Time.deltaTime;

            //UpdateSurvivalTimer();

            HUDSurvivalTimer.GetComponent<Text>().text = PrUtils.floatToTimerString(survivalTimer);
            if (survivalStage == SurvivalStage.Start)
            {
                HUDSurvivalTimer.GetComponent<Text>().text = "";
                HUDSurvivalActualWave.GetComponent<Text>().text = "";
                if (survivalTimer >= survivalSetup.initialTimer)
                    SetSurvivalWave();
            }

            if (survivalStage == SurvivalStage.InWave)
            {

                HUDSurvivalActualWave.GetComponent<Text>().text = "Wave " + (actualWaveHUD).ToString();
                actualWaveTimer += Time.deltaTime;
                waveSpawnTimer += Time.deltaTime;
                if (waveEnemiesSpawned < survivalSetup.waves[actualWave].enemiesCount)
                {
                    if (waveSpawnTimer >= timeBetweenSpawn)
                    {
                        SurvivalSpawnEnemy();
                    }
                }

            }
            if (survivalStage == SurvivalStage.WaitingWave)
            {
                HUDSurvivalActualWave.GetComponent<Text>().text = "";
                interWavesTimer += Time.deltaTime;
                if (interWavesTimer >= survivalSetup.timeBetweenWaves)
                {
                    SetSurvivalWave();
                }
            }
        }
        else if (stage == GameStage.EndedMatch)
        {
            if (Input.GetButtonDown("Player1Start") && actualPlayerCount >= 1)
            {
                RestartLvlFromLastObjective(1);
            }
        }
    }

    void SurvivalSpawnEnemy()
    {
        waveSpawnTimer = 0.0f;
        int actualSpawner = Random.Range(0, enemySpawners.Length);
        int enemyToSpawn = Random.Range(0, survivalSetup.waves[actualWave].Enemies.Length);

        enemySpawners[actualSpawner].SurvivalSpawnEnemy(survivalSetup.waves[actualWave].Enemies[enemyToSpawn]);

        waveEnemiesSpawned += 1;
        totalEnemiesSpawned += 1;
    }

    void InterWaveSetup()
    {
        survivalStage = SurvivalStage.WaitingWave;
        interWavesTimer = 0.0f;

    }

    void EnemyDead()
    {
        if (mode == GameMode.Survival)
        {
            waveEnemiesDeath += 1;
            if (waveEnemiesDeath == survivalSetup.waves[actualWave].enemiesCount)
            {
                InterWaveSetup();
            }
        }
       
    }

    void SetSurvivalWave()
    {
        SetSurvivalStage(SurvivalStage.InWave);
        actualWave += 1;
        actualWaveHUD += 1;

        ////Debug.Log(actualWave + " " + survivalSetup.waves.Length);
        if (actualWave >= survivalSetup.waves.Length)
        {
            actualWave -= survivalSetup.repeatLastWaves;
        }

        actualWaveTimer = 0.0f;
        waveEnemiesSpawned = 0;
        waveEnemiesDeath = 0;
        timeBetweenSpawn = Random.Range(survivalSetup.waves[actualWave].timeBetweenSpawn[0], survivalSetup.waves[actualWave].timeBetweenSpawn[1]);
        ////Debug.Log("Time Between Spawn" + timeBetweenSpawn);

        /*
        int enemiesPerSpawner = survivalSetup.waves[actualWave].enemiesCount / enemySpawners.Length;
        foreach(PrEnemySpawner spawner in enemySpawners)
        {
            spawner.SpawnerEnabled = true;
            spawner.SpawnStartDelay = 0.0f;
        }*/
    }

    void SetSurvivalStage(SurvivalStage sStage)
    {
        survivalStage = sStage;
    }


    void SetPlayersForCamera()
    {
        if (!useSplitScreen)
        {
            int playerCount = 0;

            for (int i = 0; i < 4; i++)
            {
                if (!playersSettings.playersInGame[i])
                {
                    //DoNothing
                }
                else
                {
                    if (playersInventorys[i].character.isDead)
                    {
                        //DoNothing
                    }
                    else
                    {
                        playerCount += 1;
                    }

                }
            }

            if (playerCount > 0)
            {
                playersForCamera = new GameObject[playerCount];

                int finalCount = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (!playersSettings.playersInGame[i])
                    {
                        //DoNothing
                    }
                    else
                    {
                        if (playersInventorys[i].character.isDead)
                        {
                            //DoNothing
                        }
                        else
                        {
                            playersForCamera[finalCount] = actualPlayerPrefabs[i];
                            playersControllers[i].useCameraPrefab = false;
                            playersControllers[i].m_Cam = actualCameraScript.transform.GetComponentInChildren<Camera>().transform;
                            finalCount += 1;
                        }
                    }

                }
            }
        }
        
        
    }

    void CreateCamera()
    {
        if (useSplitScreen)
        {
            //waits until players are created
        }
        else
        {
            if (!actualCameraScript)
            {
                GameObject actualCameraGO = Instantiate(playerSettings.multiplayerCamera, GetCameraCenter(), Quaternion.Euler(0, 45, 0)) as GameObject;
                actualCameraGO.transform.parent = this.transform;
                actualCameraGO.name = "MutiplayerCamera";
                actualCameraScript = actualCameraGO.GetComponent<PrTopDownMutiplayerCam>();
            }

            SetPlayersForCamera();
            actualCameraScript.MultiplayerCam(playersForCamera, actualPlayerCount);
            actualCameraScript.ResetWalls();
            actualCameraScript.targetHeightVariation = targetHeightVariation;
            actualCameraScript.targetHeightDistanceFactor = targetHeightDistanceFactor;
            actualCameraScript.targetHeightCorrection = targetHeightCorrection;
            actualCameraScript.useCameraColisions = useSingleScreenCameraLimits;
            
            // Minimap
            actualCameraScript.minimap = playerSettings.minimap;
            actualCameraScript.minimapZoom = playerSettings.minimapZoom;
            actualCameraScript.useMinimap = playerSettings.useMinimap;
            if (actualCameraScript.useMinimap && actualCameraScript.minimap)
            {
                actualCameraScript.CreateMinimap();
                for (int i = 0; i < playerCharacters.Length; i++)
                {
                    if (playerCharacters[i]) 
                        playerCharacters[i].SetUpMinimapIcons(playerSettings.playerIconScale, playerSettings.playerColor[i]);
                }
            }

        }
    }

    Vector3 GetCameraCenter()
    {
        int a = 0;
        Vector3 cameraCenter = Vector3.zero;
        //if (actualPlayerCount == 1)
       // {
        foreach (bool x in playersSettings.playersInGame)
        {
            if (x)
            {
                cameraCenter += actualPlayerPrefabs[a].transform.position;
            }
            a += 1;
        }
            
       /* }
        else if (actualPlayerCount == 2)
        {
            // cameraCenter = (actualPlayerPrefabs[0].transform.position + actualPlayerPrefabs[1].transform.position) / actualPlayerCount;
            foreach (bool x in playersSettings.playersInGame)
            {
                if (x)
                {
                    cameraCenter = actualPlayerPrefabs[a].transform.position;
                }
                a += 1;
            }
            
        }
        else if (actualPlayerCount == 3)
        {
            cameraCenter = (actualPlayerPrefabs[0].transform.position + actualPlayerPrefabs[1].transform.position + actualPlayerPrefabs[2].transform.position) / actualPlayerCount;
        }
        else if (actualPlayerCount == 4)
        {
            cameraCenter = (actualPlayerPrefabs[0].transform.position + actualPlayerPrefabs[1].transform.position + actualPlayerPrefabs[2].transform.position + actualPlayerPrefabs[3].transform.position) / actualPlayerCount;
        }*/
    
        return cameraCenter / actualPlayerCount; 
    }

    void DestroyPlayer(int playerNumber)
    {
        Destroy(actualPlayerPrefabs[playerNumber]);
    }

    int RandomNum(int lastRandNum)
    {
        int randNum = Random.Range(0, playersSpawnPos.Length);
        
        return randNum;
    }
    
    void SpawnPlayer(int playerNumber, bool randomPos)
    {
        if (playersSettings.playersInGame[playerNumber])
        {
            int posInt = playerNumber;

            if (randomPos)
            {
                posInt = RandomNum(posInt);
                int tries = 0;
                while (spawnPointFull[posInt] == true && tries < 12)
                {
                    posInt = RandomNum(posInt);
                    tries += 1;
                }

            }
            //set last position if using lives
            Vector3 finalSpawnPos = playersSpawnPos[posInt].position;
            if (useLives && playersDeathPos[playerNumber] != playersSpawnPos[playerNumber].position)
            {
            //    //Debug.Log("aaaa" + playersDeathPos[playerNumber]);
                finalSpawnPos = playersDeathPos[playerNumber];
            }

            ////Debug.Log(finalSpawnPos);
            //Instantiate player Prefab in Scene
            
            GameObject tempPlayer = PrUtils.InstantiateActor(playersPrefabs[playerNumber], finalSpawnPos, playersSpawnPos[posInt].rotation, "Player_" + playerNumber, this.transform);
            //GameObject tempPlayer = Instantiate(playersPrefabs[playerNumber], finalSpawnPos, playersSpawnPos[posInt].rotation) as GameObject;
            //tempPlayer.transform.parent = this.transform;
            actualPlayerPrefabs[playerNumber] = tempPlayer;
            playerCharacters[playerNumber] = tempPlayer.transform.GetComponentInChildren<PrCharacter>();
            playersControllers[playerNumber] = tempPlayer.transform.GetComponentInChildren<PrCharacterController>();
            playersInventorys[playerNumber] = tempPlayer.transform.GetComponentInChildren<PrCharacterInventory>();

            if (useSplitScreen)
                playersControllers[playerNumber].useCameraPrefab = true;

            playersControllers[playerNumber].playerNmb = playerNumber + 1;
            playersControllers[playerNumber].characterHUD.playerNmb = playerNumber + 1;

            if (mode != GameMode.SinglePlayer)
            {
                //set split screen var
                playersControllers[playerNumber].characterHUD.SetSplitScreen(useSplitScreen, actualPlayerCount);

            }
            playersControllers[playerNumber].characterHUD.InitializeHUD();
            //playersControllers[playerNumber].SavePlayerInfo();
            //playersInventorys[playerNumber].characterHUD.playerNmb = playerNumber + 1;

            //Sets player Team settings
            if (mode == GameMode.DeathMatch)
            {
                playerCharacters[playerNumber].team = playerNumber;
                playersControllers[playerNumber].JoystickEnabled = true;
               
            }
            else if (mode == GameMode.TeamDeathMatch)
            {
                if (playerNumber < 2)
                {
                    playerCharacters[playerNumber].team = 1;
                }
                else
                {
                    playerCharacters[playerNumber].team = 2;
                }
                playersControllers[playerNumber].JoystickEnabled = true;

            }
            //Set player Colors
            if (mode == GameMode.SinglePlayer)
            {
                playerCharacters[playerNumber].SetPlayerColors(0, playerNumber, playerSettings);
            }
            else if (mode == GameMode.DeathMatch)
            {
                playerCharacters[playerNumber].SetPlayerColors(1, playerNumber, playerSettings);
            }
            else if (mode == GameMode.Coop || mode == GameMode.Survival)
            {
                if (playerNumber > 0)
                    playersControllers[playerNumber].JoystickEnabled = true;
                playerCharacters[playerNumber].SetPlayerColors(2, playerNumber, playerSettings);
                coopPlayerStates[playerNumber] = "InGame";
            }
            else if (mode == GameMode.TeamDeathMatch)
            {
                playerCharacters[playerNumber].SetPlayerColors(3, playersInventorys[playerNumber].character.team - 1, playerSettings);
            }

            if (mode != GameMode.SinglePlayer)
            {
                
                if (useSplitScreen)
                {
                    ////Debug.Log("Split Screen Active");
                    //Get Player Camera
                    //playersControllers[playerNumber].useCameraPrefab = true;

                    //playersControllers[playerNumber].InitializeController();

                    if (playersControllers[playerNumber].CamScript)
                    {
                        playersControllers[playerNumber].CamScript.gameObject.SetActive(true);

                        Camera tempCam = playersControllers[playerNumber].CamScript.transform.GetComponentInChildren<Camera>();

                        if (actualPlayerCount == 1)
                        {
                            //DoNothing
                        }
                        else if (actualPlayerCount == 2)
                        {
                            if (playerNumber == 0)
                                SetCamSplitScreen(tempCam, 0, 0, 0.5f, 1);
                            else if (playerNumber == 1)
                                SetCamSplitScreen(tempCam, 0.5f, 0, 0.5f, 1);
                        }
                        else if (actualPlayerCount == 3)
                        {
                            if (playerNumber == 0)
                                SetCamSplitScreen(tempCam, 0.0f, 0.5f, 1f, 1f);
                            else if (playerNumber == 1)
                                SetCamSplitScreen(tempCam, 0.0f, 0, 0.5f, 0.5f);
                            else if (playerNumber == 2)
                                SetCamSplitScreen(tempCam, 0.5f, 0, 0.5f, 0.5f);
                        }
                        else if (actualPlayerCount == 4)
                        {
                            if (playerNumber == 0)
                                SetCamSplitScreen(tempCam, 0.0f, 0.5f, 0.5f, 0.5f);
                            else if (playerNumber == 1)
                                SetCamSplitScreen(tempCam, 0.5f, 0.5f, 0.5f, 0.5f);
                            else if (playerNumber == 2)
                                SetCamSplitScreen(tempCam, 0.0f, 0, 0.5f, 0.5f);
                            else if (playerNumber == 3)
                                SetCamSplitScreen(tempCam, 0.5f, 0, 0.5f, 0.5f);

                        }
                    }
                        
                    //SetCamSplitScreen()
                    // playersControllers[playerNumber].CamScript.transform.GetComponentInChildren<Camera>().rect.
                }
                else
                {
                    playersControllers[playerNumber].useCameraPrefab = false;
                    //playersControllers[playerNumber].m_Cam = actualCameraScript.transform.GetComponentInChildren<Camera>().transform;
                    if (playersControllers[playerNumber].CamScript != null)
                        playersControllers[playerNumber].CamScript.gameObject.SetActive(false);
                    playersInventorys[playerNumber].characterHUD.HUDDamageFullScreen.SetActive(false);

                }
            }
        }
        
        

    }

    void SetCamSplitScreen(Camera cam, float x, float y, float width, float height)
    {
        cam.rect = new Rect(x, y, width, height);
    }

    void ResetFragHUD()
    {
        
        int i = 1;

        foreach (GameObject text in fragCounter)
        {
            if (playersSettings.playersInGame[i-1])
            {
                playersFrags[i - 1] = 0;
                text.GetComponent<Text>().text = "P" + i.ToString() + " " + playersFrags[i - 1].ToString();
            }
           
        }

        i = 1;

        if (mode == GameMode.TeamDeathMatch)
        {
            foreach (GameObject text in teamfragCounter)
            {
                teamFrags[i - 1] = 0;
                text.GetComponent<Text>().text = teamFrags[i - 1].ToString();
            }
        }
        
    }

    void OrganizeFragHUD()
    {
        if (mode == GameMode.DeathMatch)
        {
            fragCounter[0].transform.parent.gameObject.SetActive(true);
            teamfragCounter[0].transform.parent.gameObject.SetActive(false);
            int i = 1;
            foreach (GameObject text in fragCounter)
            {
                if (actualPlayerCount == 1)
                {
                    //Debug.Log("Un Solo player");
                    text.GetComponent<Text>().text = "";
                }
                else
                {
                    if (playersSettings.playersInGame[i-1])
                    {
                        text.GetComponent<Text>().color = playerSettings.playerColor[i - 1];
                        text.GetComponent<Text>().text = "P" + i.ToString() + " " + playersFrags[i - 1].ToString() + "0";
                    }
                    else
                    {
                        text.GetComponent<Text>().text = "";
                    }
                    
                }

                i += 1;
            }
        }
        else if (mode == GameMode.TeamDeathMatch)
        {
            fragCounter[0].transform.parent.gameObject.SetActive(false);
            teamfragCounter[0].transform.parent.gameObject.SetActive(true);
            
        }


    }

    void UpdateFragHUD()
    {
        if (mode == GameMode.DeathMatch)
        {
            int i = 1;
            foreach (GameObject text in fragCounter)
            {
                if (actualPlayerCount >= i)
                {
                    text.GetComponent<Text>().text = "P" + i.ToString() + " " + playersFrags[i - 1].ToString("00");

                    i += 1;
                }

            }
        }
        else if (mode == GameMode.TeamDeathMatch)
        {
            teamfragCounter[0].GetComponent<Text>().text = teamFrags[0].ToString("00");
            teamfragCounter[1].GetComponent<Text>().text = teamFrags[1].ToString("00");

        }
    }

    public void NewFrag(int team)
    {
        if (stage == GameStage.inGame)
        {
            if (mode == GameMode.DeathMatch )
            {
                if (team < 0)
                {
                    team = team * -1;
                    team -= 1;
                    playersFrags[team] -= 1;

                    //Debug.Log("EnemyTeam Game" + team);
                }
                else
                {
                    playersFrags[team] += 1;
                }

                UpdateFragHUD();

                if (playersFrags[team] >= fragsToWin)
                {
                    SetPlayerWin(team);
                }


            }
            else if (mode == GameMode.TeamDeathMatch)
            {
                teamFrags[team - 1] += 1;
                //Debug.Log("Frag By " + (team - 1));

                UpdateFragHUD();

                if (teamFrags[team - 1] >= fragsToWin)
                {
                    SetTeamWin(team - 1);
                }
            }
        }

    }

    public void PlayerSpawned()
    {
        SetPlayersForCamera();
        if (actualCameraScript)
            actualCameraScript.MultiplayerCam(playersForCamera, actualPlayerCount);
    }

    public void PlayerDied(int playerNumber)
    {
        //Debug.Log("Player Died" + " " + playerNumber + " " + useLives);
        if (mode == GameMode.DeathMatch || mode == GameMode.TeamDeathMatch || mode == GameMode.Survival)
        {
            SetPlayersForCamera();
            if (!useSplitScreen)
            {
                actualCameraScript.MultiplayerCam(playersForCamera, actualPlayerCount);

            }

            if (GameOverUI)
                GameOver();
        }
        else if (mode == GameMode.SinglePlayer)
        {
            //Debug.Log("Getting singleplayer" + " " + useLives + " " + livesPerPlayer[playerNumber - 1] + " " + playerSettings.livesPerPlayer);
            /*if (useLives == true)
            {
                if (livesPerPlayer[playerNumber - 1] > 1)
                {
                    livesPerPlayer[playerNumber - 1] -= 1;
                    coopPlayerStates[playerNumber - 1] = "Respawning";
                }
            }
            else
            {
                coopPlayerStates[playerNumber - 1] = "Dead";
            }*/
            if (useLives && livesPerPlayer[playerNumber - 1] > 0 && playerSettings.livesPerPlayer >= 0)
            {
                GameOver();
                //Debug.Log("GameOver" + " " + livesPerPlayer[playerNumber - 1]);
            }
            else
            {
                SceneManager.LoadScene(0, LoadSceneMode.Single);
            }
        }
        else if (mode == GameMode.Coop)
        {
            SetPlayersForCamera();
            if (!useSplitScreen)
            {
                actualCameraScript.MultiplayerCam(playersForCamera, actualPlayerCount);

            }
            if (useLives == true)
            {
                if ( livesPerPlayer[playerNumber - 1] > 1)
                {
                    livesPerPlayer[playerNumber - 1] -= 1;
                    coopPlayerStates[playerNumber - 1] = "Respawning";
                    respawnTimers[playerNumber - 1] = playerSettings.respawnAfterDeathTimer;
                }
                else
                {
                    respawnTimers[playerNumber - 1] = 0.0f;
                    coopPlayerStates[playerNumber - 1] = "Dead";
                }
            }
            else
            {
                coopPlayerStates[playerNumber - 1] = "Dead";
            }


            bool gameOverTest = true;
            foreach (string pS in coopPlayerStates)
            {
                if (pS == "InGame" || pS == "Respawning")
                {
                    gameOverTest = false;
                    //Do Nothing 
                }
            }
            if (gameOverTest)
                GameOver();
        }
        UpdateLastPlayerPos(playerNumber - 1);
        //Do whatever you need
    }


    public void GameOver()
    {
        if (mode == GameMode.Survival )
            stage = GameStage.EndedMatch;

        if (mode == GameMode.SinglePlayer || mode == GameMode.Coop)
        {
            objectivesManager.enabled = false;
        }

        if (GameOverUI && actualGameOverUI != null)
            actualGameOverUI.SetActive(true);

        else if (GameOverUI)
        {
            actualGameOverUI = Instantiate(GameOverUI, Vector3.zero, Quaternion.identity);
        }
        
        for (int i = 0; i == 3; i++)
        {
            livesPerPlayer[i] = livesPerPlayer[i] - 1;
        }
        //Debug.Log("You Lose");

        CallEnemiesToStop();
        int x = 0;
        foreach (PrCharacterController p in playersControllers)
        {
            //Debug.Log(p);

            if (p != null)
            {
                //Debug.Log(p + "is NOT NULL");
                PrPlayerInfo.player1.loadPrevSettings = true;
                ////Debug.Log("Actual Objective " + objectivesManager.actualObjective);
                //PrPlayerInfo.player1.lastPlayerPosition = p.transform.position;
                if (objectivesManager)
                    PrPlayerInfo.player1.lastObjectiveActive = objectivesManager.actualObjective - 1 ;
               /* if (useLives)
                {
                    //Debug.Log("USE LIVES " + PrPlayerInfo.player1.lives[0]);
                    /*for (int i = 0; i == 3; i++)
                    {
                        PrPlayerInfo.player1.lives[i] -= 1;
                        
                        //PrPlayerInfo.player1.lives[i] = livesPerPlayer[i] -= 1;
                    }
                }*/
                p.StopMoving("GameOver");
            }
            x++;
        }
        //stage = GameStage.levelComplete;
    }

    public void SetTeamWin(int teamToWin)
    {
        //Debug.Log("Wining Team " + teamToWin);
        stage = GameStage.EndedMatch;

        playerWinsText.SetActive(true);

        playerWinsText.GetComponent<Text>().color = playerSettings.teamColor[teamToWin];

        string finalText = "";

        if (teamToWin == 0)
        {
            finalText = "RED TEAM WINS";
        }
        else
        {
            finalText = "BLUE TEAM WINS";
        }
        
        playerWinsText.GetComponent<Text>().text = finalText;
    }

    public void SetPlayerWin(int playerToWin)
    {
        stage = GameStage.EndedMatch;

        playerWinsText.SetActive(true);

        playerWinsText.GetComponent<Text>().color = playerSettings.playerColor[playerToWin];

        int finalPlayer = playerToWin + 1;
        string finalText = "Player " + finalPlayer.ToString() + " Wins";

        playerWinsText.GetComponent<Text>().text = finalText;
    }

    void OnDrawGizmos()
    {
        if (playersSettings && targetArrow)
        {
            int n = 0;
            foreach (Transform spawnPos in playersSpawnPos)
            {
                Gizmos.color = playersSettings.playerColor[n] * 2;
                if (spawnPos.gameObject.activeInHierarchy)
                {
                    Gizmos.DrawMesh(targetArrow, spawnPos.position + Vector3.up, Quaternion.Euler(0, 10, 0), Vector3.one);
                    n += 1;

                    // Gizmos.color = Color.white;
                    Gizmos.DrawMesh(areaMesh, spawnPos.position, Quaternion.Euler(0, 0, 0), Vector3.one);
                }
               

            }
        }

    }
}
