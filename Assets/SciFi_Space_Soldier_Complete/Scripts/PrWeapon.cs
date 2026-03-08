using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;

public class PrWeapon : MonoBehaviour {


    public string WeaponName = "Rifle";

    public enum WT
    {
        Pistol = 0, Rifle = 1, Minigun = 2, RocketLauncher = 3, Melee = 4, Laser = 5
    }

    public WT Type = WT.Rifle;
    public bool useIK = true;

    [Header("Melee Weapon")]
    public float MeleeRadius = 1.0f;
    public int meleeDamage = 1;
    private List<GameObject> meleeFinalTarget;

    [Header("Stats")]

    public int BulletsPerShoot = 1;
    public int BulletDamage = 20;
    private int originalBulletDamage = 20;
    public float tempModFactor = 0.0f;

    // Canceled scaling bullets, was causing weird behavior with collisions. 
    // [HideInInspector]
    // public float BulletSize = 1.0f;

    public float BulletSpeed = 1.0f;
    public float BulletAccel = 0.0f;
    public float bulletHitForce = 1.0f;

    public int Bullets = 10;
    [HideInInspector]
    public int ActualBullets = 0;

    public int Clips = 3;
    [HideInInspector]
    public int ActualClips = 0;

    public float ReloadTime = 1.0f;
    public bool playReloadAnim = true;
    private float ActualReloadTime = 0.0f;

    public float bulletTimeToLive = 3.0f;

    [HideInInspector]
    public bool Reloading = false;

    public float FireRate = 0.1f;
    public float AccDiv = 0.0f;

    public float radialAngleDirection = 0.0f;

    public float shootingNoise = 25f;

    public float kickBack = 0.5f;

    [Header("Quick Reload")]
    public bool useQuickReload = true;
    public Vector2 HUDQuickReloadTimes = new Vector2(0.5f, 0.7f);
    private bool quickReloadActive = false;

    [Header("References & VFX")]
    public float shootShakeFactor = 2.0f;
    public Transform ShootFXPos;
    public GameObject BulletPrefab;
    public GameObject ShootFXFLash;
    public Light ShootFXLight;
    public Renderer LaserSight;
    //[HideInInspector]
    public PrTopDownCamera playerCamera;

    [Header("Laser Weapon Settings")]
    public GameObject laserBeamPrefab;
    private GameObject[] actualBeams;
    public float laserWidthFactor = 1.0f;
    public float laserLiveTime = 1.0f;
    public float warmingTime = 0.2f;
    public bool generatesBloodDamage = true;
    public GameObject warmingVFX;
    private GameObject actualWarmingVFX;
    public GameObject laserHitVFX;
    private GameObject[] actualLaserHits;

    [HideInInspector]
    public Transform ShootTarget;
    //[HideInInspector]
    public GameObject Player;
    public GameObject playerController;
    public Animator playerAnimator;

    [Header("Sound FX")]
    public AudioClip[] ShootSFX;
    public AudioClip ReloadSFX;
    public AudioClip ShootEmptySFX;
    //[HideInInspector]
    public AudioSource Audio;

    [Header("Autoaim")]
    public float AutoAimAngle = 7.5f;
    public float AutoAimDistance = 10.0f;

    private Vector3 EnemyTargetAuto = Vector3.zero;
    private Vector3 FinalTarget = Vector3.zero;

    //HUD
    [Header("HUD")]
    [HideInInspector]
    public bool updateHUD = true;

    public Sprite WeaponPicture;
    [HideInInspector]
    public GameObject HUDWeaponPicture;
    [HideInInspector]
    public Text HUDWeaponBullets;
    [HideInInspector]
    public Image HUDWeaponBulletsBar;
    [HideInInspector]
    public Text HUDWeaponClips;
    [HideInInspector]
    public RectTransform HUDQuickReloadMarker;
    [HideInInspector]
    public RectTransform HUDQuickReloadZone;


    //Object Pooling Manager
    public bool usePooling = true;
    private GameObject[] GameBullets;
    private PrBullet[] gameBulletsComp;
    private GameObject BulletsParent;
    private int ActualGameBullet = 0;
    private GameObject Muzzle;

    [HideInInspector]
    public bool AIWeapon = false;
    public bool AIFriendlyWeapon = false;
    public bool hiddenWeapon = false;
    //[HideInInspector]
    public Transform AIEnemyTarget;

    [HideInInspector]
    public bool turretWeapon = false;

    [HideInInspector]
    public int team = 0;
    [HideInInspector]
    public int plyrController = 99;


    private void Awake()
    {
        ActualBullets = Bullets;
        ActualClips = Clips;
        originalBulletDamage = BulletDamage;
        if (Type == WT.Melee)
            originalBulletDamage = meleeDamage;
    }

    public void UpdateWeaponLevel(float factor)
    {
        BulletDamage = Mathf.RoundToInt(originalBulletDamage * factor);
        meleeDamage = Mathf.RoundToInt(originalBulletDamage * factor);
        if (Type != WT.Melee &&  usePooling && gameBulletsComp != null && gameBulletsComp.Length > 0)
        {
            foreach (PrBullet b in gameBulletsComp)
            {
                if (b)
                    b.Damage = BulletDamage; 
            }
        }
    }
    
    // Use this for initialization

    public void SetLayer()
    {
        if (GetComponentInChildren<SkinnedMeshRenderer>() && Player)
        {
            GetComponentInChildren<SkinnedMeshRenderer>().gameObject.layer = Player.layer;

            if (AIWeapon && !AIFriendlyWeapon)
            {
                GetComponentInChildren<SkinnedMeshRenderer>().gameObject.layer = LayerMask.NameToLayer("Character");
            }
            else if (AIFriendlyWeapon)
            {
                GetComponentInChildren<SkinnedMeshRenderer>().gameObject.layer = LayerMask.NameToLayer("PlayerCharacter");
            }
        }
       

        if (GetComponentInChildren<MeshRenderer>())
        {
            MeshRenderer[] meshes = GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer x in meshes)
            {
                if (playerAnimator)
                {
                    if (!LaserSight || x != LaserSight)
                        x.gameObject.layer = playerAnimator.gameObject.layer;
                }
            }
        }
    }

    void Start()
    {
        if (!hiddenWeapon)
            Audio = transform.parent.GetComponent<AudioSource>();

        

        if (updateHUD)
        {
            if (HUDWeaponBullets && HUDWeaponClips && HUDWeaponBulletsBar)
            {
                HUDWeaponBullets.text = (ActualBullets / BulletsPerShoot).ToString();
                HUDWeaponClips.text = ActualClips.ToString();
                HUDWeaponBulletsBar.fillAmount = (1.0f / Bullets) * ActualBullets;
                HUDWeaponBulletsBar.GetComponent<RectTransform>().localScale = Vector3.one;
            }

            if (playerController)
                team = playerController.GetComponent<PrCharacter>().team;
        }

        //Basic Object Pooling Initialization ONLY FOR RANGED WEAPONS
        if (Type == WT.Rifle || Type == WT.Pistol || Type == WT.Minigun || Type == WT.RocketLauncher)
        {
            if (usePooling)
            {
                GameBullets = new GameObject[Bullets * BulletsPerShoot];
                gameBulletsComp = new PrBullet[Bullets * BulletsPerShoot];
                BulletsParent = new GameObject(WeaponName + "_Bullets");

                for (int i = 0; i < (Bullets * BulletsPerShoot); i++)
                {
                    GameBullets[i] = Instantiate(BulletPrefab, ShootFXPos.position, ShootFXPos.rotation) as GameObject;
                    GameBullets[i].SetActive(false);
                    GameBullets[i].name = WeaponName + "_Bullet_" + i.ToString();
                    GameBullets[i].transform.parent = BulletsParent.transform;
                    gameBulletsComp[i] = GameBullets[i].GetComponent<PrBullet>();

                    gameBulletsComp[i].team = team;
                    gameBulletsComp[i].usePooling = true;
                    gameBulletsComp[i].InitializePooling();
                    //Generic 
                    if (!AIWeapon)
                        gameBulletsComp[i].playerCamera = playerCamera;
                }
            }

        }
        else if (Type == WT.Laser)
        {

            actualBeams = new GameObject[BulletsPerShoot];
            actualLaserHits = new GameObject[BulletsPerShoot];
            GameObject BulletsParent = new GameObject(WeaponName + "_Beams");

            //Laser Weapon Initialization
            for (int i = 0; i < BulletsPerShoot; i++)
            {
                actualBeams[i] = Instantiate(laserBeamPrefab, ShootFXPos.position, ShootFXPos.rotation) as GameObject;
                actualBeams[i].SetActive(false);
                actualBeams[i].name = WeaponName + "_Beam_" + i.ToString();
                actualBeams[i].transform.parent = BulletsParent.transform;
                actualBeams[i].GetComponent<PrWeaponLaserBeam>().InitializeLine(laserWidthFactor, ShootFXPos);

                actualLaserHits[i] = Instantiate(laserHitVFX, ShootFXPos.position, ShootFXPos.rotation) as GameObject;
                actualLaserHits[i].SetActive(false);
                actualLaserHits[i].name = WeaponName + "_Beam_Hit_" + i.ToString();
                actualLaserHits[i].transform.parent = BulletsParent.transform;
                
            }

            if (turretWeapon)
            {
                ShootTarget = new GameObject("ShootTarget").transform;
                ShootTarget.SetParent(transform);
            }


        }
        else if (Type == WT.Melee)
        {
            //Melee Weapon Initialization
            /*
            HUDWeaponBullets.text = "";
            HUDWeaponClips.text = "";
            HUDWeaponBulletsBar.GetComponent<RectTransform>().localScale = Vector3.zero;*/
        }

        if (ShootFXFLash)
        {
            Muzzle = Instantiate(ShootFXFLash, ShootFXPos.position, ShootFXPos.rotation) as GameObject;
            Muzzle.transform.parent = ShootFXPos.transform;
            Muzzle.SetActive(false);
        }
        if (playerCamera != null)
        {
            playerCamera = playerController.GetComponent<PrCharacterController>().CamScript;


        }
        
        ////Debug.Log("PlayerCamera :" + playerCamera);
        
       

        if (useQuickReload)
        {
            if (HUDQuickReloadTimes[0] < 0.0f)
                HUDQuickReloadTimes[0] = 0.0f;
            else if (HUDQuickReloadTimes[0] >= 1.0f)
                HUDQuickReloadTimes[0] = 0.98f;

            if (HUDQuickReloadTimes[1] < 0.0f)
                HUDQuickReloadTimes[1] = 0.1f;
            else if (HUDQuickReloadTimes[1] >= 1.0f)
                HUDQuickReloadTimes[1] = 0.99f;
        }

        

    }


    // Update is called once per frame
    void Update() {

        if (Reloading)
        {
            ActualReloadTime += Time.deltaTime;

            if (!AIWeapon && !turretWeapon && useQuickReload && HUDQuickReloadMarker)
            {
                HUDQuickReloadMarker.localPosition = new Vector3(ActualReloadTime * (46.0f / ReloadTime), 0, 0);

                if (ActualReloadTime >= (HUDQuickReloadTimes[0] * ReloadTime) && ActualReloadTime <= (HUDQuickReloadTimes[1] * ReloadTime))
                    quickReloadActive = true;
                else
                    quickReloadActive = false;
            }

            if (ActualReloadTime >= ReloadTime)
            {
                PositiveReload();
            }
        }


    }

    private void OnDestroy()
    {
        if (BulletsParent)
            Destroy(BulletsParent);
    }

    void PositiveReload()
    {
        Reloading = false;
        ActualReloadTime = 0.0f;
        SendMessageUpwards("EndReload", SendMessageOptions.DontRequireReceiver);

        WeaponEndReload();
    }

    public void SetupQuickReload()
    {
        HUDQuickReloadZone.localPosition = new Vector3(HUDQuickReloadTimes[0] * 46.0f, 0, 0);
        HUDQuickReloadZone.localScale = new Vector3(HUDQuickReloadTimes[1] - HUDQuickReloadTimes[0], 1, 1);
    }

    public void TryQuickReload()
    {
        if (quickReloadActive)
        {
            PositiveReload();
            quickReloadActive = false;
        }
            
    }

    public void TurnOffLaser()
    {
        LaserSight.enabled = false;
    }

    void LateUpdate()
    {
        if (!AIWeapon && !hiddenWeapon && ShootTarget != null)
        {
            LaserSight.transform.position = ShootFXPos.position;
            LaserSight.transform.LookAt(ShootTarget.position, Vector3.up);
        }
    }

    void WeaponEndReload()
    {
        ActualBullets = Bullets;
        if (gameObject.activeInHierarchy == true)
        {
            UpdateWeaponGUI();
        }
        
        
    }

    void UpdateWeaponGUI()
    {
        if (updateHUD)
        {
            if (Type != WT.Melee)
            {
                HUDWeaponBullets.text = (ActualBullets / BulletsPerShoot).ToString();
                HUDWeaponClips.text = ActualClips.ToString();
                HUDWeaponBulletsBar.fillAmount = (1.0f / Bullets) * ActualBullets;
            }
            else
            {
                HUDWeaponBullets.text = "-";
                HUDWeaponClips.text = "-";
                HUDWeaponBulletsBar.fillAmount = 1.0f;
            }
            
            ////Debug.Log("Bullets = " + Bullets);
            //HUDWeaponBulletsBar.GetComponent<RectTransform>().localScale = new Vector3((1.0f / Bullets) * ActualBullets, 1.0f, 1.0f);

        }

    }

    public void UpdateWeaponGUI(GameObject weapPic)
    {
        if (!AIWeapon)
        {
            if (Type != WT.Melee)
            {
                HUDWeaponBullets.text = (ActualBullets / BulletsPerShoot).ToString();
                HUDWeaponClips.text = ActualClips.ToString();
                HUDWeaponBulletsBar.fillAmount = (1.0f / Bullets) * ActualBullets;
                //HUDWeaponBulletsBar.GetComponent<RectTransform>().localScale = new Vector3((1.0f / Bullets) * ActualBullets, 1.0f, 1.0f);
                HUDWeaponPicture = weapPic;
                if (HUDWeaponPicture.GetComponentInChildren<Text>())
                    HUDWeaponPicture.GetComponentInChildren<Text>().text = WeaponName;
            }
            else
            {
                HUDWeaponBullets.text = "-";
                HUDWeaponClips.text = "-";
                HUDWeaponBulletsBar.fillAmount = 1.0f;
                //HUDWeaponBulletsBar.GetComponent<RectTransform>().localScale = new Vector3((1.0f / Bullets) * ActualBullets, 1.0f, 1.0f);
                HUDWeaponPicture = weapPic;
                if (HUDWeaponPicture.GetComponentInChildren<Text>())
                    HUDWeaponPicture.GetComponentInChildren<Text>().text = WeaponName;
            }
        }
        
    }

    public void CancelReload()
    {
        Reloading = false;
        if (playReloadAnim && playerAnimator)
             playerAnimator.SetBool("Reloading", false);
        SendMessageUpwards("EndReload", SendMessageOptions.DontRequireReceiver);
        ActualReloadTime = 0.0f;
    }

	public void Reload()
	{
		if (ActualClips > 0 || Clips == -1)
		{
            
            if (!AIWeapon || !turretWeapon)
            {
                if (useQuickReload)
                    SendMessageUpwards("QuickReloadActive", true, SendMessageOptions.DontRequireReceiver);
                ActualClips -= 1;
            }
            
            if (playReloadAnim && !turretWeapon && !AIWeapon && playerAnimator)
                playerAnimator.SetBool("Reloading", true);
            Reloading = true;
            Audio.PlayOneShot(ReloadSFX);
            ActualReloadTime = 0.0f;
           
        }
	}

    public void AIReload()
    {
        SendMessageUpwards("StartReload", SendMessageOptions.DontRequireReceiver);
        Reloading = true;
        Audio.PlayOneShot(ReloadSFX);
        ActualReloadTime = 0.0f;
    }

    void AutoAim()
    {
        //Autoaim////////////////////////

        GameObject[] Enemys = GameObject.FindGameObjectsWithTag("Enemy");
        if (Enemys != null)
        {
            float BestDistance = 100.0f;

            foreach (GameObject Enemy in Enemys)
            {
                Vector3 EnemyPos = Enemy.transform.position;
                Vector3 EnemyDirection = EnemyPos - playerAnimator.transform.position;
                float EnemyDistance = EnemyDirection.magnitude;

                if (Vector3.Angle(playerAnimator.transform.forward, EnemyDirection) <= AutoAimAngle && EnemyDistance < AutoAimDistance)
                {
                    //
                    if (Enemy.GetComponent<PrActorUtils>().character.isDead != true)
                    {
                        if (EnemyDistance < BestDistance)
                        {
                            BestDistance = EnemyDistance;
                            EnemyTargetAuto = EnemyPos + new Vector3(0, 1, 0);
                        }
                    }
                   

                }
            }
        }

        if (EnemyTargetAuto != Vector3.zero)
        {
            FinalTarget = EnemyTargetAuto;
            ShootFXPos.transform.LookAt(FinalTarget);
        }
        else
        {
            ShootFXPos.transform.LookAt(ShootTarget.position);
            FinalTarget = ShootTarget.position;
        }

        //End of AutoAim
        /////////////////////////////////

    }

    void AIAutoAim()
    {
        //Autoaim////////////////////////

        Vector3 PlayerPos = AIEnemyTarget.position + new Vector3(0, 1.0f, 0);
        FinalTarget = PlayerPos;
        
      
    }

    public void PlayShootAudio()
    {
        if (ShootSFX.Length > 0)
        {
            int FootStepAudio = 0;

            if (ShootSFX.Length > 1)
            {
                FootStepAudio = Random.Range(0, ShootSFX.Length);
            }

            float RandomVolume = Random.Range(0.6f, 1.0f);

            if (Audio != null)
                Audio.PlayOneShot(ShootSFX[FootStepAudio], RandomVolume);
            else
                Debug.LogWarning("Weapon shoot audio beign played but you need to assign a AudioSource component to your Weapon Attachment node");

            if (!AIWeapon)
                playerController.SendMessage("MakeNoise", shootingNoise);
           
        }
    }

    public void Shoot()
	{
        if (AIWeapon || turretWeapon)
        {
            AIAutoAim();
        }
        else
        {
            AutoAim();
        }

        if (ActualBullets > 0)
            PlayShootAudio();
        //else
        //    Audio.PlayOneShot(ShootEmptySFX);
        float angleStep = radialAngleDirection / BulletsPerShoot;
        float finalAngle = 0.0f; 

        for (int i = 0; i < BulletsPerShoot; i++)
		{
            
            float FinalAccuracyModX = Random.Range(AccDiv, -AccDiv) * Vector3.Distance(Player.transform.position, FinalTarget);
            FinalAccuracyModX /= 100;

            float FinalAccuracyModY = Random.Range(AccDiv, -AccDiv) * Vector3.Distance(Player.transform.position, FinalTarget);
            FinalAccuracyModY /= 100;

            float FinalAccuracyModZ = Random.Range(AccDiv, -AccDiv) * Vector3.Distance(Player.transform.position, FinalTarget);
            FinalAccuracyModZ /= 100;
          
            Vector3 FinalOrientation = FinalTarget + new Vector3(FinalAccuracyModX, FinalAccuracyModY, FinalAccuracyModZ);

			ShootFXPos.transform.LookAt(FinalOrientation);

            if (BulletsPerShoot > 1 && radialAngleDirection > 0.0f)
            {
                Quaternion aimLocalRot = Quaternion.Euler(0, finalAngle - (radialAngleDirection / 2) + (angleStep * 0.5f), 0);
                ShootFXPos.transform.rotation = ShootFXPos.transform.rotation * aimLocalRot;

                finalAngle += angleStep;
            }

            //Get Who is Shooting to give XP after killing


            if (Type != WT.Laser && BulletPrefab && ShootFXPos && !Reloading)
            {
                if (ActualBullets > 0)
                {
                    ApplyForceToOwner();

                    GameObject Bullet;
                    PrBullet bulletComp;
                    if (usePooling)
                    {
                        //Object Pooling Method 
                        Bullet = GameBullets[ActualGameBullet];
                        bulletComp = gameBulletsComp[ActualGameBullet];
                        Bullet.transform.position = ShootFXPos.position;
                        Bullet.transform.rotation = ShootFXPos.rotation;
                        Bullet.GetComponent<Rigidbody>().isKinematic = false;
                        Bullet.GetComponent<Collider>().enabled = true;
                        bulletComp.timeToLive = bulletTimeToLive;
                        bulletComp.ResetPooling();
                        bulletComp.playerNmb = plyrController;
                        Bullet.SetActive(true);
                        ActualGameBullet += 1;
                        if (ActualGameBullet >= GameBullets.Length)
                            ActualGameBullet = 0;
                    }
                    else
                    {
                        Bullet = Instantiate(BulletPrefab, ShootFXPos.position, ShootFXPos.rotation);
                        bulletComp = Bullet.GetComponent<PrBullet>();
                        bulletComp.usePooling = false;
                        Bullet.SetActive(true);
                        Bullet.GetComponent<Rigidbody>().isKinematic = false;
                        Bullet.GetComponent<Collider>().enabled = true;
                        bulletComp.timeToLive = bulletTimeToLive;
                        bulletComp.playerNmb = plyrController;
                    }
                        

                    //Object Pooling VFX
                    Muzzle.transform.rotation = transform.rotation;
                    EmitParticles(Muzzle);

                    
                    bulletComp.Damage = BulletDamage;
                    bulletComp.temperatureMod = tempModFactor;
                    bulletComp.BulletSpeed = BulletSpeed;
                    bulletComp.HitForce = bulletHitForce;
                    bulletComp.BulletAccel = BulletAccel;
                    // Canceled scaling bullets, was causing weird behavior with collisions. 
                    // if (usePooling)
                    //    Bullet.transform.localScale = bulletComp.OriginalScale * BulletSize;

                    ShootFXLight.GetComponent<PrLightAnimator>().AnimateLight(true);
                    ActualBullets -= 1;

                    if (playerCamera)
                    {
                        if (!AIWeapon)
                            playerCamera.Shake(shootShakeFactor, 0.2f);
                        else
                            playerCamera.Shake(shootShakeFactor * 0.5f, 0.2f);
                    }

                    if (ActualBullets == 0)
                        Reload();

                }

            }
            // Laser Shoot
            else if (Type == WT.Laser && actualBeams.Length != 0 && ShootFXPos && !Reloading)
            {
                bool useDefaultImpactFX = true;
                
                Vector3 HitPos = ShootTarget.position + new Vector3(0, 1.2f, 0);

                Vector3 hitNormal = ShootTarget.forward;


                if (ActualBullets > 0)
                {
                    ApplyForceToOwner();
                    //Object Pooling Method 
                    GameObject Beam = actualBeams[ActualGameBullet];
                    Beam.transform.position = ShootFXPos.position;
                    Beam.transform.rotation = ShootFXPos.rotation;
                    Beam.SetActive(true);
                    
                    Beam.GetComponent<PrWeaponLaserBeam>().Activate(laserLiveTime);
                    //Shoot Beam
                    RaycastHit hit;

                    if (Physics.Raycast(ShootFXPos.position, ShootFXPos.forward, out hit))
                    {
                        GameObject target = hit.collider.gameObject;
                        HitPos = hit.point;
                        hitNormal = hit.normal;
                        Beam.GetComponent<PrWeaponLaserBeam>().SetPositions(ShootFXPos.position, HitPos);

                        if (hit.collider.tag == "Player" && target.GetComponent<PrActorUtils>().team != team)
                        {
                            PrCharacter finalTarget = target.GetComponent<PrActorUtils>().character;
                            
                            finalTarget.ApplyDamage(BulletDamage, tempModFactor, team, transform.position, generatesBloodDamage, 1.0f, plyrController);

                            if (generatesBloodDamage)
                            {
                                if (finalTarget.DamageFX != null)
                                {
                                    Instantiate(finalTarget.DamageFX, HitPos, Quaternion.LookRotation(hitNormal));
                                    useDefaultImpactFX = false;
                                }
                            }

                        }
                        else if (hit.collider.tag == "Enemy")
                        {
                            PrCharacter finalTarget = target.GetComponent<PrActorUtils>().character;
                            finalTarget.ApplyDamage(BulletDamage, tempModFactor, team, transform.position, generatesBloodDamage, 1.0f, plyrController);

                            if (generatesBloodDamage)
                            {
                                if (finalTarget.DamageFX != null)
                                {
                                    Instantiate(finalTarget.DamageFX, HitPos, Quaternion.LookRotation(hitNormal));
                                    useDefaultImpactFX = false;
                                }
                            }

                        }

                        else if (hit.collider.tag == "AIPlayer" && target.GetComponent<PrAIController>().team != team)
                        {
                            PrCharacter finalTarget = target.GetComponent<PrActorUtils>().character;
                            finalTarget.ApplyDamage(BulletDamage, tempModFactor, team, transform.position, generatesBloodDamage, 1.0f, plyrController);

                            if (generatesBloodDamage)
                            {
                                if (finalTarget.DamageFX != null)
                                {
                                    Instantiate(finalTarget.DamageFX, HitPos, Quaternion.LookRotation(hitNormal));
                                    useDefaultImpactFX = false;
                                }
                            }



                        }
                        else if (hit.collider.tag == "Destroyable" && target.GetComponent<PrDestroyableActor>().team != team)
                        {
                            ////Debug.Log("Bullet team = " + team + " Target Team = " + Target.GetComponent<PrDestroyableActor>().team);
                            target.SendMessage("BulletPos", hit.point, SendMessageOptions.DontRequireReceiver);
                            target.SendMessage("ApplyTempMod", tempModFactor, SendMessageOptions.DontRequireReceiver);
                            target.SendMessage("ApplyDamage", BulletDamage, SendMessageOptions.DontRequireReceiver);
                            if (target.GetComponent<Rigidbody>())
                            {
                                target.GetComponent<Rigidbody>().AddForceAtPosition(hitNormal * Random.Range(-200.0f,-400.0f), HitPos);
                            }
                        }
                    }

                    else
                    {
                        Beam.GetComponent<PrWeaponLaserBeam>().SetPositions(ShootFXPos.position, ShootTarget.position + new Vector3(0,1.2f,0));
                    }

                    //default Hit VFX
                    if (useDefaultImpactFX)
                    {
                        actualLaserHits[ActualGameBullet].SetActive(true);
                        actualLaserHits[ActualGameBullet].transform.position = HitPos;
                        actualLaserHits[ActualGameBullet].transform.rotation = Quaternion.LookRotation(hitNormal);
                        actualLaserHits[ActualGameBullet].GetComponent<ParticleSystem>().Play();
                    }

                    ActualGameBullet += 1;
                    //Object Pooling VFX
                    Muzzle.transform.rotation = transform.rotation;
                    EmitParticles(Muzzle);

                    if (ActualGameBullet >= actualBeams.Length)
                        ActualGameBullet = 0;

                    ShootFXLight.GetComponent<PrLightAnimator>().AnimateLight(true);
                    ActualBullets -= 1;

                    if (playerCamera)
                    {
                        if (!AIWeapon)
                            playerCamera.Shake(shootShakeFactor, 0.2f);
                        else
                            playerCamera.Shake(shootShakeFactor * 0.5f, 0.2f);
                    }

                    if (ActualBullets == 0)
                        Reload();

                }
            }

            UpdateWeaponGUI();

            EnemyTargetAuto = Vector3.zero;

            
        }
	}

    void ApplyForceToOwner()
    {
        if (!turretWeapon && !AIWeapon)
            playerController.GetComponent<PrCharacterController>().AddForce(kickBack);
    }


    void EmitParticles(GameObject VFXEmiiter)
    {
        VFXEmiiter.SetActive(true);
        VFXEmiiter.GetComponent<ParticleSystem>().Play();
    }


    public void AIAttackMelee(Vector3 playerPos, GameObject targetGO, bool attackVehicle)
    {
        PlayShootAudio();
        //Debug.Log("Attacking Vehicle " + attackVehicle + " " + targetGO);
        //Object Pooling VFX
        if (Muzzle)
        {
            EmitParticles(Muzzle);
        }
        if (ShootFXLight)
            ShootFXLight.GetComponent<PrLightAnimator>().AnimateLight(true);

        if (Vector3.Distance(playerPos + Vector3.up, ShootFXPos.position) <= MeleeRadius)
        {
            if (!attackVehicle)
            {
                if (targetGO.GetComponent<PrActorUtils>())
                {
                    targetGO.GetComponent<PrActorUtils>().character.ApplyDamage(meleeDamage, 0, team, ShootFXPos.position, generatesBloodDamage, 1.0f, plyrController);
                }
                else if (targetGO.GetComponent<PrDestroyableActor>())
                {
                    targetGO.GetComponent<PrDestroyableActor>().ApplyDamage(meleeDamage);
                }
            }
            else
            {
                
                if (targetGO.GetComponent<PrVehicle>())
                {
                    //Debug.Log("Got finally here " + targetGO.name);
                    targetGO.GetComponent<PrVehicle>().ApplyDamage(meleeDamage, 0, team, ShootFXPos.position, false, 1.0f, plyrController);
                }
            }

            ////Debug.Log("Hit Player Sucessfully");

        }
    }

    public void AttackMelee(bool useVFX)
    {
        PlayShootAudio();

        //Object Pooling VFX
        if (Muzzle && useVFX)
        {
            EmitParticles(Muzzle);
        }
        //Use Light
        if (ShootFXLight && useVFX)
            ShootFXLight.GetComponent<PrLightAnimator>().AnimateLight(true);

        //Start Finding Enemy Target
        meleeFinalTarget = new List<GameObject>();

        GameObject[] EnemysTemp = GameObject.FindGameObjectsWithTag("Enemy");
        GameObject[] PlayersTemp = GameObject.FindGameObjectsWithTag("Player");

        GameObject[] Enemys = new GameObject[EnemysTemp.Length + PlayersTemp.Length];
        int t = 0;
        foreach (GameObject E in EnemysTemp)
        {
            Enemys[t] = E;
            t += 1;
        }
        foreach (GameObject E in PlayersTemp)
        {
            Enemys[t] = E;
            t += 1;
        }


        if (Enemys != null)
        {
            float BestDistance = 100.0f;

            foreach (GameObject Enemy in Enemys)
            {
                Vector3 EnemyPos = Enemy.transform.position;
                Vector3 EnemyDirection = EnemyPos - playerAnimator.transform.position;
                float EnemyDistance = EnemyDirection.magnitude;

                if (Vector3.Angle(playerAnimator.transform.forward, EnemyDirection) <= 90 && EnemyDistance < MeleeRadius)
                {
                    //
                    if (Enemy.GetComponent<PrAIController>())
                    {
                        if (Enemy.GetComponent<PrAIController>().actualState != PrAIController.AIState.Dead && Enemy.GetComponent<PrAIController>().team != team)
                        {
                            if (EnemyDistance < BestDistance)
                            {
                                BestDistance = EnemyDistance;
                                meleeFinalTarget.Add(Enemy);// = Enemy;
                            }

                        }
                    }
                    else if (Enemy.GetComponent<PrActorUtils>())
                    {
                        if (Enemy.GetComponent<PrActorUtils>().character.isDead != true && Enemy.GetComponent<PrActorUtils>().team != team)
                        {
                            if (EnemyDistance < BestDistance)
                            {
                                BestDistance = EnemyDistance;
                                meleeFinalTarget.Add(Enemy);// = Enemy;
                            }

                        }
                    }
                   
                }
            }
        }

        GameObject[] destroyables = GameObject.FindGameObjectsWithTag("Destroyable");

        if (destroyables != null)
        {
            float BestDistance = 100.0f;

            foreach (GameObject destroyable in destroyables)
            {
                Vector3 destroyablePos = destroyable.transform.position;
                Vector3 destrDirection = destroyablePos - playerAnimator.transform.position;
                float EnemyDistance = destrDirection.magnitude;

                if (Vector3.Angle(playerAnimator.transform.forward, destrDirection) <= 90 && EnemyDistance < MeleeRadius)
                {
                    if (EnemyDistance < BestDistance)
                    {
                        BestDistance = EnemyDistance;
                        meleeFinalTarget.Add(destroyable);// = Enemy;
                    }
                 }
            }
        }

        foreach (GameObject meleeTarget in meleeFinalTarget)
        {
            ////Debug.Log("Hit Enemy Sucessfully");
            meleeTarget.SendMessage("PlayerTeam", team, SendMessageOptions.DontRequireReceiver);
            meleeTarget.SendMessage("BulletPos", ShootFXPos.position, SendMessageOptions.DontRequireReceiver);
            meleeTarget.SendMessage("ApplyDamage", meleeDamage, SendMessageOptions.DontRequireReceiver);
        }
            
       
    }

    public void LoadAmmo(int LoadType)
    {
        ActualBullets = Bullets;
        ActualClips = Mathf.Clamp(ActualClips + (Clips / LoadType), 0, Clips);
        WeaponEndReload();
    }

    void OnDrawGizmos()
    {
        /*
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(FinalTarget, 0.25f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(ShootFXPos.position, 0.2f);*/

    }
}
