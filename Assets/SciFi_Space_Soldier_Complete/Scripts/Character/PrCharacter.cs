using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class PrCharacter : PrActor
{
    //Ragdoll Vars

    [Header("Ragdoll setup")]
    public bool useRagdollDeath = false;
    public float ragdollForceFactor = 1.0f;
    public Vector3 ragdollDirection;

    [Header("Sound FX")]
    public float FootStepsRate = 0.4f;
    public float GeneralFootStepsVolume = 1.0f;
    public AudioClip[] Footsteps;
    private float LastFootStepTime = 0.0f;


    //COMPONENT VARIABLES
    [HideInInspector]
    public PrCharacterController characterController;
    [HideInInspector]
    public PrAIController AIController;
    [HideInInspector]
    public PrCharacterInventory characterInventory;
    [HideInInspector]
    public PrActorUtils actualCharUtils;
    [HideInInspector]
    public Animator charAnimator;


    protected override void Awake()
    {
        base.Awake();

        //ragdoll Initialization
        if (useRagdollDeath)
            actualActorPrefab.AddComponent<PrCharacterRagdoll>();
    }

    protected override void CreateCharacter()
    {
        base.CreateCharacter();

        if (actualActorPrefab.GetComponent<PrActorUtils>())
            actualCharUtils = actualActorPrefab.GetComponent<PrActorUtils>();
        else
            Debug.LogWarning("Character doesn´t have PrActorUtils Component, will not work properly");
        if (actualActorPrefab.GetComponent<Animator>())
            charAnimator = actualActorPrefab.GetComponent<Animator>();
        else
            Debug.LogWarning("Character doesn´t have Animator Component, will not work properly");

        actualCharUtils.character = this;

        //Set Character Type
        if (type == CHT.player)
            actualCharUtils.Type = PrActorUtils.AT.player;
        else if (type == CHT.enemy)
            actualCharUtils.Type = PrActorUtils.AT.enemy;
        else if (type == CHT.friendlyAI)
            actualCharUtils.Type = PrActorUtils.AT.friendlyAI;
        else if (type == CHT.neutralAI)
            actualCharUtils.Type = PrActorUtils.AT.neutralAI;

        MeshRenderers = actualCharUtils.MeshRenderers;
        BurnAndFrozenVFXParent = actualCharUtils.BurnAndFrozenVFXParent;
    }

    protected override void InitializeCharacterComponents()
    {


        base.InitializeCharacterComponents();



        if (type == CHT.player)
        {
            characterController = GetComponent<PrCharacterController>();
            characterInventory = GetComponent<PrCharacterInventory>();

            if (actualHUD)
            {
                actualHUD.character = this;
                actualHUD.characterInventory = characterInventory;
                actualHUD.charController = characterController;
                
            }

            characterInventory.WeaponR = actualCharUtils.WeaponR;
            characterInventory.WeaponL = actualCharUtils.WeaponL;

            characterController.character = this;
            characterController.controlledCharacter = actualActorPrefab;

            if (overrideAnimController)
            {
                if (actorAnimController && !characterController.useRootMotion)
                    charAnimator.runtimeAnimatorController = actorAnimController;
                else if (actorRootMotionAnimController && characterController.useRootMotion)
                    charAnimator.runtimeAnimatorController = actorRootMotionAnimController;
            }

            characterInventory.InitializeInventory();

            actualCharUtils.team = team;
            actualCharUtils.SetTagAndLayer();

            characterController.InitializeController();

            if (actualCharUtils.gameObject.GetComponent<Animator>())
            {
                actualCharUtils.charAnimator = actualCharUtils.gameObject.GetComponent<Animator>();
                actualCharUtils.charAnimator.applyRootMotion = characterController.useRootMotion;
            }

        }
        else if (type == CHT.enemy || type == CHT.friendlyAI || type == CHT.neutralAI)
        {
            AIController = GetComponent<PrAIController>();
            characterInventory = GetComponent<PrCharacterInventory>();


            if (actualHUD)
            {
                actualHUD.character = this;
                actualHUD.characterInventory = characterInventory;
                actualHUD.InitializeHUD();
            }

            characterInventory.WeaponR = actualCharUtils.WeaponR;
            characterInventory.WeaponL = actualCharUtils.WeaponL;

            AIController.character = this;
            AIController.controlledCharacter = actualActorPrefab;

            if (overrideAnimController)
            {
                if (actorAnimController && !AIController.useRootmotion)
                    charAnimator.runtimeAnimatorController = actorAnimController;
                else if (actorRootMotionAnimController && AIController.useRootmotion)
                    charAnimator.runtimeAnimatorController = actorRootMotionAnimController;
            }

            characterInventory.InitializeAIInventory();
            actualCharUtils.team = team;
            actualCharUtils.SetTagAndLayer();

            AIController.InitializeController();

            if (actualCharUtils.gameObject.GetComponent<Animator>())
            {
                actualCharUtils.charAnimator = actualCharUtils.gameObject.GetComponent<Animator>();
                actualCharUtils.charAnimator.applyRootMotion = AIController.useRootmotion;
            }

        }



    }
    
 
    public void FootStep(string stepType)
    {
        //
        if (stepType == "Rifle" || stepType == "")
        {
            if (Footsteps.Length > 0 && Time.time >= (LastFootStepTime + FootStepsRate))
            {
                int FootStepAudio = 0;

                if (Footsteps.Length > 1)
                {
                    FootStepAudio = Random.Range(0, Footsteps.Length);
                }
                float FootStepVolume = 1.0f;

                if (charAnimator)
                    FootStepVolume = charAnimator.GetFloat("Speed") * GeneralFootStepsVolume;

                if (type == CHT.player && characterController.Aiming)
                    FootStepVolume *= 0.5f;

                Audio.PlayOneShot(Footsteps[FootStepAudio], FootStepVolume);

                MakeNoise(FootStepVolume * 10f);
                LastFootStepTime = Time.time;
            }
        }

    }

    public void SetPlayerColors(int mode, int team, PrPlayerSettings playerSettings)
    {
        if (changeColorsInMultiplayer)
        {
            actualActorPrefab.GetComponent<PrActorUtils>().SetPlayerColors(mode, team, playerSettings);
        }
    }

    public void RollSound(AudioClip SFX)
    {
        if (SFX != null)
            Audio.PlayOneShot(SFX);
    }

    public void MakeNoise(float volume)
    {
        actualNoise = volume;
    }

    public override void ApplyDamagePassive(int damage)
    {
        if (!isDead && canBeDamaged)
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
  

    public override void ApplyDamage(int Damage, float temperatureMod, int enTeam, Vector3 bulletPosition, bool useVFX, float hitForce, int plyrNmb)
    {
        if (canBeDamaged)
        {

            if (ActualHealth > 0)
            {
                //set AI animations and settings when receive damage. 
                if (type == CHT.enemy || type == CHT.friendlyAI || type == CHT.neutralAI)
                {
                    characterInventory.EnableArmIK(false);

                    if (characterInventory.actualWeapon.Type == PrWeapon.WT.Melee)
                        AIController.SetCanAttack(false);

                    //Get Damage Direction
                    Vector3 hitDir = new Vector3(LastHitPos.x, 0, LastHitPos.z) - actualCharUtils.transform.position;
                    Vector3 front = actualCharUtils.transform.forward;

                    if (AIController.type == PrAIController.enemyType.Bot)
                    {
                        if (Vector3.Dot(front, hitDir) > 0)
                        {
                            if (charAnimator)
                                charAnimator.SetInteger("Side", 1);
                        }
                        else
                        {
                            if (charAnimator)
                                charAnimator.SetInteger("Side", 0);
                        }
                    }
                    if (charAnimator)
                    {
                        charAnimator.SetTrigger("Hit");
                        charAnimator.SetInteger("Type", Random.Range(0, AIController.hitAnimsMaxTypes));
                    }
                    

                    if (AIController.existingPossibleTargets.Count != 0 && !AIController.towerDefenseAI)
                    {
                        AIController.agent.ResetPath();
                        AIController.CheckPlayerNoise(AIController.existingPossibleTargets[0].transform.position);
                        AIController.actualState = PrAIController.AIState.ChasingPlayer;
                    }

                    if (AIController.stopIfGetHit)
                        AIController.agent.velocity = Vector3.zero;

                    if (useVFX && actualSplatVFX)
                    {
                        actualSplatVFX.transform.LookAt(LastHitPos);
                        actualSplatVFX.Splat();
                    }
                    
                }
            }
        }

        base.ApplyDamage(Damage, temperatureMod, enTeam, bulletPosition, useVFX, hitForce, plyrNmb);
    }




    public void AIDie(bool temperatureDeath)
    {
        //Send Message to Spawners
        characterInventory.EnableArmIK(false);
        SendMessageUpwards("EnemyDead", SendMessageOptions.DontRequireReceiver);

        if (characterInventory.actualWeapon.GetComponentInChildren<MeshRenderer>())
        {
            characterInventory.actualWeapon.GetComponentInChildren<MeshRenderer>().gameObject.layer = LayerMask.NameToLayer("Default");
        }
        else if (characterInventory.actualWeapon.GetComponentInChildren<SkinnedMeshRenderer>())
        {
            characterInventory.actualWeapon.GetComponentInChildren<SkinnedMeshRenderer>().gameObject.layer = LayerMask.NameToLayer("Default");
        }

        gameObject.tag = "Untagged";
        Vector3 hitDir = LastHitPos - AIController.controlledCharacter.transform.position;

        if (explosiveDeath && actualExplosiveDeathVFX)
        {
            actualExplosiveDeathVFX.transform.position = AIController.controlledCharacter.transform.position;
            actualExplosiveDeathVFX.transform.rotation = AIController.controlledCharacter.transform.rotation;
            actualExplosiveDeathVFX.SetActive(true);
            actualExplosiveDeathVFX.SendMessage("SetExplosiveForce", LastHitPos + new Vector3(0, 1.5f, 0), SendMessageOptions.DontRequireReceiver);

            //GameObject DieFXInstance = Instantiate(actualExplosiveDeathVFX, AIController.controlledCharacter.transform.Find("Root").transform.position, Quaternion.identity) as GameObject;
            //DieFXInstance.transform.parent = AIController.controlledCharacter.transform.Find("Root").transform;
        }

        if (AIController.type == PrAIController.enemyType.Bot)
        {
            BotDestruction(hitDir);
        }
        else if (AIController.type == PrAIController.enemyType.Humanoid)
        {
            HumanoidDestruction(temperatureDeath);
        }

    }

    void BotDestruction(Vector3 hitDir)
    {
        Destroy(AIController.AIRigidBody);
        Destroy(AIController.AIController);
        Destroy(AIController.agent);
        if (AIController.charAnimator)
            AIController.charAnimator.enabled = false;

        if (AIController.controlledCharacter.transform.Find("Root").GetComponent<SphereCollider>())
            AIController.controlledCharacter.transform.Find("Root").GetComponent<SphereCollider>().enabled = true;
        if (AIController.controlledCharacter.transform.Find("Root").GetComponent<Rigidbody>())
        {
            AIController.controlledCharacter.transform.Find("Root").GetComponent<Rigidbody>().isKinematic = false;
            AIController.controlledCharacter.transform.Find("Root").GetComponent<Rigidbody>().AddForce(hitDir * -10, ForceMode.Impulse);

        }

        if (DestroyOnDead)
        {
            if (GetComponent<PrDestroyTimer>())
            {
                PrDestroyTimer DestroyScript = GetComponent<PrDestroyTimer>();
                DestroyScript.enabled = true;
            }
        }

        gameObject.name = gameObject.name + "_DEAD";
        SendMessageUpwards("EnemyDead", SendMessageOptions.DontRequireReceiver);
    }

    void HumanoidDestruction(bool temperatureDeath)
    {
        AIController.actualState = PrAIController.AIState.Dead;
        if (charAnimator)
            charAnimator.SetBool("Dead", true);
        AIController.AIController.enabled = false;
        AIController.AIRigidBody.isKinematic = true;
        Destroy(AIController.agent);
        if (useRagdollDeath)
        {
            AIController.controlledCharacter.GetComponent<PrCharacterRagdoll>().ActivateRagdoll();
            ragdollDirection = ((AIController.controlledCharacter.transform.position + new Vector3(0, 1.5f, 0)) - (LastHitPos + new Vector3(0f, 1.6f, 0f))) * (ragdollForceFactor * Random.Range(0.8f, 1.2f));

            if (!temperatureDeath)
                AIController.controlledCharacter.GetComponent<PrCharacterRagdoll>().SetForceToRagdoll(LastHitPos + new Vector3(0f, 1.6f, 0f), ragdollDirection, BurnAndFrozenVFXParent);
        }

        if (explosiveDeath && actualExplosiveDeathVFX)
        {
            actualExplosiveDeathVFX.transform.position = AIController.controlledCharacter.transform.position;
            actualExplosiveDeathVFX.transform.rotation = AIController.controlledCharacter.transform.rotation;
            actualExplosiveDeathVFX.SetActive(true);
            actualExplosiveDeathVFX.SendMessage("SetExplosiveForce", LastHitPos + new Vector3(0, 1.5f, 0), SendMessageOptions.DontRequireReceiver);

            if (currentMinimapIcon)
            {
                currentMinimapIcon.color = Color.grey;
                currentMinimapIcon.transform.SetParent(null);
            }

            Destroy(this.gameObject);

        }
        else
        {
            if (deathVFX && actualDeathVFX)
            {
                if (temperatureDeath)
                {
                    //Freezing of Burning Death VFX...
                }
                else
                {
                    actualDeathVFX.transform.position = new Vector3(AIController.controlledCharacter.transform.position.x, 0.0f, AIController.controlledCharacter.transform.position.z);
                    actualDeathVFX.transform.LookAt(LastHitPos);
                    actualDeathVFX.transform.position = new Vector3(AIController.controlledCharacter.transform.position.x, deathVFXHeightOffset, AIController.controlledCharacter.transform.position.z);

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
    }

    void PlayerDie(bool temperatureDeath)
    {
        characterController.SavePlayerInfo(true);

        characterInventory.EnableArmIK(false);
        if (charAnimator)
            charAnimator.SetBool("Dead", true);

        characterInventory.actualWeapon.TurnOffLaser();

        //Set invisible to Bullets
        if (actualCharUtils.GetComponent<Rigidbody>())
            actualCharUtils.GetComponent<Rigidbody>().isKinematic = true;
        actualCharUtils.GetComponent<Collider>().enabled = false;

        actualCharUtils.tag = "Untagged";
        characterController.currentPlayerSelection.enabled = false;

        if (actualHUD)
            DestroyHUD();

        if (useRagdollDeath && !characterController.insideVehicle)
        {
            ragdollDirection = transform.position - LastHitPos;
            ragdollDirection = ragdollDirection.normalized;
            if (!temperatureDeath)
                actualCharUtils.GetComponent<PrCharacterRagdoll>().SetForceToRagdoll(LastHitPos + new Vector3(0, 1.5f, 0), ragdollDirection * (ragdollForceFactor * Random.Range(0.8f, 1.2f)), BurnAndFrozenVFXParent);
        }

        //Send Message to Game script to notify Dead
        SendMessageUpwards("PlayerDied", characterController.playerNmb, SendMessageOptions.DontRequireReceiver);
        SendMessageUpwards("NewFrag", enemyTeam, SendMessageOptions.DontRequireReceiver);
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject i in enemies)
        {
            i.SendMessage("FindPlayers", SendMessageOptions.DontRequireReceiver);
        }

        if (explosiveDeath && actualExplosiveDeathVFX)
        {
            actualExplosiveDeathVFX.transform.position = transform.position;
            actualExplosiveDeathVFX.transform.rotation = transform.rotation;
            actualExplosiveDeathVFX.SetActive(true);
            actualExplosiveDeathVFX.SendMessage("SetExplosiveForce", LastHitPos + new Vector3(0, 1.5f, 0), SendMessageOptions.DontRequireReceiver);

            //Destroy(this.gameObject);
        }

        else
        {
            if (deathVFX && actualDeathVFX && !characterController.insideVehicle)
            {
                if (temperatureDeath)
                {
                    //Freezing of Burning Death VFX...
                }
                else
                {
                    actualDeathVFX.transform.position = new Vector3(transform.position.x, 0.0f, transform.position.z);
                    actualDeathVFX.transform.LookAt(LastHitPos);
                    actualDeathVFX.transform.position = new Vector3(transform.position.x, 0.05f, transform.position.z);

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

        AIUpdatePlayerCount();

        Destroy(characterController);
        Destroy(actualCharUtils);
        Destroy(actualCharUtils.GetComponent<Collider>());
    }


    public override void Die(bool temperatureDeath, bool addXP)
    {
        base.Die(temperatureDeath, addXP);

        if (actualCharUtils.GetComponent<AutomaticFootIK>())
        {
            Debug.Log("found!");
            actualCharUtils.GetComponent<AutomaticFootIK>().InitiateRagdoll();
        }
        
        if (currentMinimapIcon)
        {
            currentMinimapIcon.color = Color.grey;
        }

        if (type == CHT.enemy || type == CHT.friendlyAI || type == CHT.neutralAI)
        { 
            AIDie(temperatureDeath);
        }
        else
        {
            PlayerDie(temperatureDeath);
        }
            
        
    }

    public void AIUpdatePlayerCount()
    {
        //Send Message to all AI
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length > 0)
        {
            foreach (GameObject e in enemies)
            {
                e.SendMessage("FindPlayers", SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    public void DestroyHUD()
    {
        if (characterInventory.Weapon[characterInventory.ActiveWeapon] != null)
        {
            //Destroy GUI
            characterInventory.Weapon[characterInventory.ActiveWeapon].GetComponent<PrWeapon>().updateHUD = false;
        }

        if (actualHUD.HUDDamageFullScreen != null)
        {
            if (actualHUD.HUDDamageFullScreen.transform.parent.gameObject != null)
                Destroy(actualHUD.HUDDamageFullScreen.transform.parent.gameObject);
        }
        if (actualHUD.HUDWeaponPicture != null)
        {
            if (actualHUD.HUDWeaponPicture.transform.parent.parent.gameObject != null)
                Destroy(actualHUD.HUDWeaponPicture.transform.parent.parent.gameObject);
        }
    }

    public void SpawnTeleportFX()
    {
        Damaged = true;
        DamagedTimer = 1.0f;
    }

    public virtual void OnDrawGizmos()
    {
       

        if (type == CHT.enemy)
        {
            Gizmos.color = Color.red;
        }
        else if (type == CHT.player)
        {
            Gizmos.color = Color.green;
        }
        else if (type == CHT.friendlyAI)
        {
            Gizmos.color = Color.yellow;
        }
        if (actorPrefab.Length > 0)
        {
            
            MeshFilter[] stMeshes = actorPrefab[0].GetComponentsInChildren<MeshFilter>();
            SkinnedMeshRenderer[] skMeshes = actorPrefab[0].GetComponentsInChildren<SkinnedMeshRenderer>();
            
            if (stMeshes.Length > 0)
            {
                foreach (MeshFilter st in stMeshes)
                {
                    Vector3 stMeshPosition = (new Vector3(0, st.transform.position.y, 0) * scale) + new Vector3(st.transform.position.x, 0, st.transform.position.z) + transform.position;
                    Gizmos.DrawMesh(st.sharedMesh, 0, stMeshPosition, transform.rotation * st.transform.rotation, st.transform.lossyScale * scale);
                }
            }

            if (skMeshes.Length > 0)
            {
                
                foreach (SkinnedMeshRenderer sk in skMeshes)
                {
                    Vector3 meshPosition = (new Vector3(0, sk.transform.position.y,0) * scale) + new Vector3(sk.transform.position.x, 0, sk.transform.position.z) + transform.position;
                    Gizmos.DrawMesh(sk.sharedMesh,0, meshPosition, transform.rotation * sk.transform.rotation, scale * sk.transform.lossyScale);
                }
            }


        }
    }
}
