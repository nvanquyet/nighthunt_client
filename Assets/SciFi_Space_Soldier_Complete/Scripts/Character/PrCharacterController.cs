using System.Collections.Generic;
using UnityEngine;

public class PrCharacterController : MonoBehaviour
{
    [HideInInspector]
    public GameObject controlledCharacter;
    [HideInInspector]
    public PrCharacter character;
    [HideInInspector]
    public PrCharacterInventory characterInventory;
    [HideInInspector]
    public PrActorHUD characterHUD;
    [HideInInspector]
    public PrActorUtils characterUtils;

    [Header("Multiplayer")]
    public int playerNmb = 1;
    public PrPlayerSettings playerSettings;
    public Transform playerExtras;
    public GameObject PlayerSelection;
    [HideInInspector]
    public Renderer currentPlayerSelection;
    //Inputs
    //[HideInInspector]
    public string[] playerCtrlMap = {"Horizontal", "Vertical", "LookX", "LookY","FireTrigger", "Reload",
        "EquipWeapon", "Sprint", "Aim", "AimTrigger", "Roll", "Use", "Crouch", "ChangeWeapon", "Throw"  ,"Fire", "Mouse ScrollWheel", "Melee"};

    [Header("Movement")]

    public bool physicsMovement = true;

    [SerializeField]
    float m_JumpPower = 12f;
    [Range(1f, 4f)] [SerializeField] float m_GravityMultiplier = 2f;
    [SerializeField] float m_MoveSpeedMultiplier = 1f;
    [HideInInspector]
    public float m_MoveSpeedSpecialModifier = 1f;
    [SerializeField] float m_AnimSpeedMultiplier = 1f;
    private float m_GroundCheckDistance = 0.25f;

    public bool useRootMotion = true;

    public float PlayerRunSpeed = 1f;
    public float PlayerAimSpeed = 1f;
    public float PlayerSprintSpeed = 1f;
    public float PlayerCrouchSpeed = 0.75f;

    public float RunRotationSpeed = 100f;
    public float AimingRotationSpeed = 25f;

    public float AnimatorRunDampValue = 0.25f;
    public float AnimatorSprintDampValue = 0.2f;
    public float AnimatorAimingDampValue = 0.1f;

    Rigidbody m_Rigidbody;
    Animator charAnimator;

    bool m_IsGrounded;
    float m_OrigGroundCheckDistance;
    const float k_Half = 0.5f;
    float m_TurnAmount;
    float m_ForwardAmount;
    float m_CapsuleHeight;
    Vector3 m_CapsuleCenter;
    CapsuleCollider m_Capsule;
    private Vector3 colExtents;
    bool m_Crouching;
    private bool crouch = false;

    [HideInInspector] public bool b_CanRotate = true;
    private bool m_Jump;
    private float lastJump = 0.0f;
    [HideInInspector] public bool b_canJump = true;
    [HideInInspector]
    public bool Jumping = false;
    [HideInInspector] public bool Sprinting = false;
    [HideInInspector]
    public bool Rolling = false;

    public float RollStaminaUse = 0.5f;

    public enum EAction { Jump, Roll }
    public EAction evadeAction = EAction.Jump;

    [HideInInspector] public bool m_CanMove = true;

    [Header("Aiming")]
    public GameObject AimTargetVisual;
    public bool use2DAimTarget = false;
    public Transform AimFinalPos;
    [HideInInspector] public bool Aiming = false;
    public bool alwaysAim = false;
    public bool WASDOldStyleAim = false;
    public bool allowVerticalAiming = false;
    private float aimTargetDistanceToPlayer = 0.0f;
    public bool useShootingKickBack = true;
    [HideInInspector]
    public Vector3 m_Move;					  // the world-relative desired move direction, calculated from the camForward and user input.
    private Vector3 smoothMove;

    [Header("Camera")]
    [HideInInspector]
    Pr3rdPersonCamera cameraComponent;
    [HideInInspector]
    public bool useCameraPrefab = true;
    [HideInInspector]
    public bool useThirdPersonCamera = false;
    [HideInInspector]
    public Transform actualCameraPrefab;
    [HideInInspector]
    public Transform cameraTarget;
    public float cameraRotation = 45.0f;
    public float cameraRotationSpeed = 1.0f;
    //[HideInInspector]
    public PrTopDownCamera CamScript;
    [HideInInspector]
    public Transform m_Cam;                  // A reference to the main camera in the scenes transform
    
    //Use Vars
    [Header("Use Vars")]
    public float UseAngle = 75.0f;
    public GameObject UsableObject;
    public bool UsingObject = false;
    public bool AutoPickupItems = false;

    [Header("Vehicles use")]
    public bool insideVehicle = false;
    public PrVehicle vehicleToDrive;
    public float timeToNextVehicle = 0.0f;
    private bool wasArmed = false;

    [Header("Joystick / Keyboard")]
    public bool JoystickEnabled = true;
    public bool useDirectSwitchKeys = false;
    public string[] weaponDirectSwitchKeys;

    // Virtual Joystick for Mobile variables. 
    //public VariableJoystick variableJoystick;
    [HideInInspector]
    public GameObject JoystickTarget;
    [HideInInspector]
    public GameObject JoystickLookRot;

    //Look where to move this

    [Header("VFX")]
    public GameObject RollVFX;
    [HideInInspector]
    public List<GameObject> friends;

    GameObject tempCam;
    //int lives = 0;

    // Start is called before the first frame update
    void Start()
    {
        SetMultiplayerSettings();

        if (playerSettings)
        {
            playerCtrlMap = PrUtils.SetMultiplayerInputs(playerNmb, playerSettings, playerCtrlMap);

            if (playerNmb > 1)
            {
                JoystickEnabled = true;
            }
        }


        if (JoystickEnabled)
            VisualAimVisible(true);
        else
            VisualAimVisible(false);

        if (useThirdPersonCamera)
            VisualAimVisible(true);
        /*// Virtual Joystick for Mobile variables. 
        if (variableJoystick)
            JoystickEnabled = true;*/

        friends = new List<GameObject>(GameObject.FindGameObjectsWithTag("AIPlayer"));
        if (friends.Count != 0)
        {
            foreach (GameObject f in friends)
            {
                f.transform.parent.GetComponent<PrAIController>().FindPlayers();
            }
        }

    }

    public void NewMinimapCreated(GameObject minimapGameObject)
    {
        characterHUD.minimap = minimapGameObject.GetComponent<PrMinimap>();
        characterHUD.ResizeMiminaps();
    }

    public void InitializeController()
    {
        
        characterInventory = GetComponent<PrCharacterInventory>();
        character = GetComponent<PrCharacter>();
        characterHUD = character.actualHUD;
        characterUtils = character.actualCharUtils;

        if (PlayerSelection)
        {
            if (!playerExtras)
            {
                playerExtras = characterUtils.transform;
            }
            currentPlayerSelection = PrUtils.InstantiateActor(PlayerSelection, characterUtils.transform.position + (Vector3.one * 0.1f),PlayerSelection.transform.rotation, "PlayerSelection" ,  playerExtras.transform).GetComponent<Renderer>();
        }

        useThirdPersonCamera = false;
        // get the transform of the main camera
        if (useCameraPrefab)
        {
            Quaternion cameraRot = Quaternion.Euler(0.0f, cameraRotation, 0.0f);
            
            if (playerSettings.singlePlayerThirdPerson == true)
            {
                tempCam = Instantiate(playerSettings.thirdPersonCamera, transform.position, cameraRot, this.transform) as GameObject;
                useThirdPersonCamera = true;
                if (characterHUD.weaponAimTarget)
                {
                    characterHUD.weaponAimTarget.gameObject.SetActive(true);
                }

            }
            else
            {
                tempCam = Instantiate(playerSettings.singlePlayerCamera, transform.position, cameraRot, this.transform) as GameObject;
                useThirdPersonCamera = false;
                if (characterHUD.weaponAimTarget)
                {
                    characterHUD.weaponAimTarget.gameObject.SetActive(false);
                }

            }

            actualCameraPrefab = tempCam.transform;
            tempCam.name = "PlayerCamera_GO";
            CamScript = tempCam.GetComponent<PrTopDownCamera>();
            CamScript.TargetToFollow = cameraTarget;

            if (playerSettings.useMinimap && playerSettings.minimap)
            {
                CamScript.minimap = playerSettings.minimap;
                CamScript.useMinimap = true;
                CamScript.minimapZoom = playerSettings.minimapZoom;
            }

            m_Cam = tempCam.transform.GetComponentInChildren<Camera>().transform;
            foreach (GameObject a in characterInventory.Weapon)
            {
                if ( a != null)
                {
                    a.GetComponent<PrWeapon>().playerCamera = CamScript;
                }
               
            }
            if (useThirdPersonCamera)
            {
                cameraComponent = actualCameraPrefab.GetComponent<Pr3rdPersonCamera>();
                cameraComponent.playerController = this;
                characterInventory.grenadeThrow = PrCharacterInventory.ThrowWay.AimPosition;
            }
        }
        else
        {
            Debug.LogWarning(
                "Warning: no main camera found. character needs a Camera tagged \"MainCamera\", for camera-relative controls.");
            // we use self-relative controls in this case, which probably isn't what the user wants, but hey, we warned them!
        }

        Cursor.visible = false;

        JoystickTarget = new GameObject();
        JoystickTarget.name = "JoystickTarget";
        JoystickTarget.transform.position = controlledCharacter.transform.position;
        JoystickTarget.transform.parent = controlledCharacter.transform.parent;

        JoystickLookRot = new GameObject();
        JoystickLookRot.name = "JoystickLookRotation";
        JoystickLookRot.transform.position = controlledCharacter.transform.position;
        JoystickLookRot.transform.parent = controlledCharacter.transform;

        charAnimator = controlledCharacter.GetComponent<Animator>();
        m_Rigidbody = controlledCharacter.GetComponent<Rigidbody>();
        m_Capsule = controlledCharacter.GetComponent<CapsuleCollider>();
        colExtents = controlledCharacter.GetComponent<Collider>().bounds.extents;
        m_CapsuleHeight = m_Capsule.height;
        m_CapsuleCenter = m_Capsule.center;

        m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        m_OrigGroundCheckDistance = m_GroundCheckDistance;

        m_Rigidbody.isKinematic = false;
        m_Capsule.enabled = true;


        if (WASDOldStyleAim)
        {
            AimTargetVisual.transform.SetParent(controlledCharacter.transform);
            AimTargetVisual.SetActive(false);
        }

        //lives = playerSettings.livesPerPlayer;

        if (playerSettings.TypeSelected == PrPlayerSettings.GameMode.SinglePlayer || playerSettings.TypeSelected == PrPlayerSettings.GameMode.Cooperative)
        {
            if (playerNmb == 1 && GameObject.Find("playerInfo_" + playerNmb) == null)
            {
                CreatePlayerInfo();
            }
            if (playerNmb > 1 && GameObject.Find("playerInfo_1") != null)
            {
                SavePlayerInfo(false);
            }
        }

        NotifyAI();

        //Set Aim Target 2D 
        if (use2DAimTarget)
        {
            Renderer Target3D = AimTargetVisual.transform.Find("AimTargetMesh").GetComponent<Renderer>();
            Target3D.enabled = false;
            Renderer Target2D = AimFinalPos.Find("AimTarget2D").GetComponent<Renderer>();
            Target2D.enabled = true;
        }
        else
        {
            Renderer Target3D = AimTargetVisual.transform.Find("AimTargetMesh").GetComponent<Renderer>();
            Target3D.enabled = true;
            Renderer Target2D = AimFinalPos.Find("AimTarget2D").GetComponent<Renderer>();
            Target2D.enabled = false;
        }

        // Minimap setup for player
        if (playerSettings && playerSettings.useMinimap)
        {
            if (character.currentMinimapIcon == null)
            {
                if (playerSettings.minimapIcon)
                {
                    GameObject temp = Instantiate(playerSettings.minimapIcon);
                    temp.transform.SetParent(transform);
                    // Set postion to be OVER enemies
                    temp.transform.localPosition = Vector3.zero + new Vector3(0,1,0);
                    character.currentMinimapIcon = temp.GetComponent<SpriteRenderer>();
                }
            }

            character.SetUpMinimapIcons(playerSettings.playerIconScale, playerSettings.playerColor[playerNmb -1]);
        }
    }

    public void NotifyAI()
    {
        GameObject[] AIs = GameObject.FindGameObjectsWithTag("Enemy");
        if (AIs.Length > 0)
        {
            foreach (GameObject AI in AIs)
            {
                //Debug.Log(AI.name);
                if (AI.transform.parent.GetComponent<PrAIController>())
                    AI.transform.parent.GetComponent<PrAIController>().FindPlayers();
            }
        }

        GameObject[] AI2s = GameObject.FindGameObjectsWithTag("AIPlayer");
        if (AI2s.Length > 0)
        {
            foreach (GameObject AI in AI2s)
            {
                //Debug.Log(AI.name);
                if (AI.transform.parent.GetComponent<PrAIController>())
                    AI.transform.parent.GetComponent<PrAIController>().FindPlayers();
            }
        }
    }

    public void SetMultiplayerSettings()
    {
        if (currentPlayerSelection)
        {
            currentPlayerSelection.material.SetColor("_TintColor", playerSettings.playerColor[playerNmb - 1]);
        }
    }

    public void LoadPlayerInfo()
    {
        //Debug.Log("player Info found - Loading Info");
        if (PrPlayerInfo.player1)
        {
            JoystickEnabled = PrPlayerInfo.player1.usingJoystick[playerNmb - 1];
            character.Health = PrPlayerInfo.player1.health[playerNmb - 1];
            character.ActualHealth = PrPlayerInfo.player1.actualHealth[playerNmb - 1];
            characterInventory.currentMoney = PrPlayerInfo.player1.money[playerNmb - 1];
            characterInventory.grenadesCount = PrPlayerInfo.player1.grenades[playerNmb - 1];
            //lives = PrPlayerInfo.player1.lives[playerNmb - 1];

            if (playerNmb == 1)
            {
                for (int i = 0; i < PrPlayerInfo.player1.weaponsP1.Length; i++)
                {
                    characterInventory.InitialWeapons[i] = characterInventory.WeaponListObject.weapons[PrPlayerInfo.player1.weaponsP1[i]].GetComponent<PrWeapon>();
                }
            }
            else if (playerNmb == 2)
            {
                for (int i = 0; i < PrPlayerInfo.player1.weaponsP2.Length; i++)
                {
                    characterInventory.InitialWeapons[i] = characterInventory.WeaponListObject.weapons[PrPlayerInfo.player1.weaponsP2[i]].GetComponent<PrWeapon>();
                }
            }
            else if (playerNmb == 3)
            {
                for (int i = 0; i < PrPlayerInfo.player1.weaponsP3.Length; i++)
                {
                    characterInventory.InitialWeapons[i] = characterInventory.WeaponListObject.weapons[PrPlayerInfo.player1.weaponsP3[i]].GetComponent<PrWeapon>();
                }
            }
            else if (playerNmb == 4)
            {
                for (int i = 0; i < PrPlayerInfo.player1.weaponsP4.Length; i++)
                {
                    characterInventory.InitialWeapons[i] = characterInventory.WeaponListObject.weapons[PrPlayerInfo.player1.weaponsP4[i]].GetComponent<PrWeapon>();
                }
            }
        }
    }

    public void SavePlayerInfo(bool deathSave)
    {
        //Debug.Log("Saving Player Info:" + playerNmb);
        if (PrPlayerInfo.player1)
        {
            PrPlayerInfo playerI = PrPlayerInfo.player1.GetComponent<PrPlayerInfo>();
            //Debug.Log(playerI.name);
            playerI.playerNumber[playerNmb - 1] = playerNmb;
            playerI.usingJoystick[playerNmb - 1] = JoystickEnabled;
            playerI.playerName[playerNmb - 1] = characterInventory.name;
            playerI.health[playerNmb - 1] = character.Health;
            playerI.money[playerNmb - 1] = characterInventory.currentMoney;
            playerI.actualHealth[playerNmb - 1] = character.ActualHealth;
            playerI.maxWeaponCount[playerNmb - 1] = characterInventory.playerWeaponLimit;
            if (deathSave)
                playerI.lives[playerNmb - 1]--;

            if (playerNmb == 1)
            {
                playerI.weaponsP1 = new int[characterInventory.playerWeaponLimit];
                playerI.weaponsAmmoP1 = new int[characterInventory.playerWeaponLimit];
                playerI.weaponsClipsP1 = new int[characterInventory.playerWeaponLimit];
            }
            else if (playerNmb == 2)
            {
                playerI.weaponsP2 = new int[characterInventory.playerWeaponLimit];
                playerI.weaponsAmmoP2 = new int[characterInventory.playerWeaponLimit];
                playerI.weaponsClipsP2 = new int[characterInventory.playerWeaponLimit];
            }
            else if (playerNmb == 3)
            {
                playerI.weaponsP3 = new int[characterInventory.playerWeaponLimit];
                playerI.weaponsAmmoP3 = new int[characterInventory.playerWeaponLimit];
                playerI.weaponsClipsP3 = new int[characterInventory.playerWeaponLimit];
            }
            else if (playerNmb == 4)
            {
                playerI.weaponsP4 = new int[characterInventory.playerWeaponLimit];
                playerI.weaponsAmmoP4 = new int[characterInventory.playerWeaponLimit];
                playerI.weaponsClipsP4 = new int[characterInventory.playerWeaponLimit];
            }

            playerI.grenades[playerNmb - 1] = characterInventory.grenadesCount;

            for (int i = 0; i < characterInventory.playerWeaponLimit; i++)
            {
                if (playerNmb == 1)
                {
                    ////Debug.Log("Weapon " + i + " is " + characterInventory.Weapon[i] + " And the Name is " + characterInventory.Weapon[i].GetComponent<PrWeapon>().WeaponName + " And the bullets are " + characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets);
                    playerI.weaponsP1[i] = characterInventory.actualWeaponTypes[i];
                    playerI.weaponsAmmoP1[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets;
                    playerI.weaponsClipsP1[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualClips;
                }
                else if (playerNmb == 2)
                {
                    ////Debug.Log("Weapon " + i + " is " + characterInventory.Weapon[i] + " And the Name is " + characterInventory.Weapon[i].GetComponent<PrWeapon>().WeaponName + " And the bullets are " + characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets);
                    playerI.weaponsP2[i] = characterInventory.actualWeaponTypes[i];
                    playerI.weaponsAmmoP2[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets;
                    playerI.weaponsClipsP2[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualClips;
                }
                else if (playerNmb == 3)
                {
                    ////Debug.Log("Weapon " + i + " is " + characterInventory.Weapon[i] + " And the Name is " + characterInventory.Weapon[i].GetComponent<PrWeapon>().WeaponName + " And the bullets are " + characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets);
                    playerI.weaponsP3[i] = characterInventory.actualWeaponTypes[i];
                    playerI.weaponsAmmoP3[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets;
                    playerI.weaponsClipsP3[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualClips;
                }
                else if (playerNmb == 4)
                {
                    ////Debug.Log("Weapon " + i + " is " + characterInventory.Weapon[i] + " And the Name is " + characterInventory.Weapon[i].GetComponent<PrWeapon>().WeaponName + " And the bullets are " + characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets);
                    playerI.weaponsP4[i] = characterInventory.actualWeaponTypes[i];
                    playerI.weaponsAmmoP4[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets;
                    playerI.weaponsClipsP4[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualClips;
                }
            }
        }
        
    }


    public void CreatePlayerInfo()
    {
        //Create Player info to be able to save player stats during gameplay

        //Debug.Log("player Info NOT found - Saving Info");

        GameObject playerInfo = new GameObject("playerInfo_" + playerNmb);
        playerInfo.AddComponent<PrPlayerInfo>();
        PrPlayerInfo playerI = playerInfo.GetComponent<PrPlayerInfo>();

        //Debug.Log(playerI.name + "_" + playerNmb);

        playerI.playerNumber[playerNmb - 1] = playerNmb;
        playerI.usingJoystick[playerNmb - 1] = JoystickEnabled;
        playerI.playerName[playerNmb - 1] = characterInventory.name;
        playerI.health[playerNmb - 1] = character.Health;
        playerI.actualHealth[playerNmb - 1] = character.ActualHealth;
        playerI.money[playerNmb - 1] = characterInventory.currentMoney;
        playerI.maxWeaponCount[playerNmb - 1] = characterInventory.playerWeaponLimit;
        playerI.lives[playerNmb - 1] = playerSettings.livesPerPlayer; 

        if (playerNmb == 1)
        {
            playerI.weaponsP1 = new int[characterInventory.playerWeaponLimit];
            playerI.weaponsAmmoP1 = new int[characterInventory.playerWeaponLimit];
            playerI.weaponsClipsP1 = new int[characterInventory.playerWeaponLimit];
        }
        else if (playerNmb == 2)
        {
            playerI.weaponsP2 = new int[characterInventory.playerWeaponLimit];
            playerI.weaponsAmmoP2 = new int[characterInventory.playerWeaponLimit];
            playerI.weaponsClipsP2 = new int[characterInventory.playerWeaponLimit];
        }
        else if (playerNmb == 3)
        {
            playerI.weaponsP3 = new int[characterInventory.playerWeaponLimit];
            playerI.weaponsAmmoP3 = new int[characterInventory.playerWeaponLimit];
            playerI.weaponsClipsP3 = new int[characterInventory.playerWeaponLimit];
        }
        else if (playerNmb == 4)
        {
            playerI.weaponsP4 = new int[characterInventory.playerWeaponLimit];
            playerI.weaponsAmmoP4 = new int[characterInventory.playerWeaponLimit];
            playerI.weaponsClipsP4 = new int[characterInventory.playerWeaponLimit];
        }

        for (int i = 0; i < characterInventory.playerWeaponLimit; i++)
        {
            if (characterInventory.Weapon[i] != null)
            {
                if (playerNmb == 1)
                {
                    ////Debug.Log("Weapon " + i + " is " + characterInventory.Weapon[i] + " And the Name is " + characterInventory.Weapon[i].GetComponent<PrWeapon>().WeaponName + " And the bullets are " + characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets);
                    playerI.weaponsP1[i] = characterInventory.actualWeaponTypes[i];
                    playerI.weaponsAmmoP1[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets;
                    playerI.weaponsClipsP1[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualClips;

                }
                else if (playerNmb == 2)
                {
                    ////Debug.Log("Weapon " + i + " is " + characterInventory.Weapon[i] + " And the Name is " + characterInventory.Weapon[i].GetComponent<PrWeapon>().WeaponName + " And the bullets are " + characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets);
                    playerI.weaponsP2[i] = characterInventory.actualWeaponTypes[i];
                    playerI.weaponsAmmoP2[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets;
                    playerI.weaponsClipsP2[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualClips;
                }
                else if (playerNmb == 3)
                {
                    ////Debug.Log("Weapon " + i + " is " + characterInventory.Weapon[i] + " And the Name is " + characterInventory.Weapon[i].GetComponent<PrWeapon>().WeaponName + " And the bullets are " + characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets);
                    playerI.weaponsP3[i] = characterInventory.actualWeaponTypes[i];
                    playerI.weaponsAmmoP3[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets;
                    playerI.weaponsClipsP3[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualClips;
                }
                else if (playerNmb == 4)
                {
                    ////Debug.Log("Weapon " + i + " is " + characterInventory.Weapon[i] + " And the Name is " + characterInventory.Weapon[i].GetComponent<PrWeapon>().WeaponName + " And the bullets are " + characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets);
                    playerI.weaponsP4[i] = characterInventory.actualWeaponTypes[i];
                    playerI.weaponsAmmoP4[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualBullets;
                    playerI.weaponsClipsP4[i] = characterInventory.Weapon[i].GetComponent<PrWeapon>().ActualClips;
                }
            }
            
        }

    }
    /*
    public void SetLives(int newLives)
    {
        lives = newLives;
    }*/

    // Update is called once per frame
    void Update()
    {
        if (!character.isDead && m_CanMove)
        {
            UpdateInputs();

            if (Rolling && !Jumping)
                characterUtils.useRootMotion = Rolling;

            if (useCameraPrefab)
                UpdateCamera();

        }

        else
        {
            UpdateUseObject();
            m_ForwardAmount = 0.0f;
            m_TurnAmount = 0.0f;
            Aiming = false;
            UpdateAnimator(Vector3.zero);
        }
    }

    void UpdateCamera()
    {
        if (Input.GetKeyUp(KeyCode.O))
        {
            cameraRotation += 45.0f;
        }

        actualCameraPrefab.localRotation = Quaternion.Lerp(actualCameraPrefab.transform.localRotation, Quaternion.Euler(0.0f, cameraRotation, 0.0f), Time.deltaTime * cameraRotationSpeed);
    }

    void UpdateInputs()
    {
        if (Input.GetKeyDown(KeyCode.K) && playerNmb <= 1)
        {
            if (JoystickEnabled)
                JoystickEnabled = false;
            else
                JoystickEnabled = true;

            if (!useThirdPersonCamera)
                VisualAimVisible(JoystickEnabled);
            else
                VisualAimVisible(true); 
        }

        if (!WASDOldStyleAim && !JoystickEnabled)
            MouseTargetPos();

        //Crouch
        if (Input.GetKey(KeyCode.LeftControl) && playerNmb <= 1 || Input.GetButton(playerCtrlMap[12]))
        {
            //print(playerCtrlMap[13]);
            crouch = true;

        }
        else
        {
            crouch = false;
        }

        float h = Input.GetAxisRaw(playerCtrlMap[0]);
        float v = Input.GetAxisRaw(playerCtrlMap[1]);

        if (JoystickEnabled)
        {
            h = Input.GetAxisRaw(playerCtrlMap[0]);
            v = Input.GetAxisRaw(playerCtrlMap[1]);
        }

        // Virtual Joystick for Mobile variables. 
        /*if (variableJoystick)
        {
            h = variableJoystick.Horizontal;
            v = variableJoystick.Vertical;
        }*/

        if (crouch && Aiming)
        {
            h = 0;
            v = 0;
        }

        //Roll

        if (Input.GetButton(playerCtrlMap[10]) && !Rolling && !UsingObject && character.ActualStamina > RollStaminaUse)
        {
            if (evadeAction == EAction.Roll)
            {
                Rolling = true;

                characterInventory.actualWeapon.LaserSight.enabled = false;
                characterInventory.actualWeapon.CancelReload();
                character.ActualStamina -= RollStaminaUse;
                charAnimator.SetTrigger("Roll");
                if (RollVFX)
                    Instantiate(RollVFX, controlledCharacter.transform.position, Quaternion.identity);
            }
        }

        //Jump
        if (Input.GetButton(playerCtrlMap[10]) && !Rolling && !m_Jump && !UsingObject && !crouch && Time.time >= lastJump + 0.2f && m_IsGrounded && b_canJump)
        {

            if (evadeAction == EAction.Jump && !charAnimator.GetCurrentAnimatorStateInfo(0).IsName("JumpEnd"))
            {

                lastJump = Time.time;
                //Rolling = true;
                m_Jump = true;
                characterInventory.actualWeapon.LaserSight.enabled = false;
                characterInventory.actualWeapon.CancelReload();
                charAnimator.SetTrigger("Jump");
                if (RollVFX)
                    Instantiate(RollVFX, controlledCharacter.transform.position, Quaternion.identity);

            }
        }

        // Equip Weapon
        if (Input.GetButtonUp(playerCtrlMap[6]) && Aiming == false && Sprinting == false && Rolling == false && Jumping == false && characterInventory.isThrowing == false)
        {
            characterInventory.actualWeapon.CancelReload();

            if (characterInventory.Armed)
                characterInventory.Armed = false;
            else
                characterInventory.Armed = true;
            characterInventory.EquipWeapon(characterInventory.Armed);
        }
        // Melee while ranged armed
        if (Input.GetButton(playerCtrlMap[17]))
        {
            if (characterInventory.meleeActive && characterInventory.meleeWeapon)
            {
                if (characterInventory.isThrowing == false && Rolling == false && Jumping == false && Sprinting == false && characterInventory.Armed)
                {
                    if (characterInventory.CanShoot && characterInventory.Weapon[characterInventory.ActiveWeapon] != null && Time.time >= (characterInventory.LastFireTimer + characterInventory.meleeRate))
                    {
                        if (characterInventory.actualWeapon.Type == global::PrWeapon.WT.Melee)
                        {
                            //Debug.Log("Weapon already melee");
                        }
                        else
                        {
                            //Debug.Log("Melee Attack");
                            characterInventory.actualWeapon.LaserSight.enabled = false;
                            Rolling = true;
                            Aiming = false;
                            characterInventory.LastFireTimer = Time.time;
                            charAnimator.SetTrigger("AttackMelee");
                            charAnimator.SetInteger("MeleeType", -1);
                            characterUtils.useRootMotion = true;
                            CantRotate();
                            //EnableArmIK(false);
                            //waitingArmIK = true;
                        }
                    }
                }
            }

        }

        // Throw grenades
        if (Input.GetButton(playerCtrlMap[14]))
        {
            if (characterInventory.grenadesCount > 0 && characterInventory.isThrowing == false && Rolling == false && Jumping == false && Sprinting == false && characterInventory.Armed)
            {
                characterInventory.actualWeapon.CancelReload();
                characterInventory.isThrowing = true;
                charAnimator.SetTrigger("ThrowG");
                characterInventory.actualWeapon.LaserSight.enabled = false;
                characterInventory.EnableArmIK(false);

            }
        }
        // Shoot Weapons
        if (Input.GetAxis(playerCtrlMap[4]) >= 0.5f || Input.GetButton(playerCtrlMap[15]))
        {
            if (Rolling == false && Jumping == false && Sprinting == false && characterInventory.isThrowing == false)
            {
                if (characterInventory.CanShoot && characterInventory.Weapon[characterInventory.ActiveWeapon] != null && Time.time >= (characterInventory.LastFireTimer + characterInventory.FireRateTimer))
                {
                    //Melee Weapon
                    if (characterInventory.actualWeapon.Type == global::PrWeapon.WT.Melee)
                    {
                        Rolling = true;
                        characterInventory.LastFireTimer = Time.time;
                        charAnimator.SetTrigger("AttackMelee");
                        charAnimator.SetInteger("MeleeType", UnityEngine.Random.Range(0, 2));
                        characterUtils.useRootMotion = true;
                        CantRotate();

                    }
                    //Ranged Weapon
                    else
                    {
                        if (Aiming || WASDOldStyleAim)
                        {
                            characterInventory.LastFireTimer = Time.time;
                            characterInventory.actualWeapon.Shoot();
                            if (characterInventory.actualWeapon.Reloading == false)
                            {
                                if (characterInventory.actualWeapon.ActualBullets > 0)
                                    charAnimator.SetTrigger("Shoot");
                                else
                                    characterInventory.WeaponReload();
                            }
                        }
                    }
                }
            }
        }
        // Reload Weapon
        if (Input.GetButtonDown(playerCtrlMap[5]))
        {
            if (Rolling == false && Sprinting == false && characterInventory.isThrowing == false)
            {
                characterInventory.WeaponReload();
            }
            if (characterInventory.actualWeapon.Reloading == true)
            {
                characterInventory.actualWeapon.TryQuickReload();
            }
        }
        // Aim
        if (useThirdPersonCamera)
        {
            if (Input.GetButton(playerCtrlMap[8]) || Input.GetAxis(playerCtrlMap[9]) >= 0.5f)
            {
                if (Rolling == false && Jumping == false && Sprinting == false && !UsingObject && characterInventory.Armed && characterInventory.isThrowing == false)
                {
                    if (characterInventory.actualWeapon.Type != global::PrWeapon.WT.Melee)
                    {
                        if (!alwaysAim)
                        {
                            Aiming = true;
                            if (characterInventory.actualWeapon.Reloading == true || characterInventory.isThrowing == true)
                                characterInventory.actualWeapon.LaserSight.enabled = false;
                            else
                                characterInventory.actualWeapon.LaserSight.enabled = true;
                            charAnimator.SetBool("RunStop", false);
                            charAnimator.SetBool("Aiming", true);

                            
                        }

                    }
                }

            }
            //Stop Aiming
            else if (Input.GetButtonUp(playerCtrlMap[8]) || Input.GetAxis(playerCtrlMap[9]) < 0.5f)
            {
                if (!alwaysAim)
                {
                    StopAiming();
                }
            }
        }
        else if (!useThirdPersonCamera)
        {
            if (Input.GetButton(playerCtrlMap[8]) || Mathf.Abs(Input.GetAxis(playerCtrlMap[2])) > 0.3f || Mathf.Abs(Input.GetAxis(playerCtrlMap[3])) > 0.3f)
            {
                if (Rolling == false && Jumping == false && Sprinting == false && !UsingObject && characterInventory.Armed && characterInventory.isThrowing == false)
                {
                    if (characterInventory.actualWeapon.Type != global::PrWeapon.WT.Melee)
                    {
                        if (!alwaysAim)
                        {
                            Aiming = true;
                            if (characterInventory.actualWeapon.Reloading == true || characterInventory.isThrowing == true)
                                characterInventory.actualWeapon.LaserSight.enabled = false;
                            else
                                characterInventory.actualWeapon.LaserSight.enabled = true;
                            charAnimator.SetBool("RunStop", false);
                            charAnimator.SetBool("Aiming", true);
                        }

                    }
                }

            }
            //Stop Aiming
            else if (Input.GetButtonUp(playerCtrlMap[8]) || Mathf.Abs(Input.GetAxis(playerCtrlMap[2])) < 0.3f || Mathf.Abs(Input.GetAxis(playerCtrlMap[3])) < 0.3f)
            {
                if (!alwaysAim)
                {
                    StopAiming();
                    
                }
            }
        }
        
        //USE
        if (Input.GetButtonDown(playerCtrlMap[11]) && UsableObject && !UsingObject && characterInventory.isThrowing == false)
        {
            if (UsableObject.GetComponent<PrUsableDevice>().IsEnabled && Jumping == false && Rolling == false)
            {
                characterInventory.actualWeapon.CancelReload();
                StartUsingGeneric("Use");

                UsableObject.GetComponent<PrUsableDevice>().User = this.gameObject;
                UsableObject.GetComponent<PrUsableDevice>().Use();
            }
            else if (vehicleToDrive != null && Jumping == false && Rolling == false)
            {
            }
        }

        //Pickup
        else if (Input.GetButtonDown(playerCtrlMap[11]) && !UsableObject && !UsingObject && characterInventory.PickupObj && characterInventory.isThrowing == false)
        {

            if (Rolling == false && Jumping == false)
            {
                characterInventory.actualWeapon.CancelReload();

                StartUsingGeneric("Pickup");

                characterInventory.PickupItem();
            }
        }

        //Get Inside Car
        else if (Input.GetButtonDown(playerCtrlMap[11]) && vehicleToDrive && !UsingObject && characterInventory.isThrowing == false)
        {
            if (Jumping == false && Rolling == false && insideVehicle == false && Time.time >= timeToNextVehicle + 0.15f)
            {
                GetInsideVehicle();
            }

        }
        // Change Weapon
        if (Input.GetButtonDown(playerCtrlMap[13])  || Input.GetAxis(playerCtrlMap[16]) != 0f)
        {

            if (characterInventory.isThrowing == false && Time.time >= characterInventory.lastWeaponChange + 0.25f && characterInventory.Armed)
            {
                characterInventory.actualWeapon.CancelReload();
                characterInventory.ChangeWeapon();
            }

        }
        // change weapons directly with shortcuts
        if (useDirectSwitchKeys)
        {
            if (weaponDirectSwitchKeys.Length > 0)
            {
                for (int i = 0; i < weaponDirectSwitchKeys.Length; i++)
                {
                    if (Input.GetButtonDown(weaponDirectSwitchKeys[i]))
                    {
                        characterInventory.actualWeapon.CancelReload();
                        characterInventory.ChangeToWeapon(i);
                    }
                }
            }
        }


        if (alwaysAim)
        {
            if (!UsingObject && !Sprinting)
                Aiming = true;
            else
                Aiming = false;
        }

        if (b_CanRotate)
        {
            if (Aiming && !Rolling)
            {
                if (!JoystickEnabled)
                    MouseAim(AimFinalPos.position);
                else
                    JoystickLook(h, v);
            }
            else
            {
                RunningLook(new Vector3(h, 0, v));
            }
        }

        m_Move = new Vector3(h, 0, v);

        if (!JoystickEnabled)
            m_Move = m_Move.normalized * m_MoveSpeedSpecialModifier;
        else
            m_Move *= m_MoveSpeedSpecialModifier;

        Vector3 camRot = m_Cam.transform.forward;
        camRot.y = 0.0f;
        Quaternion camRotationFlattened = Quaternion.LookRotation(camRot);
        Vector3 originalMove = camRotationFlattened * m_Move;
        Vector3 animatorMove = Quaternion.Euler(0, 0 - controlledCharacter.transform.eulerAngles.y + m_Cam.transform.parent.transform.eulerAngles.y, 0) * m_Move;

        //Rotate move in camera space
        if (physicsMovement && m_Rigidbody)
        {
            m_Move = originalMove;
            Debug.DrawRay(controlledCharacter.transform.position + Vector3.up, m_Move, Color.green);
            Debug.DrawRay(controlledCharacter.transform.position + Vector3.up, originalMove, Color.blue);
            Debug.DrawRay(controlledCharacter.transform.position + Vector3.up, m_Rigidbody.linearVelocity, Color.red);

        }
        else
            m_Move = Quaternion.Euler(0, 0 - controlledCharacter.transform.eulerAngles.y + m_Cam.transform.parent.transform.eulerAngles.y, 0) * m_Move;

        //Move Player
        Move(m_Move, animatorMove, crouch, m_Jump);
        m_Jump = false;

        //Sprint
        if (Input.GetButton(playerCtrlMap[7]) && !Rolling && !Jumping && m_Move.magnitude >= 0.2f && !UsingObject && !crouch)
        {


            if (character.ActualStamina > 0.0f)
            {
                Sprinting = true;
                if (alwaysAim)
                {
                    Aiming = false;
                    charAnimator.SetBool("Aiming", false);
                }
            }
            else
            {
                Sprinting = false;
                if (alwaysAim)
                {
                    Aiming = true;
                    charAnimator.SetBool("Aiming", true);
                }
            }
        }
        else
        {
            Sprinting = false;
            if (alwaysAim)
            {
                Aiming = true;
                charAnimator.SetBool("Aiming", true);
            }
        }

        character.UsingStamina = Sprinting;

        //Get distance to player
        if (Aiming)
        {
            aimTargetDistanceToPlayer = Vector3.Distance(AimTargetVisual.transform.position, characterUtils.transform.position);
            if (aimTargetDistanceToPlayer < 1.0f)
            {
                Debug.Log("can´t Aim");
                StopAiming();
            }
        }
    }

    void StopAiming()
    {
        Aiming = false;
        charAnimator.SetBool("Aiming", false);
        if (characterInventory.actualWeapon.LaserSight)
            characterInventory.actualWeapon.LaserSight.enabled = false;
    }

    public void GetOutsideVehicle()
    {
        timeToNextVehicle = Time.time;
        //Debug.Log("getting out");
        m_Rigidbody = characterUtils.gameObject.AddComponent<Rigidbody>();
        m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        m_Rigidbody.isKinematic = false;
        m_Rigidbody.useGravity = true;
        characterUtils.enabled = true;

        m_Capsule.isTrigger = false;
        controlledCharacter.transform.position = vehicleToDrive.outDriverPos.position;
        controlledCharacter.transform.rotation = vehicleToDrive.outDriverPos.rotation;

        currentPlayerSelection.enabled = true;
        if (vehicleToDrive.controller.currentPlayerSelection)
        {
            vehicleToDrive.controller.currentPlayerSelection.enabled = false;
        }

        //Set player visibility
        if (vehicleToDrive.showDriver)
        {
            //Set drinving anim
            //Play Anim
            if (charAnimator.GetBool("driving"))
            {
                charAnimator.SetBool("driving", false);
                charAnimator.SetInteger("vehicleType", 0);
            }

            //Play anim in vehicle (open door or something like that)
            //vehicleToDrive.animator.SetTrigger("OpenDriver");
        }
        else
        {
            controlledCharacter.SetActive(true);
            vehicleToDrive.controller.tag = "Vehicles";
            vehicleToDrive.tag = "Vehicles";
        }

        characterUtils.gameObject.GetComponent<PrCharacterIK>().enabled = true;
        characterUtils.gameObject.GetComponent<PrCharacterIK>().InsideVehicle(false, null,
            null, null, null);
        //Play anim in vehicle (open door or something like that)
        //vehicleToDrive.animator.SetTrigger("OpenDriver");

        //Set Character as a Child of the vehicle.
        controlledCharacter.transform.SetParent(this.transform);
        vehicleToDrive.controller.driverController = null;
        vehicleToDrive.driver = null;
        //Set camera for vehicle.

        //activate all character Components. 
        m_CanMove = true;
        if (!JoystickEnabled)
            VisualAimVisible(false);
        characterHUD.SetCanvasesVisible(true);

        //Deactivate Vehicle
        vehicleToDrive.controller.controlON = false;
        vehicleToDrive.team = -1;
        if (vehicleToDrive.useHUD)
        {
            if (vehicleToDrive.controller.aimTarget)
                vehicleToDrive.controller.actualAimTarget.SetActive(false);
            vehicleToDrive.actualHUD.SetCanvasesVisible(false);
            vehicleToDrive.actualHUD.gameObject.SetActive(false);
        }

        vehicleToDrive.gameObject.layer = LayerMask.NameToLayer("Vehicles");

        insideVehicle = false;

        if (wasArmed)
        {
            wasArmed = false;
            characterInventory.Armed = true;
            characterInventory.EquipWeapon(true);
        }
        character.canBeDamaged = true;
    }

    void GetInsideVehicle()
    {
        if (vehicleToDrive.driver == null)
        {
            insideVehicle = true;
            character.canBeDamaged = false;
            //Set vehicle driver position and adjust player physics. 
            m_Rigidbody.isKinematic = true;
            m_Rigidbody.useGravity = false;
            characterUtils.enabled = false;
            DestroyImmediate(m_Rigidbody);
            //m_Capsule.enabled = false;
            m_Capsule.isTrigger = true;
            controlledCharacter.transform.position = vehicleToDrive.driverLocation.position;
            controlledCharacter.transform.rotation = vehicleToDrive.driverLocation.rotation;
            //Set player Unarmed to drive
            if (characterInventory.Armed)
            {
                wasArmed = true;
                characterInventory.Armed = false;
                characterInventory.EquipWeapon(false);
            }

            currentPlayerSelection.enabled = false;
            if (vehicleToDrive.controller.currentPlayerSelection)
            {
                vehicleToDrive.controller.currentPlayerSelection.material = currentPlayerSelection.material;
                vehicleToDrive.controller.currentPlayerSelection.enabled = true;
            }

            //Set player visibility
            if (vehicleToDrive.showDriver)
            {
                //Set drinving anim
                //Play Anim

                charAnimator.SetBool("driving", true);
                charAnimator.SetInteger("vehicleType", 0);

                characterUtils.gameObject.GetComponent<PrCharacterIK>().enabled = true;
                characterUtils.gameObject.GetComponent<PrCharacterIK>().InsideVehicle(true, vehicleToDrive.handsIKPos[0],
                    vehicleToDrive.handsIKPos[1], vehicleToDrive.feetIKPos[0], vehicleToDrive.feetIKPos[1]);
                //Play anim in vehicle (open door or something like that)
                //vehicleToDrive.animator.SetTrigger("OpenDriver");
            }
            else
            {
                controlledCharacter.SetActive(false);
                vehicleToDrive.controller.tag = controlledCharacter.tag;
                vehicleToDrive.tag = controlledCharacter.tag;
            }

            vehicleToDrive.gameObject.layer = LayerMask.NameToLayer("PlayerCharacter");

            //Set Character as a Child of the vehicle.
            controlledCharacter.transform.SetParent(vehicleToDrive.driverLocation);
            vehicleToDrive.controller.playerCtrlMap = playerCtrlMap;
            vehicleToDrive.controller.driverController = this.GetComponent<PrCharacterController>();
            vehicleToDrive.driver = controlledCharacter;
            //Set camera for vehicle.


            //Deactivate all character Components. 
            m_CanMove = false;
            VisualAimVisible(true);
            characterHUD.SetCanvasesVisible(false);

            //Activate Vehicle
            vehicleToDrive.team = character.team;
            vehicleToDrive.controller.controlON = true;
            if (vehicleToDrive.useHUD)
            {
                if (vehicleToDrive.controller.aimTarget && !JoystickEnabled)
                    vehicleToDrive.controller.actualAimTarget.SetActive(true);
                vehicleToDrive.actualHUD.gameObject.SetActive(true);
                vehicleToDrive.actualHUD.SetCanvasesVisible(true);
            }
                
        }

    }

    
    void DeactivateAll()
    {

    }

    void UpdateUseObject()
    {
        if (UsingObject && UsableObject)
        {
            Quaternion EndRotation = Quaternion.LookRotation(UsableObject.transform.position - controlledCharacter.transform.position);
            controlledCharacter.transform.rotation = Quaternion.Lerp(controlledCharacter.transform.rotation, EndRotation, Time.deltaTime * 5);
            controlledCharacter.transform.localEulerAngles = new Vector3(0, controlledCharacter.transform.localEulerAngles.y, 0);
        }
    }

    public void StartUsingGeneric(string Type)
    {
        Aiming = false;
        UsingObject = true;

        m_CanMove = false;
        charAnimator.SetTrigger(Type);

        characterInventory.actualWeapon.LaserSight.enabled = false;

        characterInventory.EnableArmIK(false);
    }

    public void StopUse()
    {
        m_CanMove = true;
        charAnimator.SetTrigger("StopUse");
        UsingObject = false;

        characterInventory.EnableArmIK(true);

    }

    public void StopMoving(string Case)
    {
        if (Case == "GameOver")
        {
            m_CanMove = false;
            b_CanRotate = false;

            charAnimator.SetTrigger("GameOver");
            //Inventory.isDead = true;
        }
    }

    public void VisualAimVisible(bool IsOn)
    {
        
        if (IsOn)
            AimTargetVisual.SetActive(false);
        else
            AimTargetVisual.SetActive(true);
            
    }

    public void EndRoll()
    {
        Rolling = false;
        Jumping = false;
        b_CanRotate = true;
    }

    public void CanJump(int Value)
    {
        if (Value == 1)
        {
            b_canJump = true;
        }
        else
        {
            b_canJump = false;
        }
    }

    public void CantRotate()
    {
        b_CanRotate = false;
    }

    private void RunningLook(Vector3 Direction)
    {
        if (Direction.magnitude >= 0.05f)
        {
            Direction = Quaternion.Euler(0, 0 + m_Cam.transform.parent.transform.eulerAngles.y, 0) * Direction;

            if (!Rolling)
                controlledCharacter.transform.rotation = Quaternion.Lerp(controlledCharacter.transform.rotation, Quaternion.LookRotation(Direction), Time.deltaTime * (RunRotationSpeed * 0.1f));
            else
                controlledCharacter.transform.rotation = Quaternion.Lerp(controlledCharacter.transform.rotation, Quaternion.LookRotation(Direction), Time.deltaTime * RunRotationSpeed);

            controlledCharacter.transform.localEulerAngles = new Vector3(0, controlledCharacter.transform.localEulerAngles.y, 0);
        }

    }

    public void EndMelee()
    {
        characterUtils.useRootMotion = false;
        Rolling = false;
        EndRoll();
        if (characterInventory.waitingArmIK)
            characterInventory.EnableArmIK(true);
        characterInventory.Weapon[characterInventory.ActiveWeapon].transform.rotation = characterInventory.WeaponR.rotation;
        characterInventory.Weapon[characterInventory.ActiveWeapon].transform.localRotation = Quaternion.Euler(90, 0, 0);
    }

    public void MeleeEvent()
    {
        //this event comes from animation, the exact moment of HIT
        if (characterInventory.actualWeapon.Type == global::PrWeapon.WT.Melee)
        {
            characterInventory.actualWeapon.AttackMelee(true);
        }
        else if (characterInventory.meleeActive && characterInventory.meleeWeapon)
        {
            characterInventory.actualMeleeWeapon.GetComponent<PrWeapon>().AttackMelee(false);
        }

    }

    private void JoystickLook(float h, float v)
    {
        JoystickTarget.transform.rotation = controlledCharacter.transform.rotation;

        //Joystick Look input
        float LookX = Input.GetAxis(playerCtrlMap[2]);
        float LookY = Input.GetAxis(playerCtrlMap[3]);

        Vector3 JoystickLookVec = new Vector3(LookX, 0, LookY) * 10;

        JoystickLookVec = Quaternion.Euler(0, 0 + m_Cam.transform.parent.transform.eulerAngles.y, 0) * JoystickLookVec;

        JoystickTarget.transform.position = controlledCharacter.transform.position + JoystickLookVec * 5;

        if (Mathf.Abs(LookX) <= 0.2f && Mathf.Abs(LookY) <= 0.2f)
        {
            JoystickTarget.transform.localPosition += JoystickTarget.transform.forward * 2;
        }

        JoystickLookRot.transform.LookAt(JoystickTarget.transform.position);


        if (!useThirdPersonCamera)
        {
            AimTargetVisual.transform.position = JoystickTarget.transform.position;
            AimTargetVisual.transform.LookAt(controlledCharacter.transform.position);

            controlledCharacter.transform.rotation = Quaternion.Lerp(controlledCharacter.transform.rotation, JoystickLookRot.transform.rotation, Time.deltaTime * AimingRotationSpeed);
            controlledCharacter.transform.localEulerAngles = new Vector3(0, controlledCharacter.transform.localEulerAngles.y, 0);
        }
       
        if (useThirdPersonCamera)
        {
            AimTargetVisual.transform.position = cameraComponent.aimingPos.position - AimTargetVisual.transform.up;
            AimTargetVisual.transform.rotation = cameraComponent.aimingPos.rotation;

            MouseAim(AimTargetVisual.transform.position);
        }


    }


    private void MouseTargetPos()
    {
        if (m_Cam && !useThirdPersonCamera)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            int defaultInt = 0;
            int objectInt = 8;
            // int dynamicsInt = 11;
            
            int defaultLayer = 1 << defaultInt;
            int objectLayer = 1 << objectInt;
            // int dynamicsLayer = 1 << dynamicsInt;

            int finalMask = defaultLayer; //| objectLayer;// | vehiclesLayer;
            if (Physics.Raycast(ray, out hit, 2000f, finalMask))
            {
                ////Debug.Log("---------- Hit Something------------");
                Vector3 FinalPos = new Vector3(hit.point.x, 0, hit.point.z);

                if (allowVerticalAiming)
                    FinalPos.y = hit.point.y;
                AimTargetVisual.transform.position = FinalPos;
                AimTargetVisual.transform.LookAt(controlledCharacter.transform.position);

            }
        }
        else if (m_Cam && useThirdPersonCamera)
        {
            //Debug.Log(useThirdPersonCamera);
            AimTargetVisual.transform.position = cameraComponent.aimingPos.position - AimTargetVisual.transform.up;
            AimTargetVisual.transform.rotation = cameraComponent.aimingPos.rotation;
        }
    }

    private void MouseAim(Vector3 FinalPos)
    {
        JoystickLookRot.transform.LookAt(FinalPos);
        controlledCharacter.transform.rotation = Quaternion.Lerp(controlledCharacter.transform.rotation, JoystickLookRot.transform.rotation, Time.deltaTime * AimingRotationSpeed);
        controlledCharacter.transform.localEulerAngles = new Vector3(0, controlledCharacter.transform.localEulerAngles.y, 0);

    }

    

    public void Move(Vector3 move, Vector3 animatorMove, bool crouch, bool jump)
    {
        if (!UsingObject)
        {
            CheckGroundStatus();

            m_TurnAmount = animatorMove.x;
            m_ForwardAmount = animatorMove.z;

            // control and velocity handling is different when grounded and airborne:
            if (m_IsGrounded)
            {
                HandleGroundedMovement(crouch, jump, move);
            }
            else
            {
                HandleAirborneMovement();
                /*
                if (!Jumping)
                    m_Rigidbody.velocity = m_Rigidbody.velocity + (controlledCharacter.transform.forward * move.magnitude * 0.15f);*/
            }

            ScaleCapsuleForCrouching(crouch);
            PreventStandingInLowHeadroom();

            // send input and other state parameters to the animator
            UpdateAnimator(move);
        }

    }


    void ScaleCapsuleForCrouching(bool crouch)
    {
        if (m_Rigidbody)
        {
            if (m_IsGrounded && crouch)
            {
                if (m_Crouching) return;
                m_Capsule.height = m_Capsule.height / 1.5f;
                m_Capsule.center = m_Capsule.center / 1.5f;
                m_Crouching = true;
            }
            else
            {
                Ray crouchRay = new Ray(m_Rigidbody.position + Vector3.up * m_Capsule.radius * k_Half, Vector3.up);
                float crouchRayLength = m_CapsuleHeight - m_Capsule.radius * k_Half;
                if (Physics.SphereCast(crouchRay, m_Capsule.radius * k_Half, crouchRayLength))
                {
                    m_Crouching = true;
                    return;
                }
                m_Capsule.height = m_CapsuleHeight;
                m_Capsule.center = m_CapsuleCenter;
                m_Crouching = false;
            }
        }
       
    }

    void PreventStandingInLowHeadroom()
    {
        // prevent standing up in crouch-only zones
        if (!m_Crouching && m_Rigidbody)
        {
            Ray crouchRay = new Ray(m_Rigidbody.position + Vector3.up * m_Capsule.radius * k_Half, Vector3.up);
            float crouchRayLength = m_CapsuleHeight - m_Capsule.radius * k_Half;
            if (Physics.SphereCast(crouchRay, m_Capsule.radius * k_Half, crouchRayLength))
            {
                m_Crouching = true;
            }
        }
    }


    void UpdateAnimator(Vector3 move)
    {

        // update the animator parameters
        if (m_Rigidbody)
        {
            if (physicsMovement)
                charAnimator.SetFloat("Y", m_ForwardAmount, AnimatorAimingDampValue, Time.deltaTime);
            else
                charAnimator.SetFloat("Y", m_ForwardAmount - m_Rigidbody.linearVelocity.magnitude, AnimatorAimingDampValue, Time.deltaTime);

            charAnimator.SetFloat("X", m_TurnAmount, AnimatorAimingDampValue, Time.deltaTime);

            if (!Sprinting)
                charAnimator.SetFloat("Speed", move.magnitude, AnimatorSprintDampValue, Time.deltaTime);
            else
                charAnimator.SetFloat("Speed", 2.0f, AnimatorRunDampValue, Time.deltaTime);

            charAnimator.SetBool("Crouch", m_Crouching);
            charAnimator.SetBool("OnGround", m_IsGrounded);

            // the anim speed multiplier allows the overall speed of walking/running to be tweaked in the inspector,
            // which affects the movement speed because of the root motion.
            if (m_IsGrounded && move.magnitude > 0)
            {

                if (Aiming && !Rolling)
                {
                    move *= PlayerAimSpeed;
                    if (physicsMovement)
                    {
                        m_Rigidbody.linearVelocity = move * move.magnitude * 0.5f;
                    }
                    else
                    {
                        controlledCharacter.transform.Translate(move * Time.deltaTime);
                    }

                    characterUtils.useRootMotion = false;
                }
                else if (UsingObject)
                {
                    move = move * 0.0f;
                    if (physicsMovement)
                        m_Rigidbody.linearVelocity = Vector3.zero;
                    else
                        controlledCharacter.transform.Translate(Vector3.zero);
                    characterUtils.useRootMotion = false;
                }
                else
                {
                    if (useRootMotion)
                        characterUtils.useRootMotion = true;
                    else
                    {
                        if (!Rolling)
                        {
                            if (Sprinting)
                            {
                                move *= PlayerSprintSpeed;
                            }
                            else if (crouch)
                            {
                                move *= PlayerCrouchSpeed;
                            }
                            else
                            {
                                move *= PlayerRunSpeed;
                            }

                            if (physicsMovement)
                                m_Rigidbody.linearVelocity = controlledCharacter.transform.forward * move.magnitude;
                            else
                                controlledCharacter.transform.Translate(move * Time.deltaTime);

                            characterUtils.useRootMotion = false;
                        }
                    }

                }
                charAnimator.speed = m_AnimSpeedMultiplier;
            }
            else
            {

                // don't use that while airborne
                charAnimator.speed = 1;
            }
        }
        
    }


    void HandleAirborneMovement()
    {
        if (m_Rigidbody)
        {
            // apply extra gravity from multiplier:
            Vector3 extraGravityForce = (Physics.gravity * m_GravityMultiplier) - Physics.gravity;

            m_Rigidbody.AddForce(extraGravityForce);

            m_GroundCheckDistance = m_Rigidbody.linearVelocity.y < 0 ? m_OrigGroundCheckDistance : 0.01f;
        }
        
    }

    public void AddForce(float kickBack)
    {

        //Vector3 velocityDir = Quaternion.Euler(0, 0 - controlledCharacter.transform.eulerAngles.y + m_Cam.transform.parent.transform.eulerAngles.y, 0) * (controlledCharacter.transform.forward * -kickBack);
        if (!m_Crouching && useShootingKickBack)
            m_Rigidbody.linearVelocity = (controlledCharacter.transform.forward * -kickBack);
    }

    void HandleGroundedMovement(bool crouch, bool jump, Vector3 move)
    {
        // check whether conditions are right to allow a jump:

        if (jump && !crouch)
        {
            if (charAnimator.GetCurrentAnimatorStateInfo(0).IsName("Grounded") || charAnimator.GetCurrentAnimatorStateInfo(0).IsName("Grounded_Unarmed") || charAnimator.GetCurrentAnimatorStateInfo(0).IsName("Aiming")
                || charAnimator.GetCurrentAnimatorStateInfo(0).IsName("Grounded_Armed_Crouch") || charAnimator.GetCurrentAnimatorStateInfo(0).IsName("Grounded_Unarmed_Crouch"))
            {
                // jump!
                ////Debug.Log("-------------------JUMP---------------------");
                if (!Aiming)
                    m_Rigidbody.linearVelocity = ((controlledCharacter.transform.forward * m_ForwardAmount * 0.5f) + new Vector3(0, 1, 0)) * m_JumpPower;
                else
                    m_Rigidbody.linearVelocity = (new Vector3(0, 1, 0)) * m_JumpPower;

                m_IsGrounded = false;
                characterUtils.useRootMotion = false;
                m_GroundCheckDistance = 0.025f;
            }

        }
    }

    //This function it´s used only for Aiming and Jumping states. Those anims doesn´t have root motion so we move the player by script
    public void OnAnimatorMove()
    {
        // we implement this function to override the default root motion.
        // this allows us to modify the positional speed before it's applied.
        if (m_IsGrounded && Time.deltaTime > 0)
        {
            Vector3 v = (charAnimator.deltaPosition * m_MoveSpeedMultiplier) / Time.deltaTime;

            // we preserve the existing y part of the current velocity.
            v.y = m_Rigidbody.linearVelocity.y;
            m_Rigidbody.linearVelocity = v;
        }
    }
    /*
    public bool IsGrounded()
    {
        Ray ray = new Ray(controlledCharacter.transform.position + Vector3.up * colExtents.y, Vector3.down);
        return Physics.SphereCast(ray, colExtents.y, colExtents.y + 0.2f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(controlledCharacter.transform.position + Vector3.up * colExtents.y, 1.0f);
    }
    */
    void CheckGroundStatus()
    {
        RaycastHit hitInfo;
//#if UNITY_EDITOR
        // helper to visualise the ground check ray in the scene view
        //Debug.DrawLine(transform.position + (Vector3.up * 0.1f), transform.position + (Vector3.up * 0.1f) + (Vector3.down * m_GroundCheckDistance));
//#endif
        // 0.1f is a small offset to start the ray from inside the character
        // it is also good to note that the transform position in the sample assets is at the base of the character

        //OLD WAY if (Physics.Raycast(controlledCharacter.transform.position + (Vector3.up * 0.1f), Vector3.down, out hitInfo, m_GroundCheckDistance))

        //New Way to check (thanks to Kristian Holik!)
        //if (IsGrounded())
        if (Physics.Raycast(controlledCharacter.transform.position + (Vector3.up * 0.1f), Vector3.down, out hitInfo, m_GroundCheckDistance))
        {
            ////Debug.Log("Touching floor");
            m_IsGrounded = true;
            //if (useRootMotion)
                characterUtils.useRootMotion = true;
        }
        else
        {
            ////Debug.Log("Not touching floor");
            m_IsGrounded = false;
            characterUtils.useRootMotion = false;
        }
    }

    public void SetNewSpeed(float speedFactor)
    {
        m_MoveSpeedSpecialModifier = speedFactor;
    }


}
