using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PrAIController : MonoBehaviour
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
    [HideInInspector]
    public Animator charAnimator;
    [HideInInspector]
    public PrPlayerSettings playerSettings;

    public enum enemyType
    {
        Humanoid,
        Bot
    }

    public enum AIState
    {
        Patrol,
        Wander,
        ChasingPlayer,
        AimingPlayer,
        Attacking,
        CheckingSound,
        Dead,
        TowerDefensePath,
        FriendlyFollow,
        Waiting
    }

    [Header("Basic AI Settings")]
    public enemyType type = enemyType.Humanoid;
    public AIState actualState = AIState.Patrol;
    private bool stopPlaying = false;

    public float chasingSpeed = 1.0f;
    public float normalSpeed = 0.75f;
    public float aimingSpeed = 0.3f;

    public bool stopIfGetHit = true;
    public float rotationSpeed = 0.15f;
    public float randomWaypointAccuracy = 1.0f;

    public bool useRootmotion = true;
    public bool standInPlace = false;

    public bool lockRotation = false;
    public Vector3 lockedRotDir = Vector3.zero;

    private bool canAttack = true;
    [HideInInspector]
    public int team = 10;
    public bool friendlyAI = false;
    [HideInInspector]
    public List<GameObject> friends;
    private GameObject closestFriend;
    private float closestFriendDistance = 99999.0f;

    [Header("Wandering Settings")]
    public bool useWander = true;
    public float wanderingWaitTime = 3.0f;
    public float wanderingRadius = 10.0f;

    private float currentWanderingWaitTimer = 0.0f;
    private bool reachedWanderingPosition = true;
    private bool newPositionAdded = false;

    [Header("AI Sensor Settings")]

    public float awarnessDistance = 20;
    public float aimingDistance = 15;
    public float attackDistance = 8;
    private float playerActualDistance = 99999;

    [Range(10f, 360f)]
    public float lookAngle = 90f;
    public float hearingDistance = 20;

    public Transform eyesAndEarTransform;
    private Transform actualSensorTrans;
    private Vector3 actualSensorPos;

    [HideInInspector]
    public bool Aiming = false;
    [HideInInspector]
    public bool playerIsVisible = false;
    private float playerLastTimeSeen = 0.0f;
    private bool attackingVehicle = false;

    public float forgetPlayerTimer = 5.0f;
    private float actualforgetPlayerTimer = 0.0f;
    private Vector3 lastNoisePos;
    private float alarmedTimer = 10.0f;
    private float actualAlarmedTimer = 0.0f;
    private float newtAlarm = 0.0f;

    [HideInInspector]
    public bool lookForPlayer = false;

    [Header("Waypoints Settings")]
    public PrWaypointsRoute waypointRoute;
    private int actualWaypoint = 0;
    [HideInInspector]
    public Transform[] waypoints;
    public bool waypointPingPong = false;
    private bool inverseDirection = false;
    private bool waiting = false;
    private float timeToWait = 3.0f;
    private float actualTimeToWait = 0.0f;
    private float waitTimer = 0.0f;
    public Vector3 finalGoal = Vector3.zero;

    [Header("Tower Defense Settings")]
    public bool towerDefenseAI = false;
    public Transform towerDefenseTarget;
    public int towerDefenseStage = 0;
    private bool pathEnded = false;
    private Vector3 attackPos = Vector3.zero;

    [Header("Weapon Settings")]
    public float attackAngle = 5f;
    public int meleeAttacksOptions = 1;
    private int actualMeleeAttack = 0;
    public bool chooseRandomMeleeAttack = true;

    //[HideInInspector]
    public List<GameObject> existingPossibleTargets;
    //[HideInInspector]
    public Transform charTargetTransform;
    public PrActorUtils charTargetUtils;
    public PrAIController charTargetConstroller;
    public Transform currentTarget;

    [Header("VFX")]
    public int hitAnimsMaxTypes = 1;
    private int randomHitAnim = -1;

    [HideInInspector]
    public NavMeshAgent agent;
    [HideInInspector]
    public Rigidbody AIRigidBody;
    [HideInInspector]
    public CharacterController AIController;
    public float characterHeight = 2.0f;
    public float characterRadius = 0.5f;

    [Header("Debug")]
    public bool doNotAttackPlayer = false;
    public bool DebugOn = false;
    public TextMesh DebugText;

    public Mesh AreaMesh;
    public Mesh TargetArrow;

    private PrCharacterIK CharacterIKController;
    private Transform ArmIKTarget;

    public void InitializeController()
    {
        //General Variables
        characterInventory = GetComponent<PrCharacterInventory>();
        character = GetComponent<PrCharacter>();
        characterHUD = GetComponent<PrActorHUD>();
        if (controlledCharacter.GetComponent<Animator>())
            charAnimator = controlledCharacter.GetComponent<Animator>();
        characterUtils = character.actualCharUtils;

        //Set friendly AI
        if (character.type == PrActor.CHT.friendlyAI)
        {
            friendlyAI = true;
            characterUtils.Type = PrActorUtils.AT.friendlyAI;
        }
        else if (character.type == PrActor.CHT.enemy)
        {
            characterUtils.Type = PrActorUtils.AT.enemy;
        }


        //Add NavMesh Agent
        agent = controlledCharacter.AddComponent<NavMeshAgent>();

        //Set up rigid bodies
        AIRigidBody = controlledCharacter.GetComponent<Rigidbody>();
        if (controlledCharacter.GetComponent<CapsuleCollider>())
        {
            characterHeight = controlledCharacter.GetComponent<CapsuleCollider>().height;
            characterRadius = controlledCharacter.GetComponent<CapsuleCollider>().radius;
            Destroy(controlledCharacter.GetComponent<CapsuleCollider>());
            controlledCharacter.AddComponent<CharacterController>();
        }

        AIController = controlledCharacter.GetComponent<CharacterController>();
        AIController.height = characterHeight;
        AIController.center = new Vector3(0, characterHeight * 0.5f, 0);
        AIController.radius = characterRadius;

        AIRigidBody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        AIRigidBody.isKinematic = false;
        AIController.enabled = true;

        /// <summary>
        /// Setting All the variables
        /// </summary>

        //Debug
        if (DebugText && !DebugOn)
        {
            DebugText.GetComponent<Renderer>().enabled = false;
            DebugText.gameObject.AddComponent<PrCopyPosition>();
            DebugText.GetComponent<PrCopyPosition>().targetObject = characterUtils.transform;
        }
            

        //Create Waypoints Array
        SetWaypoints();

        if (characterUtils.EarAndEyesPosition)
            actualSensorTrans = characterUtils.EarAndEyesPosition;
        else if (eyesAndEarTransform)
            actualSensorTrans = eyesAndEarTransform;
        else
            actualSensorTrans = controlledCharacter.transform;

        actualSensorPos = actualSensorTrans.position;

        actualforgetPlayerTimer = forgetPlayerTimer;

        SetTimeToWait();

        //Friendly AI setup
        if (friendlyAI)
        {
            team = 0;
        }

        character.team = team;
        characterUtils.team = team;

        //Find Players
        FindPlayers();

        //Initialize Waypoints
        if (waypoints.Length > 0)
        {
            finalGoal = waypoints[0].transform.position;
        }

        if (lookForPlayer && !towerDefenseAI)
            CheckPlayerVisibility(360f);

        if (useRootmotion)
            characterUtils.useRootMotion = true;

        agent.enabled = true;

        if (!towerDefenseAI)
            GetCLoserWaypoint();

        GameObject[] AIs = GameObject.FindGameObjectsWithTag("AIPlayer");
        foreach (GameObject AI in AIs)
        {
            if (AI != null)
                AI.transform.parent.GetComponent<PrAIController>().FindPlayers();
        }

        SetupAIWeapon();

        currentWanderingWaitTimer = wanderingWaitTime;

        if (playerSettings && playerSettings.useMinimap)
        {
            if (character.currentMinimapIcon == null && playerSettings.minimapIcon)
            {
                GameObject temp = Instantiate(playerSettings.minimapIcon);
                temp.transform.SetParent(characterUtils.transform);
                temp.transform.localPosition = Vector3.zero;
                temp.transform.localEulerAngles = new Vector3(90, 0, 0);
                character.currentMinimapIcon = temp.GetComponent<SpriteRenderer>();
            }

            if (friendlyAI)
            {
                character.SetUpMinimapIcons(playerSettings.friendlyAiIconScale, playerSettings.friendlyAiIconColor);
            }
            else
            {
                character.SetUpMinimapIcons(playerSettings.enemyIconScale, playerSettings.enemyIconColor);
            }
            
        }
    }

    public void FreezeMove(int active)
    {
        if (active == 0)
        {
            standInPlace = false;
        }
        else if (active == 1)
        {
            standInPlace = true;
        }
    }


    public void SetWaypoints()
    {
        if (waypointRoute)
        {
            waypoints = new Transform[waypointRoute.waypoints.Length];
            timeToWait = waypointRoute.timeToWait;

            for (int i = 0; i < (waypoints.Length); i++)
            {
                waypoints[i] = waypointRoute.waypoints[i];

            }
        }
        else
        {
            if (actualState == AIState.Patrol)
            {
                actualState = AIState.FriendlyFollow;
            }
        }
    }

    public void StopAllActivities()
    {
        ////Debug.Log("Stop Moving");
        stopPlaying = true;
        actualState = AIState.Wander;


        SetFloatToAnimator("Speed", 0.0f);
        SetBoolToAnimator("Aiming", false);
        /*if (charAnimator)
        {
            charAnimator.SetFloat("Speed", 0.0f);
            charAnimator.SetBool("Aiming", false);
        }*/
        
        existingPossibleTargets = new List<GameObject>();
    }

    // Start is called before the first frame update
    void Start()
    {
        //FindPlayers();
    }

    public void FindPlayers()
    {
        //NEW
        //USUAL ENEMY 

        if (!friendlyAI)
        {
            //Debug.Log("Trying to find players" + this.name);
            existingPossibleTargets = new List<GameObject>(GameObject.FindGameObjectsWithTag("Player"));
            //Debug.Log("actual players " + existingPossibleTargets.Count);
            List<GameObject> AIplayers = new List<GameObject>(GameObject.FindGameObjectsWithTag("AIPlayer"));
            /*
            if (AIplayers.Count != 0)
            {
                foreach (GameObject AI in AIplayers)
                {
                    AI.GetComponent<PrAIController>().GetClosestTarget();
                }
                existingPossibleTargets.AddRange(AIplayers);
            }*/
        }
        //FRIENDLY AI
        else if (friendlyAI)
        {
            friends = new List<GameObject>(GameObject.FindGameObjectsWithTag("Player"));

            GetClosestFriends();
            ////Debug.Log("Friends =" + friends.Count);

            existingPossibleTargets = new List<GameObject>(GameObject.FindGameObjectsWithTag("Enemy"));

        }

        GetClosestTarget();
    }

    public void GetClosestFriends()
    {

        ////Debug.Log("GETTING CLOSEST Friends " + gameObject.name);

        foreach (GameObject f in friends)
        {
            if (f != null)
            {
                if (closestFriendDistance > Vector3.Distance(actualSensorPos, f.transform.position))
                {
                    closestFriend = f;

                    ////Debug.Log("FRIEND TRANSFORM =" + closestFriend.name);

                    closestFriendDistance = Vector3.Distance(actualSensorPos, f.transform.position);
                }

            }


        }
        if (friends.Count == 0)
        {
            // //Debug.Log("NO FRIENDS FOUND " + gameObject.name);
        }

    }


    public void GetClosestTarget()
    {
        /*
        if (friendlyAI)
        {
            ////Debug.Log("GETTING CLOSEST ENEMY " + gameObject.name);
        }*/
        if (existingPossibleTargets.Count != 0)
        {
            /* if (friendlyAI)
             {
                 ////Debug.Log("ENEMY AI COUNT =" + existingPossibleTargets.Count);
             }*/
            foreach (GameObject p in existingPossibleTargets)
            {
                if (p != null)
                {
                    ////Debug.Log(p + " " + charTargetTransform);

                    if (playerActualDistance > Vector3.Distance(actualSensorPos, p.transform.position + new Vector3(0.0f, 1.6f, 0.0f)))
                    {
                        charTargetTransform = p.transform;
                        charTargetUtils = p.GetComponent<PrActorUtils>();
                        if (p.transform.parent != null && p.transform.parent.GetComponent<PrAIController>())
                            charTargetConstroller = p.transform.parent.GetComponent<PrAIController>();
                        else
                            charTargetConstroller = null;
                        /*if (friendlyAI)
                        {
                            ////Debug.Log("ENEMY TRANSFORM =" + charTargetTransform.name);
                        }*/
                        playerActualDistance = Vector3.Distance(actualSensorPos, p.transform.position + new Vector3(0.0f, 1.6f, 0.0f));
                    }
                }
            }
        }
        else
        {
            playerActualDistance = 9999.0f;
            charTargetTransform = null;
            actualState = AIState.Patrol;
            ////Debug.Log("NO ENEMIES FOUND " + gameObject.name);
        }
    }

    void OnAnimatorMove()
    {
        if (agent != null && useRootmotion && charAnimator)
            agent.velocity = charAnimator.deltaPosition / Time.deltaTime;
    }


    void RandomHitAnim()
    {
        if (randomHitAnim < hitAnimsMaxTypes)
        {
            randomHitAnim += 1;
        }
        else
        {
            randomHitAnim = 0;
        }

    }
    
    void SetupAIWeapon()
    {
        if (characterInventory.actualWeapon)
        {

            characterInventory.actualWeapon.Player = characterUtils.gameObject;
            characterInventory.actualWeapon.team = team;
            characterInventory.actualWeapon.AIWeapon = true;
            characterInventory.actualWeapon.LaserSight.enabled = false;
            characterInventory.actualWeapon.SetLayer();
            if (characterInventory.actualWeapon.Type == PrWeapon.WT.Melee)
            {
                characterInventory.actualWeapon.MeleeRadius = attackDistance;
                aimingDistance = attackDistance;
            }

            characterInventory.FireRateTimer = characterInventory.actualWeapon.FireRate;

            if (charTargetTransform)
                characterInventory.actualWeapon.AIEnemyTarget = charTargetTransform;

            if (characterInventory.useArmIK)
            {
                if (characterInventory.actualWeapon.gameObject.transform.Find("ArmIK"))
                {
                    ArmIKTarget = characterInventory.actualWeapon.gameObject.transform.Find("ArmIK");
                    if (GetComponent<PrCharacterIK>() == null)
                    {
                        gameObject.AddComponent<PrCharacterIK>();
                        CharacterIKController = GetComponent<PrCharacterIK>();
                    }
                    else if (GetComponent<PrCharacterIK>())
                    {
                        CharacterIKController = GetComponent<PrCharacterIK>();
                    }

                    if (CharacterIKController)
                    {
                        CharacterIKController.leftHandTarget = ArmIKTarget;
                        characterInventory.EnableArmIK(true);
                    }

                }
                else
                {
                    if (CharacterIKController != null)
                        characterInventory.EnableArmIK(false);
                }
            }
        }
    }

    void SetRandomPosVar(Vector3 goal)
    {
        finalGoal = goal + new Vector3(Random.Range(-randomWaypointAccuracy, randomWaypointAccuracy), 0, Random.Range(-randomWaypointAccuracy, randomWaypointAccuracy));
    }

    void SetRandomWanderingPos(Vector3 goal)
    {
        finalGoal = goal + new Vector3(Random.Range(-wanderingRadius, wanderingRadius), 0, Random.Range(-wanderingRadius, wanderingRadius));
    }

    void SetTimeToWait()
    {
        actualTimeToWait = Random.Range(timeToWait * 0.75f, -timeToWait * 0.75f) + timeToWait;
    }

    public void SwitchDebug()
    {
        if (DebugOn)
        {
            DebugOn = false;
        }
        else
        {
            DebugOn = true;
        }

        if (DebugText)
            DebugText.GetComponent<Renderer>().enabled = DebugOn;

    }

    // Update is called once per frame
    public virtual void Update()
    {
        if (!stopPlaying)
        {
            UpdateInput();
            SetState();
            ApplyState();
            ApplyRotation();
            SetDebug();
        }

    }

    public virtual void UpdateInput()
    {
        if (Input.GetKeyUp(KeyCode.F1))
        {
            SwitchDebug();
        }
    }

    public virtual void SetState()
    {
        if (character.ActualHealth <= 0 || character.isDead)
        {
            actualState = AIState.Dead;
            if (character.DestroyOnDead)
            {
                character.destroyOnDeadtimer -= Time.deltaTime;
                if (character.destroyOnDeadtimer <= 0.0f)
                {
                    ////Debug.Log("BodyDestroyed");
                    Destroy(gameObject);

                }
            }
        }
        else
        {
            //Set variables
            if (eyesAndEarTransform)
                actualSensorPos = actualSensorTrans.position;
            else
                actualSensorPos = controlledCharacter.transform.position + new Vector3(0.0f, 1.6f, 0.0f);
            if (!towerDefenseAI) //Generic AI against Player
            {
                if (friendlyAI)
                {
                    GetClosestTarget();
                    GetClosestFriends();
                    if (closestFriend != null)
                        closestFriendDistance = Vector3.Distance(actualSensorPos, closestFriend.transform.position);
                    if (charTargetTransform != null)
                    {
                        ////Debug.Log(charTargetTransform.name + " " + playerActualDistance);
                        CheckPlayerVisibility(lookAngle);
                        if (!playerIsVisible)
                        {
                            charTargetTransform = null;
                            playerActualDistance = 999.0f;
                        }
                    }
                    else
                        actualState = AIState.FriendlyFollow;
                }
                // IF ENEMY
                else
                {
                    if (actualAlarmedTimer > 0.0)
                    {
                        actualAlarmedTimer -= Time.deltaTime;
                    }

                    if (existingPossibleTargets.Count != 0 && character.ActualHealth > 0 && !doNotAttackPlayer)
                    {
                        GetClosestTarget();
                        if (charTargetTransform != null)
                            playerActualDistance = Vector3.Distance(actualSensorPos, charTargetTransform.position + new Vector3(0.0f, 1.6f, 0.0f));

                        if (actualAlarmedTimer > 0.0)
                        {
                            actualState = AIState.CheckingSound;
                        }
                        else if (actualAlarmedTimer <= 0.0 && playerActualDistance <= awarnessDistance)
                        {
                            CheckPlayerVisibility(lookAngle);
                            if (!playerIsVisible)
                            {
                                charTargetTransform = null;
                                playerActualDistance = 999.0f;
                                
                            }
                        }
                        else if (actualAlarmedTimer <= 0.0f || playerActualDistance > awarnessDistance)
                        {
                            if (waypointRoute)
                            {
                                actualState = AIState.Patrol;
                            }
                            else if (useWander)
                            {
                                actualState = AIState.Wander;
                            }
                            else
                            {
                                actualState = AIState.Waiting;
                            }
                            
                        }
                        else if (character.ActualHealth > 0)
                        {
                            if (useWander)
                            {
                                actualState = AIState.Wander;
                            }
                            else
                            {
                                actualState = AIState.Waiting;
                            }
                        }
                    }
                }

            }
            else //TOWER DEFENSE AI DECISIONS
            {
                if (towerDefenseAI)
                {
                    actualState = AIState.TowerDefensePath;
                    if (towerDefenseTarget)
                        playerActualDistance = Vector3.Distance(actualSensorPos, towerDefenseTarget.position + new Vector3(0.0f, 1.6f, 0.0f));
                    else
                        playerActualDistance = 0.0f;
                }
            }

        }
    }
    public virtual void ApplyState()
    {
        switch (actualState)
        {
            case AIState.Patrol:
                if (standInPlace)
                {
                    StopMovement();
                }
                else if (waypoints.Length > 0 && !standInPlace)
                {
                    if (agent.remainingDistance >= 1.0f && !waiting)
                    {
                        if (agent.remainingDistance >= 2.0f)
                            MoveForward(normalSpeed, finalGoal);
                        else
                            MoveForward(aimingSpeed, finalGoal);
                    }
                    else if (!waiting && waitTimer < actualTimeToWait)
                    {
                        waiting = true;

                    }
                    if (waiting)
                    {
                        StopMovement();
                        if (waitTimer < actualTimeToWait)
                            waitTimer += Time.deltaTime;
                        else
                            ChangeWaytpoint();
                    }
                }
                ////Debug.Log("patrolling");
                break;
            
            // 2.2 - Added waiting behavior to stop movement.
            case AIState.Waiting:
                StopMovement();
                break;

            // 2.2 - Added Wander behavior
            case AIState.Wander:

                if (reachedWanderingPosition)
                {
                    currentWanderingWaitTimer -= Time.deltaTime;
                }

                if (currentWanderingWaitTimer > 0.0f)
                {
                    // Wondering in pause, waiting to choose new position
                    StopMovement();
                }
                else
                {
                    if (!newPositionAdded)
                    {
                        // Wandering is ON. Walking to new position.
                        SetWanderingWaypoint();

                        // Check if location is reachable
                        bool check = GetPossiblePosition(finalGoal);
                        if (GetPossiblePosition(finalGoal))
                        {
                            newPositionAdded = true;
                        }
                    }
                    else
                    {
                        // turn and walk there. 
                        if (agent.remainingDistance >= 1.0f)
                        {
                            MoveForward(normalSpeed, finalGoal);
                        }
                        else
                        {
                            resetWandering();
                        }
                    }
                    
                }

                break;

            case AIState.ChasingPlayer:
                if (charTargetTransform)
                {
                    if (standInPlace)
                    {
                        StopMovement();
                    }
                    else
                    {
                        MoveForward(chasingSpeed, charTargetTransform.position);
                    }
                }
                break;

            case AIState.AimingPlayer:

                if (charTargetTransform)
                {
                    if (standInPlace)
                    {
                        StopMovement();
                    }
                    else
                    {
                        MoveForward(aimingSpeed, charTargetTransform.position);
                    }
                    LookToTarget(charTargetTransform.position);
                    AttackTarget();
                }
                break;

            case AIState.Attacking:
                if (charTargetTransform)
                {
                    LookToTarget(charTargetTransform.position);
                    AttackTarget();
                    StopMovement();

                }
                else
                {
                    playerActualDistance = 999.0f;
                    GetClosestTarget();
                }
                // //Debug.Log("Attacking");
                break;
            case AIState.Dead:
                StopMovement();
                ////Debug.Log("Dead");
                break;
            case AIState.CheckingSound:
                if (standInPlace)
                {
                    StopMovement();
                }
                else if (Vector3.Distance(actualSensorPos, lastNoisePos) >= 2.0f)
                {
                    MoveForward(normalSpeed, lastNoisePos);
                }
                else
                {
                    StopMovement();
                }
                CheckPlayerVisibility(lookAngle);
                //  //Debug.Log("Checking noise position!!");
                break;
            case AIState.TowerDefensePath:
                if (waypoints.Length > 0 && !pathEnded)
                {
                    towerDefenseStage = 0;
                    if (agent.remainingDistance >= 1.5f)
                        MoveForward(normalSpeed, finalGoal);
                    else
                        ChangeWaytpoint();
                    ////Debug.Log("Tower Defense Waypoints");
                }
                else if (actualWaypoint == waypoints.Length - 1 && pathEnded)
                {
                    if (playerActualDistance > (attackDistance * 0.8f))
                    {
                        towerDefenseStage = 1;
                        attackPos = Vector3.zero;
                        if (towerDefenseTarget)
                            MoveForward(normalSpeed, towerDefenseTarget.position);

                        ////Debug.Log("Tower Defense Chasing Target");
                    }
                    else
                    {
                        towerDefenseStage = 2;
                        StopMovement();
                        if (towerDefenseTarget)
                        {
                            LookToTarget(towerDefenseTarget.position);
                            AttackTower();
                        }
                        ////Debug.Log("Tower Defense Attacking!!!!");
                    }
                }

                ////Debug.Log("Tower Defense Attack!!!");
                break;
            case AIState.FriendlyFollow:
                if (charAnimator.GetBool("Aiming") == true)
                {
                    SetBoolToAnimator("Aiming", false);
                    //charAnimator.SetBool("Aiming", false);
                }

                if (standInPlace)
                {
                    StopMovement();
                }
                else if (closestFriendDistance >= 4.0f && closestFriend != null)
                {
                    MoveForward(normalSpeed, closestFriend.transform.position);
                }

                else
                {
                    StopMovement();
                }
                CheckPlayerVisibility(lookAngle);
                ////Debug.Log("Dead");
                break;
            default:
                // //Debug.Log("NOTHING");
                break;
        }
    }

    private void resetWandering()
    {
        reachedWanderingPosition = true;
        currentWanderingWaitTimer = wanderingWaitTime;
        newPositionAdded = false;
        StopMovement();
    }

    public virtual void ApplyRotation()
    {
        controlledCharacter.transform.localEulerAngles = new Vector3(0, controlledCharacter.transform.localEulerAngles.y, 0);
        if (agent != null)
        {
            if (character.useTemperature && character.temperature >= 0.05f)
                agent.updateRotation = true;
            else if (character.useTemperature && character.temperature < 0.05f)
                agent.updateRotation = false;
            else if (!character.useTemperature)
                agent.updateRotation = true;
        }
    }
    public virtual void SetDebug()
    {
        if (DebugText && DebugOn)
        {
            DebugText.text = actualState.ToString() + "\n" + "Alarmed= " + Mathf.Round(actualAlarmedTimer) + "\n" + "ForgetPlayer= " + Mathf.Round(actualforgetPlayerTimer);
            if (actualState == AIState.Patrol)
                DebugText.color = Color.white;
            else if (actualState == AIState.ChasingPlayer)
                DebugText.color = Color.green * 3;
            else if (actualState == AIState.AimingPlayer)
                DebugText.color = Color.yellow * 2;
            else if (actualState == AIState.Attacking)
                DebugText.color = Color.red * 2;
            else if (actualState == AIState.CheckingSound)
                DebugText.color = Color.cyan;
            else if (actualState == AIState.Dead)
                DebugText.color = Color.gray;
            else if (actualState == AIState.FriendlyFollow)
                DebugText.color = Color.white;
            if (friendlyAI)
            {
                if (charTargetTransform)
                {
                    float distance = Vector3.Distance(charTargetTransform.position, controlledCharacter.transform.position);
                    DebugText.text = DebugText.text + "\n" + "EnemyDistance= " + Mathf.Round(distance) + "\n" + "actualEnemy= " + charTargetTransform.name + "\n" + "CanAttack= " + canAttack;
                }

                else
                    DebugText.text = DebugText.text + "\n" + "FriendDistance= " + closestFriendDistance;
            }
        }

    }

    void AttackTarget()
    {
        Vector3 targetDir = (charTargetTransform.position + new Vector3(0f, 1.6f, 0f)) - (actualSensorPos);

        float angle = Vector3.Angle(targetDir, controlledCharacter.transform.forward);
        if (angle < attackAngle)
        {
            if (charAnimator != null)
            {
                charAnimator.ResetTrigger("Alert");
                charAnimator.SetTrigger("CancelAlert");
            }
            

            if (characterInventory.actualWeapon && !doNotAttackPlayer)
            {

                if (Time.time >= (characterInventory.LastFireTimer + characterInventory.FireRateTimer))
                {
                    characterInventory.LastFireTimer = Time.time;
                    //Attack Melee
                    if (characterInventory.actualWeapon.Type == PrWeapon.WT.Melee)
                    {
                        UseMeleeWeapon();
                    }
                    //Attack Ranged 
                    else
                    {
                        ShootWeapon();
                    }
                }
            }
        }

    }


    void AttackTower()
    {
        if (towerDefenseTarget)
        {
            Vector3 targetDir = (towerDefenseTarget.position + new Vector3(0f, 1.6f, 0f)) - (actualSensorPos);

            float angle = Vector3.Angle(targetDir, controlledCharacter.transform.forward);
            if (angle < attackAngle)
            {
                if (characterInventory.actualWeapon)
                {
                    if (Time.time >= (characterInventory.LastFireTimer + characterInventory.FireRateTimer))
                    {
                        characterInventory.LastFireTimer = Time.time;
                        //Attack Melee
                        if (characterInventory.actualWeapon.Type == PrWeapon.WT.Melee)
                        {
                            //Debug.LogWarning("Using Weapon " + gameObject.name);
                            UseMeleeWeapon();
                        }
                        //Attack Ranged 
                        else
                        {
                            ShootWeapon();
                        }
                    }
                }
            }
        }

    }

    void ShootWeapon()
    {
        if (canAttack)
        {
            if (charTargetTransform)
                characterInventory.actualWeapon.AIEnemyTarget = charTargetTransform;

            characterInventory.actualWeapon.Shoot();
            if (characterInventory.actualWeapon.Reloading == false)
            {
                if (characterInventory.actualWeapon.ActualBullets > 0)
                    SetTriggerToAnimator("Shoot");
                //charAnimator.SetTrigger("Shoot");
                else if (characterInventory.actualWeapon.ActualBullets > 0 && !charAnimator)
                {
                    characterInventory.actualWeapon.Shoot();
                }
                else
                {
                    characterInventory.actualWeapon.Reload();
                }

            }
        }

    }

    void UseMeleeWeapon()
    {
        if (canAttack)
        {
            standInPlace = true;

            if (charTargetTransform)
                characterInventory.actualWeapon.AIEnemyTarget = charTargetTransform;
            SetTriggerToAnimator("MeleeAttack");
            /*if (charAnimator)
                charAnimator.SetTrigger("MeleeAttack");*/

            if (chooseRandomMeleeAttack)
                SetIntegerToAnimator("MeleeType", Random.Range(0, meleeAttacksOptions));
            //charAnimator.SetInteger("MeleeType", Random.Range(0, meleeAttacksOptions));
            else
            {
                SetIntegerToAnimator("MeleeType", actualMeleeAttack);
                /*if (charAnimator)
                    charAnimator.SetInteger("MeleeType", actualMeleeAttack);*/
                if (actualMeleeAttack < meleeAttacksOptions - 1)
                    actualMeleeAttack += 1;
                else
                    actualMeleeAttack = 0;
            }
        }

    }

    public void MeleeEvent()
    {
        if (!towerDefenseAI && currentTarget != null)
        {
            characterInventory.actualWeapon.AIAttackMelee(charTargetTransform.position, currentTarget.gameObject, attackingVehicle);
        }
        else
        {
            if (towerDefenseTarget)
                characterInventory.actualWeapon.AIAttackMelee(towerDefenseTarget.position, towerDefenseTarget.gameObject, attackingVehicle);
        }
    }

    public void SetCanAttack(bool set)
    {
        canAttack = set;

    }
    public void CanAttack()
    {
        SetCanAttack(true);
        characterInventory.EnableArmIK(true);
    }

    public void CheckPlayerNoise(Vector3 noisePos)
    {
        if (!doNotAttackPlayer && !character.isDead && !towerDefenseAI && !friendlyAI)
        {
            ////Debug.Log("Noise in AI");
            //agent.ResetPath();
            //actualState = AIState.ChasingPlayer;

            Vector3 currentGoal = agent.destination;
            SetWaypoint(noisePos);
            NavMeshPath NoisePath = agent.path;


            if ( agent.CalculatePath(noisePos, NoisePath))
            {
                if (newtAlarm == 0.0f || Time.time >= newtAlarm + 15f)
                {
                    if (actualState == AIState.Patrol)
                    {
                        /*if (charAnimator)
                            charAnimator.SetTrigger("Alert");*/
                        SetTriggerToAnimator("Alert");
                        lastNoisePos = noisePos;
                        newtAlarm = Time.time;

                        actualAlarmedTimer = alarmedTimer;

                        agent.SetDestination(noisePos);
                        ////Debug.Log("Noise in AI OPTION A 1");
                        ////Debug.Log(gameObject.name + " New Noise Position assigned. Position: " + lastNoisePos);

                    }
                }
                else
                {
                    lastNoisePos = noisePos;
                    newtAlarm = Time.time;

                    actualAlarmedTimer = alarmedTimer;

                    agent.SetDestination(noisePos);
                   // //Debug.Log("Noise in AI OPTION A 2");
                    ////Debug.Log(gameObject.name + " New Noise Position assigned");
                }

            }

            else
            {

                ////Debug.Log("Noise in AI OPTION B");
                agent.SetDestination(currentGoal);
                ////Debug.Log(gameObject.name + " Can´t Reach Noise");
                ////Debug.Log("Can´t Reach Noise ");
            }
        }

    }

    void PlayerVisibilityRay(Vector3 targetDir)
    {
        RaycastHit hit;
        int AIInt = 9;
        int objectInt = 8;
        int dynamicsInt = 11;
        int playerInt = 14;
        int vehiclesInt = 18;
        int playerLayer = 1 << playerInt;
        int AILayer = 1 << AIInt;
        int objectLayer = 1 << objectInt;
        int dynamicsLayer = 1 << dynamicsInt;
        int vehiclesLayer = 1 << vehiclesInt;

        int finalMask = objectLayer | dynamicsLayer | AILayer | playerLayer;// | vehiclesLayer;

        if (!friendlyAI)
        {
            if (Physics.Raycast(actualSensorPos, targetDir, out hit, awarnessDistance, finalMask))
            {
                Debug.DrawRay(actualSensorPos, targetDir * awarnessDistance, Color.magenta);

                if (hit.collider.CompareTag("Player") || hit.collider.CompareTag("AIPlayer") )
                {
                    currentTarget = hit.collider.transform;
                    //Debug.Log("Seeing Player " + hit.collider.name);
                    attackingVehicle = false;
                    playerIsVisible = true;
                    actualAlarmedTimer = 0.0f;
                    newtAlarm = 0.0f;
                    
                    
                }
                else if (hit.collider.CompareTag("Vehicles"))
                {
                    if (hit.collider.GetComponent<PrVehicle>().driver != null)
                    {
                        currentTarget = hit.collider.transform;
                        attackingVehicle = true;
                        //Debug.Log("Hitting Vehicle " + hit.collider.name);
                        playerIsVisible = true;
                        actualAlarmedTimer = 0.0f;
                        newtAlarm = 0.0f;
                    }
                    else
                    {
                        if (charTargetTransform)
                            currentTarget = charTargetTransform;
                        else
                            currentTarget = null;
                        attackingVehicle = false;
                        //Debug.Log("Can´t see Vehicle");
                        playerIsVisible = false;
                        actualState = AIState.Patrol;
                    }
                }
                else 
                {
                    if (charTargetTransform)
                        currentTarget = charTargetTransform;
                    else
                        currentTarget = null;
                    attackingVehicle = false;
                    //Debug.Log("Can´t see Player" + hit.collider.name + hit.collider.tag);
                    playerIsVisible = false;
                    actualState = AIState.Patrol;
                }
            }
        }
        else if (friendlyAI)
        {
            if (Physics.Raycast(actualSensorPos, targetDir, out hit, awarnessDistance, finalMask))
            {
                if (hit.collider.CompareTag("Enemy"))
                {

                    ////Debug.Log("Seeing Enemy " + player.transform.position);
                    //Debug.DrawRay(actualSensorPos, targetDir, Color.green);
                    if (hit.collider.GetComponent<PrActorUtils>().character.isDead == true)
                    {
                        currentTarget = null;
                        playerIsVisible = false;
                        actualState = AIState.FriendlyFollow;
                    }
                    else
                    {
                        currentTarget = charTargetTransform;
                        playerIsVisible = true;
                        actualAlarmedTimer = 0.0f;
                        newtAlarm = 0.0f;
                    }
                }
                else 
                {
                    ////Debug.Log("Can´t see Player");
                    currentTarget = null;
                    playerIsVisible = false;
                    actualState = AIState.FriendlyFollow;

                }
            }
        }

    }

    public void CheckPlayerVisibility(float actualLookAngle)
    {
        if (charTargetTransform != null)
        {
            ////Debug.Log(actualLookAngle);

            Vector3 targetDir = (charTargetTransform.position + new Vector3(0f, 0.5f, 0f)) - (actualSensorPos);

            float angle = Vector3.Angle(targetDir, controlledCharacter.transform.forward);

            if (angle < actualLookAngle && !doNotAttackPlayer && !character.isDead)
            {
                if (Time.time >= playerLastTimeSeen)
                {
                    PlayerVisibilityRay(targetDir);
                    playerLastTimeSeen = Time.time + 0.1f;
                }

                ////Debug.Log(actualLookAngle + " " + charTargetTransform + " " + playerIsVisible);

                if (playerIsVisible)
                {
                    //LookToTarget(charTargetTransform.position);
                    playerActualDistance = Vector3.Distance(actualSensorPos, charTargetTransform.position + new Vector3(0.0f, 1.6f, 0.0f));

                    if (playerActualDistance > aimingDistance)
                    {
                        actualState = AIState.ChasingPlayer;
                        SetBoolToAnimator("Aiming", false);
                       /* if (charAnimator)
                            charAnimator.SetBool("Aiming", false);*/
                    }
                    else if (playerActualDistance <= aimingDistance)
                    {
                        if (playerActualDistance <= attackDistance)
                        {
                            if (!friendlyAI)
                            {
                                if (charTargetTransform && charTargetUtils)
                                {
                                    if (charTargetUtils.character.isDead == false)
                                        actualState = AIState.Attacking;
                                    else
                                    {
                                        FindPlayers();
                                        actualState = AIState.Patrol;
                                    }

                                }
                                /*
                                else if (charTargetTransform && charTargetTransform.GetComponent<PrAIController>())
                                {
                                    if (charTargetTransform.GetComponent<PrAIController>().character.isDead == false)
                                        actualState = AIState.Attacking;
                                    else
                                    {
                                        FindPlayers();
                                        actualState = AIState.Patrol;
                                    }

                                }*/
                            }
                            else
                            {
                                if (charTargetTransform && charTargetConstroller && charTargetConstroller.character.isDead == false)
                                    actualState = AIState.Attacking;
                                else
                                {
                                    FindPlayers();
                                    actualState = AIState.FriendlyFollow;
                                }
                            }
                        }
                        else
                        {
                            if (!friendlyAI)
                            {
                                if (charTargetTransform && charTargetUtils)
                                {
                                    if (charTargetUtils.character.isDead == false)
                                        actualState = AIState.AimingPlayer;
                                    else
                                    {
                                        FindPlayers();
                                        actualState = AIState.Patrol;
                                    }
                                }
                                /*
                                else if (charTargetTransform && charTargetTransform.GetComponent<PrAIController>())
                                {
                                    if (charTargetTransform.GetComponent<PrAIController>().character.isDead == false)
                                        actualState = AIState.AimingPlayer;
                                    else
                                    {
                                        FindPlayers();
                                        actualState = AIState.Patrol;
                                    }
                                }*/
                                else
                                    actualState = AIState.Patrol;
                            }
                            else
                            {
                                if (charTargetTransform && charTargetConstroller && charTargetConstroller.character.isDead == false)
                                    actualState = AIState.AimingPlayer;
                                else
                                {
                                    FindPlayers();
                                    actualState = AIState.FriendlyFollow;
                                }

                            }

                        }
                        /*if (charAnimator)
                            charAnimator.SetBool("Aiming", true);*/
                        SetBoolToAnimator("Aiming", true);
                    }
                    //}

                }
            }
            else if (actualAlarmedTimer > 0.0f && !friendlyAI)
            {
                actualState = AIState.CheckingSound;
                SetBoolToAnimator("Aiming", false);
                /*if (charAnimator)
                    charAnimator.SetBool("Aiming", false);*/
            }
            else if (!friendlyAI)
            {
                actualState = AIState.Patrol;
                SetBoolToAnimator("Aiming", false);
                /*if (charAnimator)
                    charAnimator.SetBool("Aiming", false);*/
            }/*
            else if (friendlyAI)
            {
                actualState = AIState.FriendlyFollow;
            }*/
        }

    }

    void StopMovement()
    {
        if (agent != null)
        {
            if (!towerDefenseAI)
            {
                agent.velocity = Vector3.zero;
                SetFloatToAnimator("Speed", 0.0f, 0.1f, Time.deltaTime);
                /*if (charAnimator)
                    charAnimator.SetFloat("Speed", 0.0f, 0.5f, Time.deltaTime);*/
            }
            else
            {
                if (attackPos == Vector3.zero)
                    attackPos = controlledCharacter.transform.position;
                controlledCharacter.transform.position = attackPos;

                agent.velocity = Vector3.zero;
                SetFloatToAnimator("Speed", 0.0f);
                /*if (charAnimator)
                    charAnimator.SetFloat("Speed", 0.0f);*/
            }
        }

    }

    void ChangeWaytpoint()
    {
        waiting = false;
        if (!waypointPingPong && !towerDefenseAI) //Unidirectional Waypoint
        {
            if (actualWaypoint < waypoints.Length - 1)
                actualWaypoint = actualWaypoint + 1;
            else
                actualWaypoint = 0;
        }
        else if (waypointPingPong && !towerDefenseAI)//Ping pong waypoints
        {
            if (!inverseDirection)
            {
                if (actualWaypoint < waypoints.Length - 1)
                    actualWaypoint = actualWaypoint + 1;
                else
                {
                    inverseDirection = true;
                    actualWaypoint = actualWaypoint - 1;
                }
            }
            else
            {
                if (actualWaypoint > 0)
                    actualWaypoint = actualWaypoint - 1;
                else
                {
                    inverseDirection = false;
                    actualWaypoint = 1;
                }
            }
        }
        else if (towerDefenseAI)
        {
            if (actualWaypoint < waypoints.Length - 1)
            {
                actualWaypoint = actualWaypoint + 1;
                ////Debug.Log("ActualWaypoint =" + actualWaypoint);
            }
            else
                pathEnded = true;
        }

        waitTimer = 0.0f;
        SetTimeToWait();
        SetWaypoint(waypoints[actualWaypoint].transform.position);

    }

    public void SetWaypoint(Vector3 Pos)
    {
        SetRandomPosVar(Pos);
        agent.SetDestination(finalGoal);
    }

    Vector3 GetRandomWanderingPosition()
    {
        Vector3 AIPos = character.transform.position;

        Vector3 possiblePosition = AIPos + new Vector3(Random.Range(-wanderingRadius, wanderingRadius), 0, Random.Range(-wanderingRadius, wanderingRadius));

        return possiblePosition;
    }

    bool GetPossiblePosition(Vector3 position)
    {
        bool isPossible = false;

        RaycastHit hit;
        Debug.DrawRay(position + new Vector3(0,2,0), Vector3.down, Color.green, 3.0f);
        if (Physics.Raycast(position + new Vector3(0, 2, 0), Vector3.down, out hit, 10.0f))
        {
            // Check if it´s possible to get there
            NavMeshPath path = new NavMeshPath();
            isPossible = agent.CalculatePath(position, path);
        }

        return isPossible;
    }

    public void SetWanderingWaypoint()
    {
        finalGoal = GetRandomWanderingPosition();
        agent.SetDestination(finalGoal);
        reachedWanderingPosition = false;
    }

    void SetIntegerToAnimator(string parameter, int value)
    {
        if (charAnimator != null)
            charAnimator.SetInteger(parameter, value);
    }

    void SetFloatToAnimator(string parameter, float value, float time, float speed)
    {
        if (charAnimator != null)
            charAnimator.SetFloat(parameter, value, time, speed);
    }

    void SetFloatToAnimator(string parameter, float value)
    {
        if (charAnimator != null)
            charAnimator.SetFloat(parameter, value);
    }
    void SetBoolToAnimator(string parameter, bool value)
    {
        if (charAnimator != null)
            ////Debug.Log("-----------" + charAnimator + "-----------------");
            charAnimator.SetBool(parameter, value);
    }
    void SetTriggerToAnimator(string parameter)
    {
        if (charAnimator != null)
            charAnimator.SetTrigger(parameter);
    }

    public void MoveForward(float speed, Vector3 goal)
    {
        if (useRootmotion)
        {
            agent.destination = goal;
            agent.speed = 0.05f;

            SetFloatToAnimator("Speed", speed, 0.2f, Time.deltaTime);
            /*if (charAnimator)
                charAnimator.SetFloat("Speed", speed , 0.4f, Time.deltaTime);*/
            if (character.useTemperature)
            {
                //if (temperature > 1.0f)
                //    charAnimator.SetFloat("Temperature", 2 - Mathf.Clamp(temperature, 0.0f, onFireSpeedFactor));
                //else if (temperature <= 1.0f)
                //    charAnimator.SetFloat("Temperature", Mathf.Clamp(temperature, 0.0f, onFireSpeedFactor));
                    
                if (character.temperature <= 1.0f)
                    SetFloatToAnimator("Temperature", Mathf.Clamp(character.temperature, 0.0f, character.onFireSpeedFactor));
                    //charAnimator.SetFloat("Temperature", Mathf.Clamp(character.temperature, 0.0f, character.onFireSpeedFactor));
            }

        }
        else
        {
            ////Debug.Log("Moving Forward");
            agent.destination = goal;
            if (character.useTemperature)
            {
                if (character.temperature <= 1.0f)
                {
                    agent.speed = speed  * Mathf.Clamp(character.temperature, 0.0f, 1.0f);
                    SetFloatToAnimator("Speed", (1 / chasingSpeed) * (speed * Mathf.Clamp(character.temperature, 0.5f, 1.0f)), 0.2f, Time.deltaTime);
                    //charAnimator.SetFloat("Speed", speed * Mathf.Clamp(character.temperature, 0.5f, 1.0f), 0.25f, Time.deltaTime);
                }
                /*
                //else if (temperature > 1.0f)
                //{
                //    float tempExtra = Mathf.Clamp(temperature - 1.0f,1.0f,2.0f) * onFireSpeedFactor;
                //    //Debug.Log(tempExtra);
                //    agent.speed = (speed + 0.8f) * tempExtra;
                //   charAnimator.SetFloat("Speed", speed * tempExtra, 0.25f, Time.deltaTime);
                //}
                */
                //charAnimator.SetFloat("Temperature", Mathf.Clamp(character.temperature, 0.0f, 1.0f));
                SetFloatToAnimator("Temperature", Mathf.Clamp(character.temperature, 0.0f, 1.0f));
            }
            
            else
            {
                agent.speed = speed ;
                SetFloatToAnimator("Speed", (1 / chasingSpeed) * speed, 0.2f, Time.deltaTime);
                //charAnimator.SetFloat("Speed", speed, 0.25f, Time.deltaTime);
            }


        }

    }

    void LookToTarget(Vector3 target)
    {

        Quaternion targetRot = Quaternion.LookRotation(target - controlledCharacter.transform.position);
        if (character.useTemperature)
            rotationSpeed *= Mathf.Clamp(character.temperature, 0.0f, 1.0f);
        controlledCharacter.transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed);

    }

    void OnTriggerEnter(Collider other)
    {

        if (other.tag == "Noise")
        {
            CheckPlayerNoise(other.transform.position);
        }


    }

    public void GetCLoserWaypoint()
    {

        int selected = 0;
        float selDist = 999f;
        float dist = 0.0f;
        bool changeWayp = false;
        if (waypoints.Length > 0)
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                dist = agent.remainingDistance;

                if (dist <= selDist)
                {
                    selDist = dist;

                    actualWaypoint = selected;

                    changeWayp = true;
                }
                selected += 1;
            }
            if (changeWayp)
            {
                ChangeWaytpoint();
            }
            else
            {
                SetWaypoint(waypoints[0].position);

            }
        }
    }

    void LateUpdate()
    {
        if (lockRotation)
        {
            controlledCharacter.transform.rotation = Quaternion.Euler(lockedRotDir);
        }
    }

    public void EndMelee()
    {
        standInPlace = false;
    }


    public virtual void OnDrawGizmos()
    {
        if (!controlledCharacter)
        {
            controlledCharacter = this.gameObject;
        }

        if (friendlyAI && characterInventory && characterInventory.actualWeapon.Type != PrWeapon.WT.Melee && charTargetTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawCube(charTargetTransform.position + Vector3.up, Vector3.one * 0.5f);
        }

        Gizmos.color = Color.white;
        
        if (charTargetTransform && playerIsVisible)
        {
            if (eyesAndEarTransform)
                Gizmos.DrawLine(charTargetTransform.position + new Vector3(0, eyesAndEarTransform.position.y, 0), eyesAndEarTransform.position);
            else
                Gizmos.DrawLine(charTargetTransform.position + new Vector3(0f, 1.6f, 0f), controlledCharacter.transform.position + new Vector3(0f, 1.6f, 0f));
        }
        
        Quaternion lRayRot = Quaternion.AngleAxis(-lookAngle * 0.5f, Vector3.up);
        Quaternion rRayRot = Quaternion.AngleAxis(lookAngle * 0.5f, Vector3.up);
        Vector3 lRayDir = lRayRot * controlledCharacter.transform.forward;
        Vector3 rRayDir = rRayRot * controlledCharacter.transform.forward;

        if (eyesAndEarTransform)
        {
            Gizmos.DrawRay(eyesAndEarTransform.position, lRayDir * awarnessDistance);
            Gizmos.DrawRay(eyesAndEarTransform.position, rRayDir * awarnessDistance);
        }
        else
        {
            Gizmos.DrawRay(controlledCharacter.transform.position + new Vector3(0f, 1.6f, 0f), lRayDir * awarnessDistance);
            Gizmos.DrawRay(controlledCharacter.transform.position + new Vector3(0f, 1.6f, 0f), rRayDir * awarnessDistance);
        }

        Gizmos.DrawMesh(AreaMesh, controlledCharacter.transform.position, Quaternion.identity, Vector3.one * awarnessDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawMesh(AreaMesh, controlledCharacter.transform.position, Quaternion.identity, Vector3.one * aimingDistance);
        Gizmos.DrawWireSphere(lastNoisePos, 1.0f);

        Gizmos.color = Color.red;
        Gizmos.DrawMesh(AreaMesh, controlledCharacter.transform.position, Quaternion.identity, Vector3.one * attackDistance);

        Gizmos.color = Color.blue;
        Gizmos.DrawMesh(AreaMesh, controlledCharacter.transform.position, Quaternion.identity, Vector3.one * hearingDistance);

        Gizmos.color = Color.magenta;
        Gizmos.DrawMesh(TargetArrow, finalGoal, Quaternion.identity, Vector3.one);

        if (character && character.useRagdollDeath && character.isDead)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(character.LastHitPos + new Vector3(0f, 1.6f, 0f), 0.1f);
            Gizmos.DrawRay(character.LastHitPos + new Vector3(0f, 1.6f, 0f), character.ragdollDirection);

        }
    }
}
