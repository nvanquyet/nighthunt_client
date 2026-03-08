using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrVehicleController : MonoBehaviour
{
    [HideInInspector]
    public string[] playerCtrlMap = {"Horizontal", "Vertical", "LookX", "LookY","FireTrigger", "Reload",
        "EquipWeapon", "Sprint", "Aim", "ChangeWTrigger", "Roll", "Use", "Crouch", "ChangeWeapon", "Throw"  ,"Fire", "Mouse ScrollWheel", "Melee"};

    [Header("Basic Parameters")]

    public GameObject vehicle;
    [HideInInspector]
    public PrVehicle actualVehicle;
    private GameObject actualVisualVehicle;
    public GameObject playerSelection;
    public float playerSelectionScale = 1.0f;
    [HideInInspector]
    public Renderer currentPlayerSelection;

    [Header("Movement")]
    public bool controlON = false;
    public bool orientToGround = true;
    public PrCharacterController driverController;
    public GameObject driver;

    private bool VelocityMode = true;
    public float velocity = 0.0f;
    public float acceleration = 1.0f;
    public float forceFactor = 1.0f;
    public float maxVelocity = 100.0f;
    public float rotation = 0.0f;
    private float oldRotation = 0.0f;
    public float steerAcceleration = 1.0f;
    public float rotationSpeed = 0.0f;
    public float heightRotBlendSpeed = 3.0f;

    private Transform frontBbox;
    private Transform backBbox;


    [Header("Turret Parameters")]
    public float turretRotSpeed = 1.0f;
    [HideInInspector]
    Transform turretRot;
    private float lastShoot = 0.0f;

    [Header("Weapon Parameters")]
    public bool canShoot = true;
    public GameObject aimTarget;
    [HideInInspector]
    public GameObject actualAimTarget;
    private Vector3 aimRotation;

    private Vector3 tiltRotations = Vector3.zero;

    Vector3 vehicleFinalHeight;
    Vector3 floorPos;
    private AudioSource vehicleEngine;
    private int playingEngineAudio = 0;

    private float h = 0.0f;
    private float v = 0.0f;

    RaycastHit hit;
    // Start is called before the first frame update
    void Awake()
    {
        if (aimTarget)
        {
            actualAimTarget = PrUtils.InstantiateActor(aimTarget, transform.position, Quaternion.identity, this.name + "AimTarget", this.transform);
        }

        CreateVehicle();

        if (actualVehicle.turret)
        {
            turretRot = new GameObject("turretRot").transform;
            turretRot.SetParent(actualVehicle.turret.parent.transform);
            turretRot.rotation = actualVehicle.turret.rotation;
            turretRot.position = actualVehicle.turret.position;
        }

        SetOnOff(actualVehicle.turnedOn);
    }

    private void CreateVehicle()
    {
        actualVehicle = PrUtils.InstantiateActor(vehicle, transform.position + new Vector3(0.0f,vehicle.GetComponent<PrVehicle>().vehicleOffHeight,0.0f), transform.rotation, vehicle.GetComponent<PrVehicle>().Name, this.transform).GetComponent<PrVehicle>();
        actualVisualVehicle = actualVehicle.visualVehicle;
        actualVehicle.m_rigidbody = actualVehicle.GetComponent<Rigidbody>();
        actualVehicle.controller = this;
        actualVehicle.gameObject.layer = LayerMask.NameToLayer("Vehicles");
        vehicleEngine = actualVehicle.gameObject.GetComponent<AudioSource>();
        vehicleEngine.clip = actualVehicle.engineIdle; 

        frontBbox = new GameObject(name + "fBbox").transform;
        backBbox = new GameObject(name + "bBbox").transform;

        frontBbox.SetParent(actualVisualVehicle.transform);
        backBbox.SetParent(actualVisualVehicle.transform);
        //Debug.Log(vehicle.GetComponent<BoxCollider>().size.z);
        frontBbox.position = new Vector3(transform.position.x, transform.position.y + 1.0f, transform.position.z + vehicle.GetComponent<BoxCollider>().size.z / 2.5f);
        backBbox.position = new Vector3(transform.position.x, transform.position.y + 1.0f, transform.position.z - vehicle.GetComponent<BoxCollider>().size.z / 2.5f);

        if (playerSelection)
        {
            currentPlayerSelection = PrUtils.InstantiateActor(playerSelection, transform.position + (Vector3.one * 0.1f), playerSelection.transform.rotation, "VehicleSelection", this.transform).GetComponent<Renderer>();
            currentPlayerSelection.gameObject.transform.localScale = Vector3.one * playerSelectionScale;
            currentPlayerSelection.gameObject.AddComponent<PrCopyPosition>();
            currentPlayerSelection.gameObject.GetComponent<PrCopyPosition>().targetObject = actualVisualVehicle.transform;
            currentPlayerSelection.gameObject.GetComponent<PrCopyPosition>().floorOffset = 0.1f;
            currentPlayerSelection.enabled = false;
        }
    }

    private void UpdateTurret()
    {

        if (actualAimTarget && actualVehicle.turret)
        {
            if (driverController && driverController.JoystickEnabled)
            {

                driverController.JoystickTarget.transform.rotation = actualVehicle.transform.rotation;

                //Joystick Look input
                float LookX = Input.GetAxis(playerCtrlMap[2]);
                float LookY = Input.GetAxis(playerCtrlMap[3]);

                Vector3 JoystickLookVec = new Vector3(LookX, 0, LookY) * 10;

                JoystickLookVec = Quaternion.Euler(0, 0 + driverController.m_Cam.transform.parent.transform.eulerAngles.y, 0) * JoystickLookVec;

                driverController.JoystickTarget.transform.position = actualVehicle.transform.position + JoystickLookVec * 5;

                if (Mathf.Abs(LookX) <= 0.2f && Mathf.Abs(LookY) <= 0.2f)
                {
                    driverController.JoystickTarget.transform.localPosition += driverController.JoystickTarget.transform.forward * 2;
                }

                driverController.JoystickLookRot.transform.LookAt(driverController.JoystickTarget.transform.position);

                driverController.AimTargetVisual.transform.position = driverController.JoystickTarget.transform.position;
                driverController.AimTargetVisual.transform.LookAt(transform.position);

                actualVehicle.turret.transform.rotation = Quaternion.Lerp(actualVehicle.turret.transform.rotation, driverController.AimTargetVisual.transform.rotation, Time.deltaTime * turretRotSpeed);
                actualVehicle.turret.transform.localEulerAngles = new Vector3(0, actualVehicle.turret.transform.localEulerAngles.y, 0);

            }
            else
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit2;
                if (Physics.Raycast(ray, out hit2, 2000f, 9))
                {
                    ////Debug.Log("---------- Hit Something------------");
                    Vector3 FinalPos = new Vector3(hit2.point.x, 0, hit2.point.z);

                    actualAimTarget.transform.position = FinalPos;
                    actualAimTarget.transform.LookAt(actualVehicle.turret.position);

                    turretRot.LookAt(actualAimTarget.transform.position);

                    actualVehicle.turret.transform.rotation = Quaternion.Lerp(actualVehicle.turret.rotation, turretRot.rotation, Time.deltaTime * turretRotSpeed);
                    actualVehicle.turret.transform.localEulerAngles = new Vector3(0.0f, actualVehicle.turret.transform.localEulerAngles.y, 0.0f);
                }
            }
            
        }
        
    }


    public void UpdateWheels()
    {
        if (actualVehicle.rotateWheels)
        {
            if (actualVehicle.rotWheels.Length > 0)
            {
                foreach (Transform w in actualVehicle.rotWheels)
                {
                    w.localEulerAngles = new Vector3(actualVehicle.wheelsXRot, 0.0f, 0.0f);
                    w.position = rayCastOffset(w.parent.position + new Vector3(0.0f, 0.5f, 0.0f), actualVehicle.wheelsSize * 0.5f);

                }

            }
            if (actualVehicle.staticWheels.Length > 0)
            {
                foreach (Transform w in actualVehicle.staticWheels)
                {
                    w.localEulerAngles = new Vector3(actualVehicle.wheelsXRot, 0.0f, 0.0f);
                    w.position = rayCastOffset(w.parent.position + new Vector3(0.0f, 0.5f, 0.0f), actualVehicle.wheelsSize * 0.5f);

                }

            }
        }
        if (actualVehicle.wheelsPivots.Length > 0)
        {
            foreach (Transform p in actualVehicle.wheelsPivots)
            {
                p.localEulerAngles = new Vector3(0.0f, actualVehicle.wheelsYRot, 0.0f);
                p.eulerAngles = new Vector3(0.0f, p.eulerAngles.y, 0.0f);
            }
        }
    }



    Vector3 rayCastOffset(Vector3 initPos, float offset)
    {
        RaycastHit h;
        Vector3 dist = initPos;
        if (Physics.Raycast(initPos, Vector3.down, out h, 100, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
        {
            dist = h.point;
            dist.y += offset;
        }

        return dist;
    }


    void SetOnOff(bool active)
    {
        actualVehicle.turnedOn = active;
        if (actualVehicle.lights)
            actualVehicle.lights.SetActive(active);
        if (actualAimTarget)
            actualAimTarget.SetActive(active);
        if (!active)
        {
            PlayEngineAudio(0, false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdateWheels();

        if (controlON && !actualVehicle.isDead)
        {
            UpdatebasicInputs();
        }

    }

    void FixedUpdate()
    {
        if (controlON)
        {
            UpdatePassengers();
        }

        if (actualVehicle.turnedOn && controlON)
        {
            UpdateInputs();

            UpdateRotation();

            UpdateHeight();

            UpdateTurret();
        }
        else
        {
            UpdateInertia();
            UpdateOffHeight();
            UpdateRotation();
        }
    }

    void UpdatePassengers()
    {
        if (actualVehicle.showDriver && driver)
        {
            driver.transform.position = actualVehicle.driverLocation.position;
            driver.transform.rotation = actualVehicle.driverLocation.rotation;
        }
        else if (!actualVehicle.showDriver && driver)
        {
            driver.SetActive(false);
        }
    }

    void UpdateRotation()
    {
        if (actualVehicle.visualVehicle)
        {
            actualVehicle.visualVehicle.transform.localEulerAngles = new Vector3(-velocity * actualVehicle.tiltFrontFactor, 0.0f, rotationSpeed  * (actualVehicle.tiltSideFactor *velocity));
        }

        if (orientToGround)
        {

            RaycastHit hit;
                
            if (Physics.Raycast(frontBbox.position + Vector3.up, Vector3.down, out hit, actualVehicle.actualHeight * 4, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
            {
                
                Quaternion orientRot = Quaternion.LookRotation(transform.forward * 1.1f, hit.normal);

                transform.rotation = Quaternion.Lerp(transform.rotation, orientRot, Time.deltaTime * heightRotBlendSpeed);
            }

        }

    }

    void UpdateHeight()
    {
        actualVehicle.actualHeight = actualVehicle.vehicleHeight + actualVehicle.heightAnim.Evaluate(Time.time * actualVehicle.heightAnimSpeed);
        float average = 0.0f;
        //Debug.DrawRay(frontBbox.position, Vector3.down * actualVehicle.actualHeight * 4, Color.red);
        if (Physics.Raycast(frontBbox.position, Vector3.down, out hit, actualVehicle.actualHeight * 4, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
        {
            Vector3 hitPos = hit.point;
            //Debug.DrawRay(hit.point, hit.normal * 10.0f, Color.green);
           // Debug.Log(hit.collider.name);
            average = actualVehicle.vehicleHeight + hitPos.y;
        }
        //Debug.DrawRay(backBbox.position, Vector3.down * actualVehicle.actualHeight * 4, Color.red);
        if (Physics.Raycast(backBbox.position, Vector3.down, out hit, actualVehicle.actualHeight * 4, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
        {
            Vector3 hitPos = hit.point;
           // Debug.DrawRay(hit.point, hit.normal * 10.0f, Color.green);
            //Debug.Log(hit.collider.name);

            average = average + (hitPos.y + actualVehicle.vehicleHeight);
        }
        vehicleFinalHeight = new Vector3(actualVehicle.transform.position.x, (average/2), actualVehicle.transform.position.z);
        actualVehicle.transform.position = Vector3.Lerp(actualVehicle.transform.position, vehicleFinalHeight, Time.deltaTime * heightRotBlendSpeed);
    }

    public void UpdateOffHeight()
    {
        actualVehicle.transform.position = Vector3.Lerp(actualVehicle.transform.position, rayCastOffset(actualVehicle.transform.position, actualVehicle.vehicleOffHeight), Time.deltaTime * 2);
    }

    void UpdatebasicInputs()
    {
        if (Input.GetButtonDown(playerCtrlMap[5]))
        {
            Debug.Log("pressing Vehicle ON Off");
            if (actualVehicle.turnedOn)
                SetOnOff(false);
            else
                SetOnOff(true);
        }

        //Get Outside Vehicle
        if (Input.GetButtonDown(playerCtrlMap[11]) && driverController)
        {
            //Debug.Log(driverController);
            driverController.GetOutsideVehicle();
        }
    }

    void UpdateInertia()
    {
        h = Input.GetAxis(playerCtrlMap[0]) * v;
        v = 0.0f;
        
        if (VelocityMode)
            UpdateMovement();
        else
            UpdateForceMovement();
    }
    
    void UpdateMovement()
    {
        float wheelH = h;
        if (v < 0.0f)
        {
            wheelH *= -1;
        }

        velocity = velocity + (v * Time.deltaTime * acceleration);
        velocity = Mathf.Clamp(velocity, -maxVelocity, maxVelocity);
        actualVehicle.wheelsXRot += velocity;

        oldRotation = rotation;

        rotation = rotation + (wheelH * steerAcceleration) * Mathf.Abs(Mathf.Clamp(velocity * 0.5f, -1.0f, 1.0f));

        actualVehicle.wheelsYRot = Mathf.Lerp(actualVehicle.wheelsYRot, h * actualVehicle.maxWheelTurn, Time.deltaTime * actualVehicle.tiltRotationsSpeed * 2);

        rotationSpeed = Mathf.Lerp(rotationSpeed, rotation - oldRotation, Time.deltaTime * actualVehicle.tiltRotationsSpeed);
        actualVehicle.m_rigidbody.linearVelocity = (actualVehicle.transform.forward * velocity) + (Physics.gravity * Time.deltaTime);

        Vector3 actualRotation = actualVehicle.transform.localRotation * actualVehicle.transform.forward;
        actualVehicle.transform.localRotation = Quaternion.Euler(new Vector3(actualRotation.x, rotation, actualRotation.z));

        if (velocity != 0.0f)
        {
            if (Mathf.Abs(v) < 0.1f)
                velocity = Mathf.Lerp(velocity, 0.0f, Time.deltaTime);

            if (Mathf.Abs(velocity) < 0.25f && Mathf.Abs(v) <= 0.1f)
            {
                velocity = 0.0f;
            }
            
        }

    }
  
    void UpdateForceMovement()
    {
        float wheelH = h;
        if (v < 0.0f)
        {
            wheelH *= -1;
        }

        velocity = actualVehicle.m_rigidbody.linearVelocity.magnitude;

        actualVehicle.wheelsXRot += actualVehicle.m_rigidbody.linearVelocity.magnitude * v;

        oldRotation = rotation;

        rotation = rotation + (wheelH * steerAcceleration) * ( Mathf.Abs(Mathf.Clamp(velocity * 0.5f, -1.0f, 1.0f))) * v;

        actualVehicle.wheelsYRot = Mathf.Lerp(actualVehicle.wheelsYRot, h * actualVehicle.maxWheelTurn, Time.deltaTime * actualVehicle.tiltRotationsSpeed * steerAcceleration);

        rotationSpeed = Mathf.Lerp(rotationSpeed, rotation - oldRotation, Time.deltaTime * actualVehicle.tiltRotationsSpeed);

        if (actualVehicle.wheelsPivots.Length > 0)
            actualVehicle.m_rigidbody.AddForce(actualVehicle.wheelsPivots[0].forward * v * forceFactor, ForceMode.VelocityChange);
        else
            actualVehicle.m_rigidbody.AddForce(actualVehicle.transform.forward * v * forceFactor, ForceMode.VelocityChange);

        Vector3 actualRotation = actualVehicle.transform.localRotation * actualVehicle.transform.forward;
        actualVehicle.transform.localRotation = Quaternion.Euler(new Vector3(actualRotation.x, rotation, actualRotation.z));
        
        if (velocity != 0.0f)
        {
            velocity = Mathf.Lerp(velocity, 0.0f, Time.deltaTime);
        }
    }

    void Shoot()
    {
        actualVehicle.actualWeapon.Shoot();
        lastShoot = Time.time;
    }

    void PlayEngineAudio(int playType, bool playStop)
    {
        playingEngineAudio = playType;
        vehicleEngine.loop = true;
        if (playType == 1)
            vehicleEngine.clip = actualVehicle.engineIdle;
        else if (playType == 2)
            vehicleEngine.clip = actualVehicle.engineRunning;
        if (playStop)
            vehicleEngine.Play();
        else vehicleEngine.Stop();
    }

    void UpdateInputs()
    {
            
        h = Input.GetAxis(playerCtrlMap[0]);
        v = Input.GetAxis(playerCtrlMap[1]);

        if (driverController.JoystickEnabled)
        {
            h = Input.GetAxisRaw(playerCtrlMap[0]);
            v = Input.GetAxisRaw(playerCtrlMap[1]);
        }
        
        if (Mathf.Abs(v) > 0.1f && playingEngineAudio != 2)
        {
            PlayEngineAudio(2, true);
        }
        else if (Mathf.Abs(v) < 0.01f && playingEngineAudio != 1)
        {
            PlayEngineAudio(1, true);
        }

        if (VelocityMode)
            UpdateMovement();
        else
            UpdateForceMovement();

        

        if (canShoot && actualVehicle.actualWeapon && actualAimTarget)
        {
            if (Input.GetButtonDown(playerCtrlMap[15]) && Time.time >= lastShoot + actualVehicle.fireRate)
            {
                Shoot();
            }
        }
    }

    void OnDrawGizmos()
    {
        //Gizmos.DrawCube(floorPos, Vector3.one * 0.3f);

        if (vehicle != null)
        {

            MeshFilter[] stMeshes = vehicle.GetComponentsInChildren<MeshFilter>();
            SkinnedMeshRenderer[] skMeshes = vehicle.GetComponentsInChildren<SkinnedMeshRenderer>();

            if (stMeshes.Length > 0)
            {
                foreach (MeshFilter st in stMeshes)
                {
                    //Debug.Log(st.sharedMesh);
                    Gizmos.DrawMesh(st.sharedMesh, 0, st.transform.position + transform.position, st.transform.rotation, st.transform.lossyScale);
                }
            }

            if (skMeshes.Length > 0)
            {

                foreach (SkinnedMeshRenderer sk in skMeshes)
                {
                    //Debug.Log("SKMeshes " + sk.sharedMesh );
                    Gizmos.DrawMesh(sk.sharedMesh, 0, sk.transform.position + transform.position, sk.transform.rotation, this.transform.localScale);
                }
            }


        }
    }
}
