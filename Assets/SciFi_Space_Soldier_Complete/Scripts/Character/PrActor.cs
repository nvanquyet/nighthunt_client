using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrActor : MonoBehaviour
{
    public enum CHT
    {
        player, enemy, friendlyAI, neutralAI, destroyable, vehicle
    }
    [Header("Actor Type")]
    public CHT type = CHT.player;
    public GameObject[] actorPrefab;
    [HideInInspector]
    public GameObject actualActorPrefab;
    public bool randomPrefab = false;
    public bool overrideAnimController = true;
    public RuntimeAnimatorController actorAnimController;
    public RuntimeAnimatorController actorRootMotionAnimController;
    public string Name = "Humanoid";
    public bool changeColorsInMultiplayer = true;

    [Range(0.1f, 10.0f)]
    public float scale = 1.0f;
    [Range(0.0f, 0.95f)]
    public float scaleRandomVariation = 0.0f;

    [Header("Actor Stats")]
    public bool canBeDamaged = true;
    public int Health = 100;
    public int ActualHealth = 100;
    public bool oneShotHealth = false;
    [Space]
    public float Stamina = 1.0f;
    public float StaminaRecoverSpeed = 0.5f;
    [HideInInspector] public float ActualStamina = 1.0f;
    public float StaminaRecoverLimit = 0.5f;
    [HideInInspector] public float ActualStaminaRecover = 0.5f;
    [HideInInspector] public bool UsingStamina = false;
    [HideInInspector] public bool isDead = false;

    [Space]
    public bool DestroyOnDead = false;
    public float destroyOnDeadtimer = 10.0f;

    public bool Damaged = false;
    [HideInInspector] public float DamagedTimer = 0.0f;
    [HideInInspector] public SphereCollider NoiseTrigger;
    [HideInInspector]
    [Space]
    public float actualNoise = 0.0f;
    public float noiseDecaySpeed = 10.0f;

    public int team = 0;
    public int enemyTeam = 1;

    [Header("Temperature Settings")]
    public bool useTemperature = true;
    [HideInInspector]
    public float temperature = 1.0f;
    public float temperatureThreshold = 2.0f;
    public int temperatureDamage = 5;
    public float onFireSpeedFactor = 0.5f;
    public float tempTimer = 0;

    [Header("Actor VFX")]
    public GameObject spawnFX;
    public GameObject DamageFX;
    [HideInInspector]
    public Vector3 LastHitPos = Vector3.zero;

    [HideInInspector]
    public AudioSource Audio;

    /// RENDERERS IN CHARACTER UTIL
    //[HideInInspector]
    public Renderer[] MeshRenderers;

    [Space]
    public Transform BurnAndFrozenVFXParent;
    public GameObject frozenVFX;
    [HideInInspector]
    public GameObject actualFrozenVFX;
    public GameObject burningVFX;
    [HideInInspector]
    public GameObject actualBurningVFX;
    [Space]
    public GameObject damageSplatVFX;
    [HideInInspector]
    public PrBloodSplatter actualSplatVFX;
    [Space]
    public GameObject deathVFX;
    public float deathVFXHeightOffset = 0.0f;
    [HideInInspector]
    public GameObject actualDeathVFX;

    [Space]
    //Explosive Death VFX
    public bool useExplosiveDeath = true;
    public bool explosiveDeath = false;
    public int damageThreshold = 50;
    public GameObject explosiveDeathVFX;
    [HideInInspector]
    public GameObject actualExplosiveDeathVFX;

    [Header("XP")]
    public int XP = 0;
    private int actualXP = 0;
    public int XPLevel = 1;
    public int XPMax = 500;
    private int actualXPLevel = 0;
    public int XPReward = 50;
    private int killerPlyrNmbr = 0;
    public bool lvlExpandHealth = true;
    public bool lvlExpandDamage = true;
    public float lvlHealthPlusFactor = 1.1f;
    private float actualLvlDamagePlusFactor = 1.0f;
    public float lvlDamagePlusFactor = 1.1f;

    [Header("Actor HUD")]
    public bool useHUD = true;
    public GameObject HUD;
    private GameObject actualHUDGO;
    [HideInInspector]
    public PrActorHUD actualHUD;

    [Header("Actor Minimap")]
    public Sprite customMinimapIcon;
    public SpriteRenderer currentMinimapIcon;

    // Start is called before the first frame update
    // Use this for initialization

    protected virtual void InitializeCharacterComponents()
    {
        if (useHUD && HUD)
        {
            CreateActorHUD();
        }
    }

    protected virtual void Awake()
    {
        ///
        ///Instantiate actor prefab
        ///
        if (actorPrefab.Length > 0)
        {
            CreateCharacter();
        }

        //Set actor Health
        ActualHealth = Health;
        ActualStamina = Stamina;
        ActualStaminaRecover = StaminaRecoverLimit;

        if (type == CHT.destroyable || type == CHT.vehicle)
            actualActorPrefab = this.gameObject;

        //Set AudioSource for Noise 
        if (actualActorPrefab.GetComponent<AudioSource>() == null)
            Audio = actualActorPrefab.AddComponent<AudioSource>();
        else
            Audio = actualActorPrefab.GetComponent<AudioSource>();

        CreateNoiseTrigger();

        // Create actor VFX 
        CreateCharacterVFX();

        InitializeCharacterComponents();

    }

    protected virtual void CreateActorHUD()
    {
        actualHUDGO = PrUtils.InstantiateActor(HUD, transform.position, transform.rotation, Name + "_HUD", null);
        actualHUD = actualHUDGO.GetComponent<PrActorHUD>();
    }

    protected virtual void CreateCharacter()
    {
        int selectedPrefab = 0;
        if (randomPrefab)
        {
            selectedPrefab = Random.Range(0, actorPrefab.Length);
        }

        actualActorPrefab = PrUtils.InstantiateActor(actorPrefab[selectedPrefab], transform.position, transform.rotation, Name, transform);
        actualActorPrefab.transform.localScale *= Mathf.Clamp((scale + Random.Range(-scaleRandomVariation, scaleRandomVariation)),0.01f, (scale + Random.Range(-scaleRandomVariation, scaleRandomVariation)));

        if (type == CHT.player)
        {
            transform.Find("PlayerExtras").GetComponent<PrCopyPosition>().targetObject = actualActorPrefab.transform;
            actualActorPrefab.layer = LayerMask.NameToLayer("PlayerCharacter");
        }
        else if (type == CHT.enemy || type == CHT.friendlyAI || type == CHT.neutralAI)
        {
            actualActorPrefab.layer = LayerMask.NameToLayer("Character");

            actualActorPrefab.transform.position = PrUtils.RaycastToFloor(actualActorPrefab.transform.position);

        }

        if (spawnFX)
            Instantiate(spawnFX, transform.position, Quaternion.identity);
    }

    public void SetIconColor(Color spriteColor)
    {
        if (currentMinimapIcon)
        {
            currentMinimapIcon.color = spriteColor;
        }
    }

    public void SetUpMinimapIcons(float scale, Color spriteColor)
    {
        //Debug.Log(spriteColor);

        if (currentMinimapIcon)
        {
            if (customMinimapIcon)
            {
                currentMinimapIcon.sprite = customMinimapIcon;
            }

            currentMinimapIcon.transform.localScale = Vector3.one * scale;
            SetIconColor(spriteColor);
        }
    }

    public void CreateCharacterVFX()
    {
        if (useExplosiveDeath && explosiveDeathVFX)
        {
            actualExplosiveDeathVFX = Instantiate(explosiveDeathVFX, transform.position, transform.rotation) as GameObject;
            actualExplosiveDeathVFX.SetActive(false);

            if (GameObject.Find("VFXBloodParent"))
                actualExplosiveDeathVFX.transform.parent = GameObject.Find("VFXBloodParent").transform;
            else
            {
                GameObject VFXParent = new GameObject("VFXBloodParent") as GameObject;
                actualExplosiveDeathVFX.transform.parent = VFXParent.transform;
            }
        }

        if (deathVFX)
        {
            actualDeathVFX = Instantiate(deathVFX, transform.position, transform.rotation) as GameObject;
            actualDeathVFX.SetActive(false);

            if (GameObject.Find("VFXBloodParent"))
                actualDeathVFX.transform.parent = GameObject.Find("VFXBloodParent").transform;
            else
            {
                GameObject VFXParent = new GameObject("VFXBloodParent") as GameObject;
                actualDeathVFX.transform.parent = VFXParent.transform;
            }
        }

        if (damageSplatVFX)
        {
            GameObject GOactualSplatVFX = PrUtils.InstantiateActor(damageSplatVFX, actualActorPrefab.transform.position, actualActorPrefab.transform.rotation, "actualDamageSplatVFX", actualActorPrefab.transform);
            actualSplatVFX = GOactualSplatVFX.GetComponent<PrBloodSplatter>();
        }
        if (frozenVFX)
        {
            actualFrozenVFX = PrUtils.InstantiateActor(frozenVFX, actualActorPrefab.transform.position, actualActorPrefab.transform.rotation, "actualFrozenVFX", actualActorPrefab.transform);
            if (BurnAndFrozenVFXParent)
                actualFrozenVFX.transform.parent = BurnAndFrozenVFXParent;
        }
        if (burningVFX)
        {
            actualBurningVFX = PrUtils.InstantiateActor(burningVFX, actualActorPrefab.transform.position, actualActorPrefab.transform.rotation, "actualBurningVFX", actualActorPrefab.transform);
            if (BurnAndFrozenVFXParent)
                actualBurningVFX.transform.parent = BurnAndFrozenVFXParent;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //Update Damage
        DamageUpdate();

        if (!isDead)
        {
            //Apply Temperature
            ApplyTemperature();

            //Manage Stamina
            StaminaUpdate();

            //Noise Manager
            NoiseUpdate();
        }

    }

    void ExpandHealth()
    {
        //Multiplies Health by lvlHealthFactor to improve health after new XP level reached
        Health = Mathf.RoundToInt(Health * lvlHealthPlusFactor);
        ActualHealth = Health;
        //Debug.Log(ActualHealth);
    }

    void ExpandDamage()
    {
        actualLvlDamagePlusFactor = actualLvlDamagePlusFactor * lvlDamagePlusFactor;
        //Debug.Log(actualLvlDamagePlusFactor);
    }

    public void AddXP(int XpPoints)
    {
        //Debug.Log("Player Adding XP");
        actualXP += XpPoints;
        if (actualXP >= XPMax)
        {
            actualXPLevel += 1;
            actualXP = actualXP - XPMax;

            if (lvlExpandHealth)
                ExpandHealth();
            if (lvlExpandDamage)
                ExpandDamage();
        }

        actualHUD.UpdateXP(actualXPLevel, actualXP, XPMax);
    }

    protected virtual void StaminaUpdate()
    {
        if (ActualStaminaRecover >= StaminaRecoverLimit)
        {
            if (UsingStamina)
            {
                if (ActualStamina > 0.05f)
                    ActualStamina -= Time.deltaTime;
                else if (ActualStamina > 0.0f)
                {
                    ActualStamina = 0.0f;
                    ActualStaminaRecover = 0.0f;
                }


            }
            else if (!UsingStamina)
            {
                if (ActualStamina < Stamina)
                    ActualStamina += Time.deltaTime * StaminaRecoverSpeed;
                else
                    ActualStamina = Stamina;
            }
        }
        else if (ActualStaminaRecover < StaminaRecoverLimit)
        {
            ActualStaminaRecover += Time.deltaTime;
        }
        
        if (actualHUD)
            actualHUD.HUDStaminaBar.GetComponent<RectTransform>().localScale = new Vector3((1.0f / Stamina) * ActualStamina, 1.0f, 1.0f);
        
    }

    void NoiseUpdate()
    {
        if (type == CHT.friendlyAI || type == CHT.player || type == CHT.vehicle)
        {
            if (actualNoise > 0.0f)
            {
                actualNoise -= Time.deltaTime * noiseDecaySpeed;
                NoiseTrigger.radius = actualNoise;
            }
        }
    }

    void ApplyOneShotHealth()
    {
        ActualHealth = 1;
    }

    void CreateNoiseTrigger()
    {
        if (type == CHT.player || type == CHT.friendlyAI)
        {
            GameObject NoiseGO = new GameObject();
            NoiseGO.name = "Player Noise Trigger";
            NoiseGO.AddComponent<SphereCollider>();
            NoiseGO.AddComponent<PrCopyPosition>();
            NoiseGO.GetComponent<PrCopyPosition>().targetObject = actualActorPrefab.transform;
            NoiseTrigger = NoiseGO.GetComponent<SphereCollider>();
            NoiseTrigger.GetComponent<SphereCollider>().isTrigger = true;
            NoiseTrigger.transform.parent = this.transform;
            NoiseTrigger.gameObject.tag = "Noise";
        }

    }


    protected virtual void DamageUpdate()
    {
        if (Damaged && MeshRenderers.Length > 0)
        {
            DamagedTimer = Mathf.Lerp(DamagedTimer, 0.0f, Time.deltaTime * 10);

            if (Mathf.Approximately(DamagedTimer, 0.0f))
            {
                DamagedTimer = 0.0f;
                Damaged = false;
            }

            foreach (Renderer Mesh in MeshRenderers)
            {
                if (Mesh.material.HasProperty("_DamageFX"))
                    Mesh.material.SetFloat("_DamageFX", DamagedTimer);
            }
            /*
            foreach (SkinnedMeshRenderer SkinnedMesh in MeshRenderers)
            {
                if (SkinnedMesh.material.HasProperty("_DamageFX"))
                    SkinnedMesh.material.SetFloat("_DamageFX", DamagedTimer);
            }*/
        }

        if (useHUD && actualHUD && actualHUD.HUDDamageFullScreen)
            actualHUD.HUDDamageFullScreen.GetComponent<UnityEngine.UI.Image>().color = new Vector4(1, 1, 1, DamagedTimer * 0.5f);
    }

    void ApplyTemperature()
    {
        if (temperature > 1.0f || temperature < 1.0f)
        {
            if (tempTimer < 1.0f)
                tempTimer += Time.deltaTime;
            else
            {
                tempTimer = 0.0f;
                applyTemperatureDamage();
            }
        }

        foreach (Renderer Mesh in MeshRenderers)
        {
            if (Mesh.material.HasProperty("_FrozenMix"))
                Mesh.material.SetFloat("_FrozenMix", Mathf.Clamp(1.0f - temperature, 0.0f, 1.0f));
            if (Mesh.material.HasProperty("_BurningMix"))
                Mesh.material.SetFloat("_BurningMix", Mathf.Clamp(temperature - 1.0f, 0.0f, 1.0f));
        }

        foreach (SkinnedMeshRenderer SkinnedMesh in MeshRenderers)
        {
            if (SkinnedMesh.material.HasProperty("_FrozenMix"))
                SkinnedMesh.material.SetFloat("_FrozenMix", Mathf.Clamp(1.0f - temperature, 0.0f, 1.0f));
            if (SkinnedMesh.material.HasProperty("_BurningMix"))
                SkinnedMesh.material.SetFloat("_BurningMix", Mathf.Clamp(temperature - 1.0f, 0.0f, 1.0f));
        }

        if (actualFrozenVFX)
        {
            if (temperature < 1.0f)
                actualFrozenVFX.SetActive(true);
            else
            {
                actualFrozenVFX.SetActive(false);
            }
        }
        if (actualBurningVFX)
        {
            if (temperature > 1.0f)
                actualBurningVFX.SetActive(true);
            else
            {
                actualBurningVFX.SetActive(false);
            }
        }
    }

    void applyTemperatureDamage()
    {
        if (temperature < 1.0f)
        {
            ApplyDamagePassive(temperatureDamage);
        }
        else if (temperature > 1.0f)
        {
            ApplyDamagePassive(temperatureDamage);
        }
    }

    public virtual void ApplyDamagePassive(int damage)
    {
        if (!isDead)
        {
            SetHealth(ActualHealth - damage);

            Damaged = true;
            DamagedTimer = 1.0f;

            if (ActualHealth <= 0)
            {
                if (actualSplatVFX)
                    actualSplatVFX.transform.parent = null;

                Die(true, false);
            }
        }
    }

    public void ApplyTempMod(float temperatureMod)
    {
        temperature += temperatureMod;
        temperature = Mathf.Clamp(temperature, 0.0f, temperatureThreshold);
    }

    public virtual void SetHealth(int HealthInt)
    {
        ActualHealth = HealthInt;

        if (useHUD && actualHUD)
        {
            if (ActualHealth > 1)
            {
                actualHUD.SetHealthBar(HealthInt, Health);
            }
            else
            {
                actualHUD.SetHealthBar(0.0f, Health);
            }
        }
       
        ////Debug.Log(ActualHealth + " 1");
    }

    public void PlayerTeam(int enTeam)
    {
        enemyTeam = enTeam;
        if (enemyTeam == team)
        {
            enemyTeam += 1;
            enemyTeam *= -1;
        }
    }

    public void BulletPos(Vector3 BulletPosition)
    {
        LastHitPos = BulletPosition;
        LastHitPos.y = 0;
    }

    public virtual void ApplyDamage(int Damage)
    {
        if (canBeDamaged)
        {
            if (ActualHealth > 0)
            {
                //Here you can put some Damage Behaviour if you want
                SetHealth(ActualHealth - Damage);

                Damaged = true;
                DamagedTimer = 1.0f;

                if (ActualHealth <= 0)
                {
                    if (actualSplatVFX)
                        actualSplatVFX.transform.parent = null;
                    if (Damage >= damageThreshold)
                        explosiveDeath = true;
                    Die(false, true);
                }

            }
        }
    }

    public virtual void ApplyDamage(int Damage, float temperatureMod, int enTeam, Vector3 bulletPosition, bool useVFX, float hitForce, int plyrNmb)
    {
        if (canBeDamaged)
        {
            PlayerTeam(enTeam);
            ApplyTempMod(temperatureMod);
            BulletPos(bulletPosition);

            if (ActualHealth > 0)
            {
                //Here you can put some Damage Behaviour if you want
                SetHealth(ActualHealth - Damage);

                Damaged = true;
                DamagedTimer = 1.0f;

                if (actualSplatVFX && useVFX)
                {
                    actualSplatVFX.transform.LookAt(LastHitPos);
                    actualSplatVFX.Splat();
                }

                if (ActualHealth <= 0)
                {
                    killerPlyrNmbr = plyrNmb;
                    if (actualSplatVFX)
                        actualSplatVFX.transform.parent = null;
                    if (Damage >= damageThreshold)
                        explosiveDeath = true;
                    Die(false, true);
                }

            }
        }
    }

    public virtual void Die(bool temperatureDeath, bool addXP)
    {
        isDead = true;

        actualActorPrefab.gameObject.tag = "Untagged";

        if (useHUD && actualHUD)
            actualHUD.DeactivateCompass();

        if (MeshRenderers.Length > 0)
        {
            foreach (SkinnedMeshRenderer m in MeshRenderers)
            {
                m.gameObject.layer = LayerMask.NameToLayer("Default");
            }
        }

        if (addXP)
        {
            if (GameObject.FindGameObjectWithTag("Game"))
            {
                if (GameObject.FindGameObjectWithTag("Game").GetComponent<PrGameSetup>())
                    GameObject.FindGameObjectWithTag("Game").GetComponent<PrGameSetup>().SetXP(killerPlyrNmbr, XPReward);
            }
        }

    }

}
