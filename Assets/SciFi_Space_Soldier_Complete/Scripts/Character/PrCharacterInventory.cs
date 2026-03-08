using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PrCharacterInventory : MonoBehaviour
{
    [Header("Weapons")]

    public bool alwaysAim = false;

    public int playerWeaponLimit = 2;

    public PrWeapon[] InitialWeapons;
    [HideInInspector]
    public float lastWeaponChange = 0.0f;
    [HideInInspector]
    public bool Armed = true;
    [HideInInspector]
    public GameObject[] Weapon;
    [HideInInspector]
    public PrWeapon actualWeapon;
    [HideInInspector]
    public int[] actualWeaponTypes;
    [HideInInspector]
    public int ActiveWeapon = 0;
    [HideInInspector]
    public bool CanShoot = true;
    public PrWeaponList WeaponListObject;
    private GameObject[] WeaponList;
    [HideInInspector]
    public Transform WeaponR;
    [HideInInspector]
    public Transform WeaponL;

    public bool aimingIK = false;
    public bool useArmIK = true;

    [HideInInspector]
    public bool useQuickReload = true;

    [Header("Always Melee")]
    public bool meleeActive = false;
    [HideInInspector]
    public float meleeRate = 1.0f;
    public GameObject meleeWeapon;
    [HideInInspector]
    public GameObject actualMeleeWeapon;
    [HideInInspector]
    public bool waitingArmIK = false;

    //Grenade Vars
    [Header("Grenades Vars")]
    public float ThrowGrenadeMaxForce = 100f;
    public GameObject grenadesPrefab;
    public int maxGrenades = 10;
    public int grenadesCount = 5;
    public enum ThrowWay {CharacterForward, AimPosition}

    public ThrowWay grenadeThrow = ThrowWay.CharacterForward;
    [HideInInspector]
    public bool isThrowing = false;

    [HideInInspector] public bool Aiming = false;

    [HideInInspector] public float FireRateTimer = 0.0f;
    [HideInInspector] public float LastFireTimer = 0.0f;

    //Pickup Vars
    [Header("Items")]
    public int currentMoney = 0;
    [HideInInspector]
    public GameObject PickupObj;
    [HideInInspector]
    public int BlueKeys = 0;
    [HideInInspector]
    public int RedKeys = 0;
    [HideInInspector]
    public int YellowKeys = 0;
    [HideInInspector]
    public int FullKeys = 0;

    //Private References
    [HideInInspector]
    public PrCharacterController charController;
    [HideInInspector]
    public PrAIController AIController;
    [HideInInspector]
    public Animator charAnimator;
    [HideInInspector]
    public PrCharacter character;
    [HideInInspector]
    public PrActorUtils characterUtils;
    [HideInInspector]
    public PrActorHUD characterHUD;

    [HideInInspector]
    public bool AIInventory = false;

    private GameObject[] Canvases;

    //ArmIK variables
    private Transform ArmIKTarget = null;
    private PrCharacterIK CharacterIKController;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void InitializeAIInventory()
    {
        //Debug.Log(gameObject.name + " " + "Initialize AI Inventory");
        AIInventory = true;

        AIController = GetComponent<PrAIController>();
        character = GetComponent<PrCharacter>();

        if (character.useHUD && character.actualHUD)
            characterHUD = character.actualHUD;

        if (character.charAnimator)
            charAnimator = character.charAnimator;
        characterUtils = character.actualCharUtils;

        //Creates weapon array
        Weapon = new GameObject[playerWeaponLimit];
        actualWeaponTypes = new int[playerWeaponLimit];

        WeaponList = WeaponListObject.AIWeapons;

        //Weapon Instantiate and initialization
        if (InitialWeapons.Length > 0)
        {
            InstantiateWeapons();

            Armed = true;

            
        }
        else
        {
            Armed = false;
        }

        if (useArmIK)
        {
            EnableArmIK(useArmIK);
        }
        
        
        //Update grenades HUD
        if (character.useHUD && characterHUD.HUDGrenadesCount)
            characterHUD.HUDGrenadesCount.GetComponent<Text>().text = grenadesCount.ToString();
    }

    public void InitializeInventory()
    {
        charController = GetComponent<PrCharacterController>();
        
        character = GetComponent<PrCharacter>();

        if (character.useHUD && character.actualHUD)
            characterHUD = character.actualHUD;

        if (character.charAnimator)
            charAnimator = character.charAnimator;
        characterUtils = character.actualCharUtils;

        //Creates weapon array
        Weapon = new GameObject[playerWeaponLimit];
        actualWeaponTypes = new int[playerWeaponLimit];

        //Load Weapon List from Scriptable Object
        WeaponList = WeaponListObject.weapons;

        //Weapon Instantiate and initialization
        if (InitialWeapons.Length > 0)
        {
            InstantiateWeapons();

            Armed = true;

        }
        else
        {
            Armed = false;
        }

        //Melee While armed
        if (meleeActive)
        {
            InstantiateMeleeWeapon();
        }

        Canvases = GameObject.FindGameObjectsWithTag("Canvas");
        if (Canvases.Length > 0)
        {
            foreach (GameObject C in Canvases)
                UnparentTransforms(C.transform);
        }

        if (alwaysAim)
        {
            Aiming = true;
            if (charAnimator)
                charAnimator.SetBool("Aiming", true);
        }

        if (useArmIK)
        {
            EnableArmIK(useArmIK);
        }

        if (characterHUD)
        {
            if (useQuickReload && characterHUD.quickReloadPanel && characterHUD.quickReloadMarker && characterHUD.quickReloadZone)
            {
                characterHUD.QuickReloadActive(false);
            }

            //Update grenades HUD
            if (characterHUD.HUDGrenadesCount)
                characterHUD.HUDGrenadesCount.GetComponent<Text>().text = grenadesCount.ToString();
        }

    }

    void InstantiateMeleeWeapon()
    {
        actualMeleeWeapon = PrUtils.InstantiateActor(meleeWeapon, WeaponR.transform.position, WeaponR.transform.rotation, "secondaryMelee", WeaponR);
        PrWeapon meleeWeaponComp = actualMeleeWeapon.GetComponent<PrWeapon>();
        //actualMeleeWeapon = Instantiate(meleeWeapon, WeaponR.transform.position, WeaponR.transform.rotation) as GameObject;
        //actualMeleeWeapon.transform.SetParent(WeaponR);
        meleeWeaponComp.hiddenWeapon = true;
        meleeWeaponComp.updateHUD = false;
        meleeWeaponComp.Audio = WeaponR.GetComponent<AudioSource>();
        meleeWeaponComp.ShootTarget = charController.AimFinalPos;
        meleeWeaponComp.Player = characterUtils.gameObject;
        meleeWeaponComp.playerController = charController.gameObject;
        meleeWeaponComp.plyrController = charController.playerNmb;
        if (charAnimator)
            meleeWeaponComp.playerAnimator = charAnimator;
        meleeRate = meleeWeaponComp.FireRate;
    }

    public void LoadBulletsAndClipsState(int weapon)
    {
        Weapon[weapon].GetComponent<PrWeapon>().ActualBullets = PrPlayerInfo.player1.weaponsAmmoP1[weapon];
        Weapon[weapon].GetComponent<PrWeapon>().ActualClips = PrPlayerInfo.player1.weaponsClipsP1[weapon];
    }

    // Update is called once per frame
    void Update()
    {

    }


    public void UnparentTransforms(Transform Target)
    {
        Target.SetParent(null);
    }

    void InstantiateWeapons()
    {
        //Debug.Log(gameObject.name + " " + "Instantiating weapon");
        int weapType = 0;
        foreach (PrWeapon Weap in InitialWeapons)
        {
            int weapInt = 0;
            ////Debug.Log("Weapon to instance = " + Weap);

            foreach (GameObject weap in WeaponList)
            {

                if (Weap.gameObject.name == weap.name)
                {
                    ////Debug.Log("Weapon to pickup = " + weap + " " + weapInt);
                    actualWeaponTypes[weapType] = weapInt;
                    PickupWeapon(weapInt);
                }

                else
                    weapInt += 1;
            }

            if (!AIInventory && GameObject.Find("playerInfo_" + charController.playerNmb))
            {
                LoadBulletsAndClipsState(weapType);
            }

            weapType += 1;
        }

    }

    public void UpdateWeaponsLevel()
    {
        foreach (GameObject w in Weapon)
        {
            w.GetComponent<PrWeapon>().UpdateWeaponLevel(character.lvlDamagePlusFactor);
        }
    }

    public void LoadGrenades(int quantity)
    {
        grenadesCount += quantity;
        if (grenadesCount > maxGrenades)
        {
            grenadesCount = maxGrenades;
        }
        if (characterHUD.HUDGrenadesCount)
            characterHUD.HUDGrenadesCount.GetComponent<Text>().text = grenadesCount.ToString();
    }

    void LateUpdate()
    {
        if (!AIInventory)
        {
            if (aimingIK && !character.isDead)
            {
                if (charController.Aiming && !actualWeapon.Reloading && !isThrowing && !charController.Rolling && !charController.Jumping && !charController.Sprinting)
                {
                    characterUtils.WeaponR.parent.transform.LookAt(charController.AimFinalPos.position, Vector3.up);
                }
                else if (charController.WASDOldStyleAim && !actualWeapon.Reloading && !isThrowing && !charController.Rolling && !charController.Jumping && !charController.Sprinting)
                {
                    characterUtils.WeaponR.parent.transform.LookAt(charController.AimFinalPos.position, Vector3.up);
                }
            }
                        
        }
              
    }

    public void ThrowG()
    {
        GameObject Grenade = Instantiate(grenadesPrefab, WeaponL.position, Quaternion.LookRotation(characterUtils.transform.forward)) as GameObject;
        Grenade.GetComponent<PrBullet>().team = character.team;
        Vector3 grenadeForce = characterUtils.transform.forward * Grenade.GetComponent<PrBullet>().BulletSpeed * 25 + Vector3.up * 2000;

        if (grenadeThrow == ThrowWay.AimPosition)
        {
            grenadeForce = (charController.AimFinalPos.position - characterUtils.transform.position).normalized * Grenade.GetComponent<PrBullet>().BulletSpeed * 20 + Vector3.up * 2000;
        }
        
        float targetDistance = Vector3.Distance(charController.AimFinalPos.transform.position, characterUtils.transform.position);
        Vector3 finalGrenadeForce = grenadeForce * (targetDistance / 20.0f);
        float MaxForce = ThrowGrenadeMaxForce * 17;
        finalGrenadeForce.x = Mathf.Clamp(finalGrenadeForce.x, -MaxForce, MaxForce);
        finalGrenadeForce.y = Mathf.Clamp(finalGrenadeForce.y, -MaxForce, MaxForce);
        finalGrenadeForce.z = Mathf.Clamp(finalGrenadeForce.z, -MaxForce, MaxForce);
        ////Debug.Log(finalGrenadeForce);
        Grenade.GetComponent<Rigidbody>().AddForce(finalGrenadeForce);
        Grenade.GetComponent<Rigidbody>().AddRelativeTorque(Grenade.transform.forward * 50f, ForceMode.Impulse);
        Grenade.GetComponent<PrBullet>().playerCamera = charController.CamScript;
        grenadesCount -= 1;
        if (characterHUD && characterHUD.HUDGrenadesCount)
            characterHUD.HUDGrenadesCount.GetComponent<Text>().text = grenadesCount.ToString();

        //Debug.Break();
    }

    public void EndThrow()
    {
        isThrowing = false;
        EnableArmIK(true);
    }


    public void EnableArmIK(bool active)
    {
        ////Debug.Log("Activating Arm IK " + active );
        if (CharacterIKController && useArmIK)
            if (actualWeapon.useIK)
            {
                CharacterIKController.ikActive = active;
                CharacterIKController.leftHandTarget = ArmIKTarget;
            }
            else
                CharacterIKController.ikActive = false;
    }

    public void WeaponReload()
    {
        if (actualWeapon.ActualBullets < actualWeapon.Bullets && actualWeapon.Reloading == false &&
            actualWeapon.ActualClips > 0)
        {
            actualWeapon.LaserSight.enabled = false;
            if (AIController)
                actualWeapon.AIReload();
            else
                actualWeapon.Reload();

            CanShoot = false;

            //Disable Arm IK
            EnableArmIK(false);

            if (characterHUD && actualWeapon.useQuickReload && !AIController)
            {
               characterHUD.QuickReloadActive(true);
            }
        }
    }

    void EndMelee()
    {
        if (!AIController)
        {
            charController.useRootMotion = false;
            charController.Rolling = false;
            charController.EndRoll();
            if (waitingArmIK)
                EnableArmIK(true);
            Weapon[ActiveWeapon].transform.rotation = WeaponR.rotation;
            Weapon[ActiveWeapon].transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
    }


    public void PickupItem()
    {
        if (charController.AutoPickupItems == false)
        {
            characterUtils.transform.rotation = Quaternion.LookRotation(PickupObj.transform.position - characterUtils.transform.position);
            characterUtils.transform.localEulerAngles = new Vector3(0, characterUtils.transform.localEulerAngles.y, 0);
        }
        
        PickupObj.SendMessage("PickupObjectNow", ActiveWeapon);
    }


    public void PickupWeapon(int WeaponType)
    {
        //Debug.Log(gameObject.name + " " + "picking Up Weapon");
        GameObject NewWeapon = PrUtils.InstantiateActor(WeaponList[WeaponType], WeaponR.position, WeaponR.rotation, "newWeapon", WeaponR);
        NewWeapon.transform.localRotation = Quaternion.Euler(90, 0, 0);
        if (!AIController)
        {
            NewWeapon.name = "Player_" + NewWeapon.GetComponent<PrWeapon>().WeaponName;
        }
        else
        {
            NewWeapon.name = "AI_" + NewWeapon.GetComponent<PrWeapon>().WeaponName;
        }
        actualWeaponTypes[ActiveWeapon] = WeaponType;

        //New multi weapon system
        bool replaceWeapon = true;

        for (int i = 0; i < playerWeaponLimit; i++)
        {
            if (Weapon[i] == null)
            {
                ////Debug.Log(i + " " + NewWeapon.name);
                Weapon[i] = NewWeapon;
                replaceWeapon = false;

                if (ActiveWeapon != i)
                {
                    // ChangeWeapon();
                    ChangeToWeapon(i);
                }
                break;
            }

        }
        if (replaceWeapon)
        {
            ////Debug.Log("Replacing weapon" + Weapon[ActiveWeapon].name + " using " + NewWeapon.name);
            if (charController.AutoPickupItems)
                Destroy(Weapon[ActiveWeapon]);
            else
                DestroyImmediate(Weapon[ActiveWeapon]);

            Weapon[ActiveWeapon] = NewWeapon;
            actualWeapon = Weapon[ActiveWeapon].GetComponent<PrWeapon>();

        }

        InitializeWeapons();

    }

    public void AddMoney(int ammount)
    {
        currentMoney += ammount;
    }

    public void EquipWeapon(bool bArmed)
    {
        if (charAnimator)
            charAnimator.SetBool("Armed", bArmed);
        Weapon[ActiveWeapon].SetActive(bArmed);
        if (characterHUD)
            Weapon[ActiveWeapon].GetComponent<PrWeapon>().UpdateWeaponGUI(characterHUD.HUDWeaponPicture);
        actualWeapon = Weapon[ActiveWeapon].GetComponent<PrWeapon>();

        if (charAnimator)
        {
            if (!bArmed)
            {
                //Debug.Log("Unarmed setup");
                int PistolLayer = charAnimator.GetLayerIndex("PistolLyr");
                charAnimator.SetLayerWeight(PistolLayer, 0.0f);
                int PistoActlLayer = charAnimator.GetLayerIndex("PistolActions");
                charAnimator.SetLayerWeight(PistoActlLayer, 0.0f);
                int RifleActlLayer = charAnimator.GetLayerIndex("RifleActions");
                charAnimator.SetLayerWeight(RifleActlLayer, 0.0f);
                int PartialActions = charAnimator.GetLayerIndex("PartialActions");
                charAnimator.SetLayerWeight(PartialActions, 0.0f);

                if (CharacterIKController)
                    CharacterIKController.enabled = false;
            }
            else if (bArmed)
            {
                if (Weapon[ActiveWeapon].GetComponent<PrWeapon>().Type == global::PrWeapon.WT.Pistol)
                {
                    //Debug.Log("Pistol setup");
                    int PistolLayer = charAnimator.GetLayerIndex("PistolLyr");
                    charAnimator.SetLayerWeight(PistolLayer, 1.0f);
                    int PistoActlLayer = charAnimator.GetLayerIndex("PistolActions");
                    charAnimator.SetLayerWeight(PistoActlLayer, 1.0f);
                    int RifleActlLayer = charAnimator.GetLayerIndex("RifleActions");
                    charAnimator.SetLayerWeight(RifleActlLayer, 0.0f);
                }
                else if (Weapon[ActiveWeapon].GetComponent<PrWeapon>().Type == global::PrWeapon.WT.Rifle)
                {
                    //Debug.Log("Rifle setup");
                    int PistolLayer = charAnimator.GetLayerIndex("PistolLyr");
                    charAnimator.SetLayerWeight(PistolLayer, 0.0f);
                    int PistoActlLayer = charAnimator.GetLayerIndex("PistolActions");
                    charAnimator.SetLayerWeight(PistoActlLayer, 0.0f);
                    int RifleActlLayer = charAnimator.GetLayerIndex("RifleActions");
                    charAnimator.SetLayerWeight(RifleActlLayer, 1.0f);
                }

                int PartAct = charAnimator.GetLayerIndex("PartialActions");
                charAnimator.SetLayerWeight(PartAct, 1.0f);

                if (CharacterIKController)
                    CharacterIKController.enabled = true;
            }

        }

        EnableArmIK(bArmed);

    }

    public void PickupKey(int KeyType)
    {
        if (KeyType == 0)
            BlueKeys += 1;
        else if (KeyType == 1)
            YellowKeys += 1;
        else if (KeyType == 2)
            RedKeys += 1;
        else if (KeyType == 3)
            FullKeys += 1;
    }

    void InitializeWeapons()
    {
        actualWeapon = Weapon[ActiveWeapon].GetComponent<PrWeapon>();
        if (charAnimator)
            actualWeapon.playerAnimator = charAnimator;
        Weapon[ActiveWeapon].SetActive(true);
        if (characterHUD)
        {
            characterHUD.HUDWeaponPicture.GetComponent<Image>().sprite = actualWeapon.WeaponPicture;
            actualWeapon.updateHUD = true;
        }
        else
        {
            actualWeapon.updateHUD = false;
        }
        
        if (charController)
        {
            actualWeapon.ShootTarget = charController.AimFinalPos;
            actualWeapon.plyrController = charController.playerNmb;
            if (charController.CamScript != null)
                actualWeapon.playerCamera = charController.CamScript;
        }

        actualWeapon.playerController = this.gameObject;
        actualWeapon.Player = characterUtils.gameObject;
        actualWeapon.team = character.team;
        actualWeapon.SetLayer();
        FireRateTimer = actualWeapon.FireRate;
        actualWeapon.BulletDamage = Mathf.RoundToInt(actualWeapon.BulletDamage * character.lvlDamagePlusFactor);

        if (characterHUD)
        {
            actualWeapon.HUDWeaponBullets = characterHUD.HUDWeaponBullets.GetComponent<Text>();
            actualWeapon.HUDWeaponBulletsBar = characterHUD.HUDWeaponBulletsBar.GetComponent<Image>();
            actualWeapon.HUDWeaponClips = characterHUD.HUDWeaponClips.GetComponent<Text>();

            useQuickReload = actualWeapon.useQuickReload;

            actualWeapon.HUDQuickReloadMarker = characterHUD.quickReloadMarker.GetComponent<RectTransform>();
            actualWeapon.HUDQuickReloadZone = characterHUD.quickReloadZone.GetComponent<RectTransform>();
            actualWeapon.SetupQuickReload();
        }

        //ArmIK
        if (useArmIK)
        {
            if (actualWeapon.gameObject.transform.Find("ArmIK"))
            {
                ArmIKTarget = actualWeapon.gameObject.transform.Find("ArmIK");
                if (characterUtils.gameObject.GetComponent<PrCharacterIK>() == null)
                {
                    characterUtils.gameObject.AddComponent<PrCharacterIK>();
                }
                if (characterUtils.gameObject.GetComponent<PrCharacterIK>())
                {
                    CharacterIKController = characterUtils.gameObject.GetComponent<PrCharacterIK>();
                }

                if (CharacterIKController)
                {
                    CharacterIKController.leftHandTarget = ArmIKTarget;
                    CharacterIKController.ikActive = true;
                }

            }
            else
            {
                if (CharacterIKController != null)
                    CharacterIKController.ikActive = false;
            }
        }

        actualWeapon.Audio = WeaponR.GetComponent<AudioSource>();

        if (charAnimator)
        {
            int PistolLayer = charAnimator.GetLayerIndex("PistolLyr");
            int PistolActLayer = charAnimator.GetLayerIndex("PistolActions");

            if (PistolLayer != -1)
                charAnimator.SetLayerWeight(PistolLayer, 0.0f);
            if (PistolActLayer != -1)
                charAnimator.SetLayerWeight(PistolActLayer, 0.0f);

            if (actualWeapon.Type == global::PrWeapon.WT.Pistol)
            {
                if (PistolLayer != -1)
                    charAnimator.SetLayerWeight(PistolLayer, 1.0f);
                if (PistolActLayer != -1)
                    charAnimator.SetLayerWeight(PistolActLayer, 1.0f);
                
                charAnimator.SetBool("Armed", true);
            }
            else if (actualWeapon.Type == global::PrWeapon.WT.Rifle)
            {
                //Debug.Log(name + "Rifle Picked up");
                charAnimator.SetBool("Armed", true);
            }
            else if (actualWeapon.Type == global::PrWeapon.WT.Melee)
            {
                //Debug.Log(name + "Melee Picked up");
                charAnimator.SetBool("Armed", false);
            }
            else if (actualWeapon.Type == global::PrWeapon.WT.Laser)
            {
                //Debug.Log(name + "Laser Picked up");
                charAnimator.SetBool("Armed", true);
            }
        }
        

        if (characterHUD)
            Weapon[ActiveWeapon].GetComponent<PrWeapon>().UpdateWeaponGUI(characterHUD.HUDWeaponPicture);

        if (AIInventory)
        {
            actualWeapon.AIWeapon = AIInventory;
            actualWeapon.AIFriendlyWeapon = AIController.friendlyAI;
            actualWeapon.LaserSight.enabled = false;
            if (actualWeapon.Type == PrWeapon.WT.Melee)
            { 
                AIController.attackDistance = actualWeapon.MeleeRadius;
                AIController.aimingDistance = AIController.attackDistance;
            }

            FireRateTimer = actualWeapon.FireRate;

            if (AIController.charTargetTransform)
                actualWeapon.AIEnemyTarget = AIController.charTargetTransform;
            
        }

    }

    public void ChangeToWeapon(int weaponInt)
    {
        lastWeaponChange = Time.time;

        int nextWeapon = weaponInt;

        ////Debug.Log("Changing Weapon " + Weapon[ActiveWeapon]);

        //New Multiple weapon system
        if (Weapon[nextWeapon] != null)
        {
            ////Debug.Log("Testing");
            foreach (GameObject i in Weapon)
            {
                if (i != null)
                {
                    i.GetComponent<PrWeapon>().LaserSight.enabled = false;
                    i.SetActive(false);
                }
                ////Debug.Log("Deactivating Weapon " + Weapon[ActiveWeapon]);
            }
            ////Debug.Log(ActiveWeapon + " " + nextWeapon);

            ActiveWeapon = nextWeapon;
            Weapon[ActiveWeapon].SetActive(true);

            InitializeWeapons();

            actualWeapon = Weapon[ActiveWeapon].GetComponent<PrWeapon>();
            if (characterHUD)
                actualWeapon.UpdateWeaponGUI(characterHUD.HUDWeaponPicture);
            

        }
    }

    public void ChangeWeapon()
    {
        lastWeaponChange = Time.time;

        int nextWeapon = ActiveWeapon + 1;
        if (nextWeapon >= playerWeaponLimit)
            nextWeapon = 0;

        //New Multiple weapon system
        if (Weapon[nextWeapon] != null)
        {
            ////Debug.Log(ActiveWeapon + " " + nextWeapon);
            foreach (GameObject i in Weapon)
            {
                if (i != null)
                {
                    i.GetComponent<PrWeapon>().LaserSight.enabled = false;
                    i.SetActive(false);
                }
                ////Debug.Log("Deactivating Weapon " + Weapon[ActiveWeapon]);
            }

            ActiveWeapon = nextWeapon;

            Weapon[ActiveWeapon].SetActive(true);

            InitializeWeapons();

        }
        else
        {
            for (int i = nextWeapon; i < playerWeaponLimit; i++)
            {
                if (Weapon[i] != null)
                {
                    ActiveWeapon = i - 1;
                    ChangeWeapon();
                    break;
                }
            }

            ActiveWeapon = playerWeaponLimit - 1;
            ////Debug.Log(playerWeaponLimit);
            ChangeWeapon();

        }

        actualWeapon = Weapon[ActiveWeapon].GetComponent<PrWeapon>();
        if (characterHUD)
            actualWeapon.UpdateWeaponGUI(characterHUD.HUDWeaponPicture);

    }


    public void EndPickup()
    {
        if (!AIInventory)
        {
            charController.m_CanMove = true;
            charController.UsingObject = false;
        }

        EnableArmIK(true);
    }

    public void EndReload()
    {
        CanShoot = true;
        if (charAnimator)
            charAnimator.SetBool("Reloading", false);
        if (characterHUD && !AIInventory && useQuickReload && characterHUD.quickReloadPanel && characterHUD.quickReloadMarker && characterHUD.quickReloadZone)
        {
            characterHUD.QuickReloadActive(false);
        }
        EnableArmIK(true);
    }


}
