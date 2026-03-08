using UnityEngine;
using System.Collections;

public class PrDestroyableActor : MonoBehaviour {

    [Header("Stats")]
    public int Health = 100;
    public bool Destroyable = true;
    private int ActualHealth = 0;
    private bool Destroyed = false;

    [Header("Temperature Settings")]
    public bool useTemperature = true;
    [HideInInspector]
    public float temperature = 1.0f;
    public float temperatureThreshold = 2.0f;
    public int temperatureDamage = 5;
    public float onFireSpeedFactor = 0.5f;
    private float tempTimer = 0;

    [Header("VFX")]
    public float shakeFactor = 3.0f;
    public float shakeDuration = 1.0f;
    public GameObject DestroyFX;
	public Vector3 DestroyFXOffset = new Vector3(0,0,0);
    private PrTopDownCamera playerCamera;

    [Space]
    public GameObject frozenVFX;
    private GameObject actualFrozenVFX;
    public GameObject burningVFX;
    private GameObject actualBurningVFX;

    [Header("Damage")]
    public bool RadialDamage = true;
    public float DamageRadius = 2;
    public int Damage = 50;

    private bool Damaged = false;
    private float DamagedTimer = 0.0f;
    public int team = 3;

    [Header("Loot")]
    public bool hasLoot = false;
    public int lootAmmount = 1;
    public float lootRandomPos = 0.5f;
    public GameObject[] lootItems;
    private GameObject[] currentLootItems;

    [Header("HUD")]
    public bool ShowHealthBar = false;
    public GameObject HealthBar;
    public float healthBarHeight = 1.5f;
    public Color HealthBarColor = Color.white;
    public bool fadeToLowHealth = true;
    public Color lowHealthBarColor = Color.red;
    private GameObject HealthBarParent;
    private GameObject currentHealthBar;
    private GameObject currentHealthBarGO;

    [Header("Debug")]
    public Mesh AreaRing;

    // Use this for initialization
    void Start () {
		ActualHealth = Health;
        if (GameObject.Find("PlayerCamera"))
            playerCamera = GameObject.Find("PlayerCamera").GetComponent<PrTopDownCamera>();
        if (HealthBar && ShowHealthBar && !currentHealthBar)
        {
            currentHealthBarGO = Instantiate(HealthBar, this.transform);
            currentHealthBarGO.transform.position = this.transform.position + new Vector3(0, healthBarHeight, 0);
            currentHealthBar = currentHealthBarGO.transform.Find("Canvas/BarBack/Bar").gameObject;
            currentHealthBar.GetComponent<UnityEngine.UI.Image>().color = HealthBarColor;
        }

        if (useTemperature)
        {
            if (frozenVFX)
            {
                actualFrozenVFX = Instantiate(frozenVFX, transform.position, transform.rotation) as GameObject;
                actualFrozenVFX.transform.position = transform.position;
                actualFrozenVFX.transform.parent = transform;
            }
            if (burningVFX)
            {
                actualBurningVFX = Instantiate(burningVFX, transform.position, transform.rotation) as GameObject;
                actualBurningVFX.transform.position = transform.position;
                actualBurningVFX.transform.parent = transform;
            }
        }

        if (hasLoot)
        {
            SpawnLoot();
        }
    }

    void ActivateHealthBar(bool active)
    {
        HealthBar.GetComponent<UnityEngine.UI.Image>().color = HealthBarColor;
        HealthBarParent = HealthBar.transform.parent.gameObject;
        HealthBarParent.SetActive(active);
    }
	
	// Update is called once per frame
	void Update () {
        if ( Damaged )
        {
            DamagedTimer = Mathf.Lerp(DamagedTimer, 0.0f, Time.deltaTime * 10);
           
            if (Mathf.Approximately(DamagedTimer, 0.0f))
            {
                DamagedTimer = 0.0f;
                Damaged = false;
            }

            GetComponent<MeshRenderer>().material.SetFloat("_DamageFX", DamagedTimer);
        }
        if (useTemperature)
            ApplyTemperature();

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

        if (GetComponent<MeshRenderer>().material.HasProperty("_FrozenMix"))
            GetComponent<MeshRenderer>().material.SetFloat("_FrozenMix", Mathf.Clamp(1.0f - temperature, 0.0f, 1.0f));
        if (GetComponent<MeshRenderer>().material.HasProperty("_BurningMix"))
            GetComponent<MeshRenderer>().material.SetFloat("_BurningMix", Mathf.Clamp(temperature - 1.0f, 0.0f, 1.0f));

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

    public virtual void applyTemperatureDamage()
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

    void ApplyDamagePassive(int damage)
    {
        if (!Destroyed)
        {
            Health -= damage;

            if (Health <= 0)
            {
                DestroyActor();
            }
        }
    }

    void ApplyTempMod(float temperatureMod)
    {
        temperature += temperatureMod;
        temperature = Mathf.Clamp(temperature, 0.0f, temperatureThreshold);
    }

    public void ApplyDamage(int Damage)
	{
        if (Destroyable && !Destroyed)
        {
            ActualHealth -= Damage;

            Damaged = true;
            DamagedTimer = 1.0f;

            if (HealthBar)
            {
                currentHealthBar.GetComponent<UnityEngine.UI.Image>().transform.localScale = new Vector3((1.0f / Health) * ActualHealth, 0.6f, 1.0f);
                if (fadeToLowHealth)
                {
                    currentHealthBar.GetComponent<UnityEngine.UI.Image>().color = Color.Lerp(lowHealthBarColor, HealthBarColor, (1.0f / Health) * ActualHealth);
                }
            }

            if (ActualHealth <= 0 && !Destroyed)
            {
                DestroyActor();
            }
        }
		
	}

    void SpawnLoot()
    {
        if (lootItems.Length > 0)
        {
            currentLootItems = new GameObject[lootAmmount];
            for (int i = 0; i < lootAmmount; i++)
            {
                // Choose random loot
                int randomLoot = Random.Range(0, lootItems.Length);
                Vector3 randomLootPos = new Vector3(
                    Random.Range(lootRandomPos, -lootRandomPos),
                    0.0f,
                    Random.Range(lootRandomPos, -lootRandomPos)
                    );

                // Instantiate Loot
                GameObject tempLoot = Instantiate(lootItems[randomLoot]);
                tempLoot.transform.position = transform.position + randomLootPos;
                tempLoot.transform.SetParent(transform);
                // deactivate Loot
                tempLoot.SetActive(false);

                // Add loot to currentLoot variable.
                currentLootItems[i] = tempLoot;

                tempLoot.GetComponent<PrPickupObject>().Initialize();
            }
        }
    }

    void ActivateLoot()
    {
        if (currentLootItems.Length > 0)
        {
            foreach (GameObject lootItem in currentLootItems)
            {
                lootItem.SetActive(true);
                lootItem.transform.SetParent(null);
            }
        }
    }

	void DestroyActor()
	{
        Destroyed = true;
        SendMessageUpwards("ActorDestroyed", SendMessageOptions.DontRequireReceiver);

        if (DestroyFX)
			Instantiate(DestroyFX, transform.position + DestroyFXOffset, Quaternion.identity);

        if (playerCamera)
        {
            playerCamera.ExplosionShake(shakeFactor, shakeDuration);
        }

        if (hasLoot)
        {
            ActivateLoot();
        }

		Destroy(this.gameObject);

        if (RadialDamage)
        {
            if (GetComponent<Rigidbody>())
                DestroyImmediate(GetComponent<Rigidbody>());
            if (GetComponent<Collider>())
                DestroyImmediate(GetComponent<Collider>());

            Vector3 explosivePos = transform.position + DestroyFXOffset;
            Collider[] colls = Physics.OverlapSphere(explosivePos, DamageRadius);
            foreach (Collider col in colls)
            {
                if (col != null)
                {
                    if (col.CompareTag("Player") || col.CompareTag("Enemy") || col.CompareTag("Destroyable"))
                    {
                        col.SendMessage("ApplyDamage", Damage, SendMessageOptions.DontRequireReceiver);
                    }
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        if (RadialDamage)
          Gizmos.DrawMesh(AreaRing, transform.position + DestroyFXOffset, Quaternion.identity,Vector3.one * DamageRadius);

        if (ShowHealthBar && HealthBar)
        {
            Gizmos.color = HealthBarColor;
            Gizmos.DrawCube(transform.position + new Vector3(0, healthBarHeight, 0), new Vector3(3, 0.3f, 0.1f));
        }
    }
}
