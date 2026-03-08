using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class PrVehicle : PrActor
{
    [Header("Vehicle")]
    public bool turnedOn = false;

    public GameObject visualVehicle;

    [Header("Vehicle Type")]
    //public VEHICLE vehicleType = VEHICLE.tank;

    public bool rotateWheels = true;
    public Transform[] rotWheels;
    [HideInInspector]
    public Transform[] wheelsPivots;
    public Transform[] staticWheels;
    [HideInInspector]
    public Transform[] staticWheelsPivots;
    public GameObject lights;
    public Rigidbody m_rigidbody;
    public Sprite HUDVehiclePicture;

    [Header("Driver")]
    public GameObject driver;
    public Transform driverLocation;
    public Transform outDriverPos;

    public Transform[] handsIKPos;
    public Transform[] feetIKPos;
    
    public bool showDriver = true;

    [Header("Passengers")]
    public GameObject[] passengers;
    public Transform[] passengersLocations;
    public bool showPassengers = true;

    [Header("Turret")]
    public Transform turret;
    public Transform turretWeaponPos;
    public GameObject weapon;
    [HideInInspector]
    public PrWeapon actualWeapon;
    public float fireRate = 0.3f;
    public AudioSource weaponAudio;


    [Header("Visual FX and anims")]
    public float vehicleHeight = 0.5f;
    public float vehicleOffHeight = 0.25f;

    public AnimationCurve heightAnim;
    public float heightAnimSpeed = 1.0f;

    [HideInInspector]
    public float actualHeight = 0.0f;
    public float tiltRotationsSpeed = 1.0f;
    public float tiltFrontFactor = 1.0f;
    public float tiltSideFactor = 1.0f;

    [HideInInspector]
    public float wheelsXRot = 0.0f;
    [HideInInspector]
    public float wheelsYRot = 0.0f;
    public float wheelsSize = 1.0f;
    public float maxWheelTurn = 50.0f;

    [Header("Audio")]
    public AudioClip engineIdle;
    public AudioClip engineRunning;

    public PrVehicleController controller;
    public Animator animator;

    // Start is called before the first frame update
    void Start()
    {
        if (rotWheels.Length > 0)
        {
            wheelsPivots = new Transform[rotWheels.Length];
            int index = 0;
            foreach (Transform w in rotWheels)
            {
                GameObject tempRot = new GameObject(w.name + "_Pivot") as GameObject;
                tempRot.transform.SetParent(w.parent);
                tempRot.transform.position = w.position;
                tempRot.transform.rotation = w.rotation;
                w.SetParent(tempRot.transform);
                wheelsPivots[index] = tempRot.transform;
                if (w.GetComponent<Renderer>())
                    wheelsSize = w.GetComponent<Renderer>().bounds.size.y;
                index += 1;
            }
        }
        if (staticWheels.Length > 0)
        {
            staticWheelsPivots = new Transform[staticWheels.Length];
            int index = 0;
            foreach (Transform s in staticWheels)
            {
                GameObject tempRot = new GameObject(s.name + "_Pivot") as GameObject;
                tempRot.transform.SetParent(s.parent);
                tempRot.transform.position = s.position;
                tempRot.transform.rotation = s.rotation;
                s.SetParent(tempRot.transform);
                staticWheelsPivots[index] = tempRot.transform;
                if (s.GetComponent<Renderer>())
                    wheelsSize = s.GetComponent<Renderer>().bounds.size.y;
                index += 1;
            }
        }

        //Create Weapons
        if (weapon && turretWeaponPos)
        {
            weaponAudio = turretWeaponPos.gameObject.AddComponent<AudioSource>();

            GameObject tempWeapon = PrUtils.InstantiateActor(weapon, turretWeaponPos.position, turretWeaponPos.rotation, Name + weapon.name, turretWeaponPos);
            actualWeapon = tempWeapon.GetComponent<PrWeapon>();
            actualWeapon.turretWeapon = true;
            actualWeapon.hiddenWeapon = true;
            actualWeapon.updateHUD = useHUD;
            actualWeapon.AIWeapon = true;
            actualWeapon.Player = this.gameObject;
            actualWeapon.AIEnemyTarget = controller.actualAimTarget.transform;
            actualWeapon.FireRate = fireRate;
            actualWeapon.Audio = weaponAudio;

            InitializeWeaponHUD();
        }

        team = -1;

        if (MeshRenderers.Length == 0)
        {
            MeshRenderers = transform.GetComponentsInChildren<SkinnedMeshRenderer>();
        }

        
    }

    public void InitializeWeaponHUD()
    {
        if (actualHUD)
        {
            
                //Debug.Log(actualHUD + "_" + actualWeapon);
                actualWeapon.HUDWeaponBullets = actualHUD.HUDWeaponBullets.GetComponent<Text>();
                actualWeapon.HUDWeaponBulletsBar = actualHUD.HUDWeaponBulletsBar.GetComponent<Image>();
                actualWeapon.HUDWeaponClips = actualHUD.HUDWeaponClips.GetComponent<Text>();

                actualHUD.HUDWeaponPicture.GetComponent<Image>().sprite = actualWeapon.WeaponPicture;
                actualWeapon.useQuickReload = false;
            if (turret)
            {
                actualWeapon.HUDWeaponBullets.enabled = false;
                actualWeapon.HUDWeaponBulletsBar.enabled = false;
                actualWeapon.HUDWeaponClips.enabled = false;
            }

            if (!turnedOn)
            {
                actualHUD.SetCanvasesVisible(false);
            }
        }
    }

    protected override void CreateActorHUD()
    {
        base.CreateActorHUD();
        actualHUD.Compass.transform.parent.gameObject.AddComponent<PrCopyPosition>();
        actualHUD.Compass.transform.parent.gameObject.GetComponent<PrCopyPosition>().targetObject = this.transform;
        actualHUD.SetVehicleHUD(HUDVehiclePicture, Name);
        actualHUD.gameObject.SetActive(false);
        actualHUD.DeactivateCompass();
        
    }

        // Update is called once per frame
    void Update()
    {
        base.DamageUpdate();


        UpdateVehicleHUD();
    }

    void UpdateVehicleHUD()
    {
        if (driver && useHUD && actualHUD)
        {
            actualHUD.SpeedBar.fillAmount = (1 / controller.maxVelocity * Mathf.Abs(controller.velocity));
            actualHUD.speedText.text = Mathf.RoundToInt(Mathf.Abs( controller.velocity)).ToString();
        }

    }

    void VehicleDestruction(bool temperatureDeath)
    {
        if (driver && controller.driverController)
        {
            controller.driverController.character.Die(false, true);
        }

        turnedOn = false;
        controller.enabled = false;
        m_rigidbody.isKinematic = true;

        this.tag = "Untagged";
        if (controller.actualAimTarget)
            controller.actualAimTarget.SetActive(false);
        
        if (deathVFX && actualDeathVFX)
        {
            if (temperatureDeath)
            {
                //Freezing of Burning Death VFX...
            }
            else
            {

                gameObject.SetActive(false);

                actualDeathVFX.transform.position = transform.position;
                actualDeathVFX.transform.rotation = transform.rotation;
                actualDeathVFX.transform.localScale = transform.localScale;

                actualDeathVFX.SetActive(true);

                ParticleSystem[] particles = actualDeathVFX.GetComponentsInChildren<ParticleSystem>();

                if (particles.Length > 0)
                {
                    foreach (ParticleSystem p in particles)
                    {
                        p.Play();
                    }
                }
            }
        }


        
    }


    public override void Die(bool temperatureDeath,bool addXp)
    {
        base.Die(temperatureDeath, false);

        VehicleDestruction(false);

    }
}
